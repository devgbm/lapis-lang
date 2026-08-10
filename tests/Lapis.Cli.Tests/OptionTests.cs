using Lapis.Cli;
using Lapis.Diagnostics;

namespace Lapis.Cli.Tests;

/// <summary>Flags globais do plano 10 §10.3.</summary>
public sealed class OptionTests
{
    private const string Broken = "def x = inexistente;\n";
    private const string Escape = "\u001b";

    // ------------------------------------------------------------------ --json

    [Fact]
    public void Json_EmitsOneLinePerDiagnostic()
    {
        var (exit, _, stderr) = CliRunner.RunSource(
            "def x = inexistente;\ndef y = tambem_inexistente;\n", "check", "--json");

        exit.ShouldBe(ExitCodes.CompilationError);
        stderr.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n').Length.ShouldBe(2);
    }

    [Fact]
    public void Json_HasTheDocumentedFields()
    {
        var (_, _, stderr) = CliRunner.RunSource(Broken, "check", "--json");

        stderr.ShouldContain("\"file\":");
        stderr.ShouldContain("\"line\":1");
        stderr.ShouldContain("\"column\":9");
        stderr.ShouldContain("\"severity\":\"error\"");
        stderr.ShouldContain("\"code\":\"LAP0201\"");
        stderr.ShouldContain("\"message\":");
    }

    /// <summary>JSON é para máquina: nada de trecho de código, cursor ou ANSI.</summary>
    [Fact]
    public void Json_HasNoHumanDecoration()
    {
        var (_, _, stderr) = CliRunner.RunSource(Broken, "check", "--json");

        stderr.ShouldNotContain("^");
        stderr.ShouldNotContain(Escape);
    }

    [Fact]
    public void Json_EscapesQuotesAndBackslashes()
    {
        var diagnostic = Diagnostic.Error("LAP9999", new SourceSpan(0, 1), "aspas \" e barra \\ e \n");
        var line = DiagnosticJson.Render(diagnostic, SourceText.From("x", "a.ls"));

        line.ShouldContain("\\\"");
        line.ShouldContain("\\\\");
        line.ShouldContain("\\n");
        line.ShouldEndWith("}");
    }

    [Fact]
    public void Json_KeepsAccentsLiteral()
    {
        var diagnostic = Diagnostic.Error("LAP9999", new SourceSpan(0, 1), "acentuação");

        DiagnosticJson.Render(diagnostic, SourceText.From("x", "a.ls")).ShouldContain("acentuação");
    }

    [Fact]
    public void Json_RendersNotes()
    {
        var diagnostic = Diagnostic.Error(
            "LAP9999", new SourceSpan(0, 1), "erro", new DiagnosticNote("uma nota", new SourceSpan(0, 1)));

        DiagnosticJson.Render(diagnostic, SourceText.From("x", "a.ls")).ShouldContain("\"notes\":[\"uma nota\"]");
    }

    // -------------------------------------------------------------- --no-color

    [Fact]
    public void NoColor_ProducesNoAnsi()
    {
        var (_, _, stderr) = CliRunner.RunSource(Broken, "check", "--no-color");

        stderr.ShouldNotContain(Escape);
    }

    /// <summary>
    /// A decisão em si, sem depender de como o processo de teste foi lançado:
    /// só há cor quando ninguém pediu o contrário e a saída é um terminal.
    /// </summary>
    [Theory]
    [InlineData(false, false, null, true)]
    [InlineData(true, false, null, false)]
    [InlineData(false, true, null, false)]
    [InlineData(false, false, "1", false)]
    [InlineData(false, false, "", true)]
    public void ColorDecision_FollowsFlagRedirectionAndEnvironment(
        bool noColor, bool redirected, string? variable, bool expected) =>
        Program.ShouldUseColor(noColor, redirected, variable).ShouldBe(expected);

    [Fact]
    public void Renderer_WithColor_WrapsSeverityInAnsi()
    {
        var source = SourceText.From("def x = 1;\n", "a.ls");
        var diagnostic = Diagnostic.Error("LAP9999", new SourceSpan(4, 1), "erro");

        var colored = DiagnosticRenderer.Render(diagnostic, source, color: true);

        colored.ShouldContain(Escape);
        colored.ShouldContain("error LAP9999");
    }

    /// <summary>Tirar a cor não pode mudar o texto — só as sequências de escape.</summary>
    [Fact]
    public void Renderer_ColorAndPlain_CarryTheSameText()
    {
        var source = SourceText.From("def x = 1;\n", "a.ls");
        var diagnostic = Diagnostic.Error("LAP9999", new SourceSpan(4, 1), "erro");

        var colored = DiagnosticRenderer.Render(diagnostic, source, color: true);
        var plain = DiagnosticRenderer.Render(diagnostic, source);

        StripAnsi(colored).ShouldBe(plain);
    }

    // ----------------------------------------------------------------- --source

    [Fact]
    public void DesugarSource_IsReparseable()
    {
        var (exit, stdout, _) = CliRunner.Run("desugar", "--source", CliRunner.ExamplePath("hello.ls"));

        exit.ShouldBe(ExitCodes.Success);

        var reparsed = Pipeline.Compile(SourceText.From(stdout, "impresso.ls"), PipelineStage.Desugar);

        reparsed.HasErrors.ShouldBeFalse(stdout);
    }

    [Fact]
    public void DesugarSource_DiffersFromSExpression()
    {
        var path = CliRunner.ExamplePath("hello.ls");

        CliRunner.Run("desugar", "--source", path).Stdout
            .ShouldNotBe(CliRunner.Run("desugar", path).Stdout);
    }

    /// <summary>Não há printer de fonte da Surface: pedir um é erro de uso, não silêncio.</summary>
    [Theory]
    [InlineData("ast")]
    [InlineData("run")]
    [InlineData("check")]
    [InlineData("tokens")]
    public void SourceFlag_OutsideDesugar_Returns64(string command)
    {
        var (exit, _, stderr) = CliRunner.Run(command, "--source", CliRunner.ExamplePath("hello.ls"));

        exit.ShouldBe(ExitCodes.Usage);
        stderr.ShouldContain("--source");
    }

    // --------------------------------------------------------------- posições

    /// <summary>As flags são globais: a posição no comando não pode importar.</summary>
    [Fact]
    public void Flags_AreAcceptedInAnyPosition()
    {
        var path = CliRunner.ExamplePath("hello.ls");

        CliRunner.Run("--source", "desugar", path).Stdout
            .ShouldBe(CliRunner.Run("desugar", path, "--source").Stdout);
    }

    private static string StripAnsi(string text)
    {
        var result = new System.Text.StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\u001b')
            {
                result.Append(text[i]);
                continue;
            }

            while (i < text.Length && text[i] != 'm')
            {
                i++;
            }
        }

        return result.ToString();
    }
}

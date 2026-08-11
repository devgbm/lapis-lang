using Lapis.Cli;
using Lapis.Diagnostics;

namespace Lapis.Cli.Tests;

/// <summary>
/// <c>lapis expand</c> (plano 17 §17.10) — a ferramenta sem a qual macros viram
/// adivinhação.
/// </summary>
public sealed class ExpandCommandTests
{
    private const string Log = "macro log match Expression:e expand { print(e); };\n";

    [Fact]
    public void Expand_PrintsTheSurfaceAstAfterExpansion()
    {
        var (exit, stdout, _) = CliRunner.RunSource(Log + "@log 1 + 2;", "expand");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("(name print)");
        stdout.ShouldNotContain("macro-call");
    }

    /// <summary>A declaração some: ela é sintaxe, não código.</summary>
    [Fact]
    public void Expand_DropsTheDeclaration() =>
        CliRunner.RunSource(Log + "@log 1;", "expand").Stdout.ShouldNotContain("(macro log");

    /// <summary>Sem macro nenhuma, `expand` e `ast` dão a mesma coisa.</summary>
    [Fact]
    public void Expand_WithoutMacros_MatchesAst()
    {
        const string Source = "def x = 1;\nprint(x);";

        CliRunner.RunSource(Source, "expand").Stdout.ShouldBe(CliRunner.RunSource(Source, "ast").Stdout);
    }

    [Fact]
    public void Expand_ReportsMacroErrors()
    {
        var (exit, _, stderr) = CliRunner.RunSource("@inexistente 1;", "expand");

        exit.ShouldBe(ExitCodes.CompilationError);
        stderr.ShouldContain(DiagnosticCodes.UnknownMacro);
    }

    [Fact]
    public void Expand_Example() =>
        CliRunner.Run("expand", CliRunner.ExamplePath("macros.ls")).Exit.ShouldBe(ExitCodes.Success);
}

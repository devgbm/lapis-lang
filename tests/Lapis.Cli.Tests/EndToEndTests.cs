using Lapis.Cli;
using Lapis.Diagnostics;

namespace Lapis.Cli.Tests;

/// <summary>
/// A cadeia completa através do executável: lexer → parser → desugar →
/// type checker → evaluator (spec §51).
/// </summary>
public sealed class EndToEndTests
{
    /// <summary>O marco do M1 (spec §60).</summary>
    [Fact]
    public void HelloExample_Prints30()
    {
        var (exit, stdout, stderr) = CliRunner.Run(CliRunner.ExamplePath("hello.ls"));

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldBe("30\n");
        stderr.ShouldBeEmpty();
    }

    [Fact]
    public void FunctionsExample_Runs()
    {
        var (exit, stdout, _) = CliRunner.Run(CliRunner.ExamplePath("functions.ls"));

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldBe("6\n50\n5\n");
    }

    [Fact]
    public void BareFile_IsEquivalentToRun()
    {
        var path = CliRunner.ExamplePath("hello.ls");

        CliRunner.Run(path).Stdout.ShouldBe(CliRunner.Run("run", path).Stdout);
    }

    [Fact]
    public void EmptyProgram_Succeeds()
    {
        var (exit, stdout, _) = CliRunner.RunSource(string.Empty);

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldBeEmpty();
    }

    /// <summary>Spec §34: a saída vem só de <c>print</c>; o valor final não é impresso.</summary>
    [Fact]
    public void ProgramValue_IsNotPrinted() =>
        CliRunner.RunSource("def x = 42;").Stdout.ShouldBeEmpty();

    [Fact]
    public void SyntaxError_Returns65_WithPosition()
    {
        var (exit, stdout, stderr) = CliRunner.RunSource("def x = ;");

        exit.ShouldBe(ExitCodes.CompilationError);
        stdout.ShouldBeEmpty();
        stderr.ShouldContain("error LAP");
        stderr.ShouldContain("(1,");
    }

    [Fact]
    public void TypeError_Returns65()
    {
        var (exit, _, stderr) = CliRunner.RunSource("def x: Str = 1;");

        exit.ShouldBe(ExitCodes.CompilationError);
        stderr.ShouldContain(DiagnosticCodes.TypeMismatch);
    }

    [Fact]
    public void TypeError_DoesNotRunTheProgram()
    {
        var (_, stdout, _) = CliRunner.RunSource("print(\"antes\"); def x: Str = 1;");

        stdout.ShouldBeEmpty();
    }

    [Fact]
    public void MultipleErrors_AreAllReported()
    {
        var (_, _, stderr) = CliRunner.RunSource("def a: Str = 1;\ndef b: Int = \"s\";");

        stderr.Split('\n').Count(l => l.Contains("error LAP")).ShouldBe(2);
    }

    [Fact]
    public void Warning_DoesNotFailTheRun()
    {
        var (exit, stdout, stderr) = CliRunner.RunSource("""
            def f = fn() Int {
                return 1;
                print(99);
            };

            print(f());
            """);

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldBe("1\n");
        stderr.ShouldContain("warning " + DiagnosticCodes.UnreachableAfterReturn);
    }

    /// <summary>Q9: divisão por zero não é mais um erro de execução.</summary>
    [Fact]
    public void DivisionByZero_Succeeds_WithMaxValue()
    {
        var (exit, stdout, stderr) = CliRunner.RunSource("print(1 / 0);");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldBe("9223372036854775807\n");
        stderr.ShouldBeEmpty();
    }

    [Fact]
    public void Utf8Output_IsPreserved() =>
        CliRunner.RunSource("print(\"acentuação\");").Stdout.ShouldBe("acentuação\n");
}

public sealed class SubcommandTests
{
    [Fact]
    public void Check_ValidFile_Succeeds_WithoutRunning()
    {
        var (exit, stdout, stderr) = CliRunner.Run("check", CliRunner.ExamplePath("hello.ls"));

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldBeEmpty();
        stderr.ShouldBeEmpty();
    }

    [Fact]
    public void Check_InvalidFile_Returns65() =>
        CliRunner.RunSource("def x: Str = 1;", "check").Exit.ShouldBe(ExitCodes.CompilationError);

    [Fact]
    public void Tokens_PrintsOneTokenPerLine()
    {
        var (exit, stdout, _) = CliRunner.RunSource("def x = 1;", "tokens");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("DefKeyword");
        stdout.ShouldContain("IntegerLiteral");
        stdout.ShouldContain("EndOfFile");
    }

    [Fact]
    public void Ast_PrintsSurfaceTree()
    {
        var (exit, stdout, _) = CliRunner.RunSource("def x = 1;", "ast");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("(source-file");
        stdout.ShouldContain("(def x");
    }

    [Fact]
    public void Desugar_PrintsCoreTree()
    {
        var (exit, stdout, _) = CliRunner.RunSource("def x = 1;", "desugar");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("(let x");
    }

    [Fact]
    public void Ast_OnHelloExample_IsStable()
    {
        var first = CliRunner.Run("ast", CliRunner.ExamplePath("hello.ls")).Stdout;
        var second = CliRunner.Run("ast", CliRunner.ExamplePath("hello.ls")).Stdout;

        first.ShouldBe(second);
    }
}

public sealed class DiagnosticRenderingTests
{
    [Fact]
    public void Render_IncludesLineColumnCodeAndSnippet()
    {
        var (_, _, stderr) = CliRunner.RunSource("def x = 1;\ndef y: Str = 2;");

        stderr.ShouldContain("(2,14): error LAP0210");
        stderr.ShouldContain("def y: Str = 2;");
        stderr.ShouldContain("^");
    }

    [Fact]
    public void Render_CaretWidthMatchesSpan()
    {
        var (_, _, stderr) = CliRunner.RunSource("print(reslt);");

        // O identificador tem 5 caracteres.
        stderr.ShouldContain("^^^^^");
    }

    [Fact]
    public void Render_IncludesSuggestionNote()
    {
        var (_, _, stderr) = CliRunner.RunSource("def result = 1;\nprint(reslt);");

        stderr.ShouldContain("nota:");
        stderr.ShouldContain("result");
    }

    [Fact]
    public void Render_UsesRelativePath()
    {
        var (_, _, stderr) = CliRunner.Run("check", CliRunner.ExamplePath("hello.ls"));

        stderr.ShouldNotContain("/home/");
    }

    [Fact]
    public void Render_OrdersDiagnosticsByOffset()
    {
        var (_, _, stderr) = CliRunner.RunSource("def a: Str = 1;\ndef b: Int = \"s\";");

        var first = stderr.IndexOf("(1,", StringComparison.Ordinal);
        var second = stderr.IndexOf("(2,", StringComparison.Ordinal);

        first.ShouldBeLessThan(second);
    }
}

public sealed class PipelineTests
{
    [Fact]
    public void StopsAtFirstFailingStage()
    {
        var result = Pipeline.Compile(SourceText.From("def x = ;"));

        result.HasErrors.ShouldBeTrue();
        result.Core.ShouldBeNull();
        result.Typed.ShouldBeNull();
        result.Evaluation.ShouldBeNull();
    }

    [Fact]
    public void StopAfter_IsRespected()
    {
        var result = Pipeline.Compile(SourceText.From("def x = 1;"), PipelineStage.Parse);

        result.Surface.ShouldNotBeNull();
        result.Core.ShouldBeNull();
    }

    [Fact]
    public void CollectsAllDiagnosticsWithinAStage()
    {
        var result = Pipeline.Compile(SourceText.From("def a: Str = 1;\ndef b: Int = \"s\";"));

        result.Diagnostics.Length.ShouldBe(2);
    }

    [Fact]
    public void IsPure_AcrossRuns()
    {
        var source = SourceText.From("def x = 1;\nprint(x);");

        var first = Pipeline.Compile(source, PipelineStage.TypeCheck);
        var second = Pipeline.Compile(source, PipelineStage.TypeCheck);

        first.Diagnostics.Length.ShouldBe(second.Diagnostics.Length);
        Ast.Printing.CoreSExprPrinter.Print(first.Core!)
            .ShouldBe(Ast.Printing.CoreSExprPrinter.Print(second.Core!));
    }
}

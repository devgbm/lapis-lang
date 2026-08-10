using Lapis.Cli;

namespace Lapis.Conformance.Tests;

/// <summary>
/// O runner é código, e código sem teste é o lugar onde uma suíte inteira passa a
/// mentir em silêncio (plano 11 §"Testes necessários do próprio runner").
/// </summary>
public sealed class DirectiveParsingTests
{
    private static ConformanceCase Parse(string text) => ConformanceCase.Parse("caso.ls", text);

    [Fact]
    public void ParsesOutputDirective()
    {
        var testCase = Parse("""
            // expect: output
            // 30
            // ---
            print(30);
            """);

        testCase.ExpectedOutput.ShouldBe("30\n");
    }

    [Fact]
    public void ParsesMultiLineOutput()
    {
        var testCase = Parse("""
            // expect: output
            // um
            // dois
            // ---
            """);

        testCase.ExpectedOutput.ShouldBe("um\ndois\n");
    }

    /// <summary>Bloco vazio significa "nada foi impresso", não "não verifiquei".</summary>
    [Fact]
    public void ParsesEmptyOutput()
    {
        Parse("// expect: output\n// ---\n").ExpectedOutput.ShouldBe(string.Empty);
    }

    /// <summary>Uma linha em branco na saída é `//` sozinho.</summary>
    [Fact]
    public void PreservesBlankOutputLines()
    {
        Parse("// expect: output\n// a\n//\n// b\n// ---\n").ExpectedOutput.ShouldBe("a\n\nb\n");
    }

    [Fact]
    public void PreservesTrailingSpacesInOutput()
    {
        Parse("// expect: output\n// a  \n// ---\n").ExpectedOutput.ShouldBe("a  \n");
    }

    [Fact]
    public void ParsesErrorDirective_WithoutPosition()
    {
        var expected = Parse("// expect: error LAP0201\n").ExpectedDiagnostics.ShouldHaveSingleItem();

        expected.Severity.ShouldBe("error");
        expected.Code.ShouldBe("LAP0201");
        expected.Line.ShouldBeNull();
    }

    [Fact]
    public void ParsesErrorDirective_WithPosition()
    {
        var expected = Parse("// expect: error LAP0201 at 4:9\n").ExpectedDiagnostics.ShouldHaveSingleItem();

        expected.Line.ShouldBe(4);
        expected.Column.ShouldBe(9);
    }

    [Fact]
    public void ParsesWarningDirective()
    {
        Parse("// expect: warning LAP0273\n").ExpectedDiagnostics.ShouldHaveSingleItem()
            .Severity.ShouldBe("warning");
    }

    [Fact]
    public void ParsesExitDirective() => Parse("// expect: exit 65\n").ExpectedExitCode.ShouldBe(65);

    [Fact]
    public void ParsesAbortDirective() => Parse("// expect: abort\n").ExpectsAbort.ShouldBeTrue();

    [Fact]
    public void ParsesSkipDirective() =>
        Parse("// skip: ainda não implementado\n").SkipReason.ShouldBe("ainda não implementado");

    [Fact]
    public void UnknownDirective_Throws() =>
        Should.Throw<FormatException>(() => Parse("// expect: nonsense\n"));

    [Fact]
    public void CaseWithoutDirectives_HasNoExpectations() =>
        Parse("print(1);\n").HasExpectations.ShouldBeFalse();

    /// <summary>O cabeçalho é só o bloco do topo; comentários do corpo não são diretivas.</summary>
    [Fact]
    public void BodyComments_AreNotDirectives()
    {
        var testCase = Parse("""
            // expect: output
            // 1
            // ---
            print(1);
            // expect: error LAP9999
            """);

        testCase.ExpectedDiagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void NormalizesLineEndings() =>
        ConformanceCase.Parse("caso.ls", "// expect: output\r\n// 30\r\n// ---\r\n")
            .ExpectedOutput.ShouldBe("30\n");

    // ------------------------------------------------- exit code implícito

    [Fact]
    public void RequiredExitCode_DefaultsToSuccess() =>
        Parse("// expect: output\n// ---\n").RequiredExitCode.ShouldBe(ExitCodes.Success);

    [Fact]
    public void RequiredExitCode_FollowsFromError() =>
        Parse("// expect: error LAP0201\n").RequiredExitCode.ShouldBe(ExitCodes.CompilationError);

    [Fact]
    public void RequiredExitCode_FollowsFromAbort() =>
        Parse("// expect: abort\n").RequiredExitCode.ShouldBe(ExitCodes.RuntimeAbort);

    /// <summary>Um `expect: exit` explícito vence a dedução — warnings não são erro.</summary>
    [Fact]
    public void RequiredExitCode_ExplicitWins() =>
        Parse("// expect: error LAP0201\n// expect: exit 0\n").RequiredExitCode.ShouldBe(0);
}

public sealed class RunnerBehaviourTests
{
    private static ConformanceCase Parse(string text) => ConformanceCase.Parse("caso.ls", text);

    /// <summary>A divergência encontrada, ou string vazia se o caso passou.</summary>
    private static string Run(string text)
    {
        var testCase = Parse(text);
        return ConformanceRunner.Verify(testCase, ConformanceRunner.Execute(testCase)) ?? string.Empty;
    }

    [Fact]
    public void MatchingOutput_Passes() =>
        Run("// expect: output\n// 30\n// ---\nprint(30);\n").ShouldBeEmpty();

    [Fact]
    public void MismatchedOutput_Fails() =>
        Run("// expect: output\n// 31\n// ---\nprint(30);\n").ShouldContain("saída divergente");

    [Fact]
    public void ExpectedDiagnostic_Passes() =>
        Run("// expect: error LAP0201\ndef x = inexistente;\n").ShouldBeEmpty();

    [Fact]
    public void MissingDiagnostic_Fails() =>
        Run("// expect: error LAP0201\nprint(1);\n").ShouldContain("esperado e ausente");

    /// <summary>Silenciar o inesperado é como uma suíte deixa de notar regressão.</summary>
    [Fact]
    public void UnexpectedDiagnostic_Fails() =>
        Run("// expect: output\n// ---\ndef x = inexistente;\n").ShouldContain("inesperado");

    [Fact]
    public void DiagnosticPosition_IsChecked() =>
        Run("// expect: error LAP0201 at 99:99\ndef x = inexistente;\n")
            .ShouldContain("esperado e ausente");

    [Fact]
    public void ExitCodeMismatch_Fails() =>
        Run("// expect: exit 0\ndef x = inexistente;\n").ShouldContain("exit code divergente");

    [Fact]
    public void ReportsDiff_ShowingNewlines() =>
        Run("// expect: output\n// a\n// ---\nprint(\"b\");\n").ShouldContain("\\n");

    [Fact]
    public void IsolatedPrelude_Works() =>
        ConformanceRunner.Execute(
            Parse("// expect: output\n// 30\n// ---\nprint(30);\n"), freshPrelude: true)
            .Output.ShouldBe("30\n");
}

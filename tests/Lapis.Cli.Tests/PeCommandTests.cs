using Lapis.Cli;
using Lapis.Diagnostics;

namespace Lapis.Cli.Tests;

/// <summary><c>lapis pe</c> (plano 12 §12.8).</summary>
public sealed class PeCommandTests
{
    [Fact]
    public void Pe_PrintsTheResidual()
    {
        var (exit, stdout, _) = CliRunner.RunSource("print(10 + 20);", "pe");

        exit.ShouldBe(ExitCodes.Success);
        stdout.Trim().ShouldBe("print(30);");
    }

    /// <summary>O residual é código <c>.ls</c>: tem que rodar, e rodar igual.</summary>
    [Fact]
    public void Pe_ResidualRunsTheSame()
    {
        const string Source = "def x = 10;\ndef y = 20;\nprint(x + y);\nprint(\"fim\");";

        var (_, residual, _) = CliRunner.RunSource(Source, "pe");
        var (_, original, _) = CliRunner.RunSource(Source, "run");

        CliRunner.RunSource(residual, "run").Stdout.ShouldBe(original);
    }

    [Fact]
    public void Pe_ExampleIsReadable()
    {
        var (exit, stdout, _) = CliRunner.Run("pe", CliRunner.ExamplePath("hello.ls"));

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("def add");
        CliRunner.RunSource(stdout, "run").Stdout.ShouldBe("30\n");
    }

    [Fact]
    public void Pe_CompilationError_Exit65()
    {
        var (exit, stdout, stderr) = CliRunner.RunSource("def x = inexistente;\n", "pe");

        exit.ShouldBe(ExitCodes.CompilationError);
        stdout.ShouldBeEmpty();
        stderr.ShouldContain(DiagnosticCodes.UnknownVariable);
    }

    /// <summary>
    /// Sem entrada externa na v0.2, todo top-level é estático e um programa fechado
    /// tende ao resultado já avaliado. <c>--dynamic</c> é o que torna o comando
    /// interessante: ele declara um nome como desconhecido e obriga o PE a
    /// residualizar de verdade.
    /// </summary>
    [Fact]
    public void Dynamic_KeepsTheNameUnknown()
    {
        const string Source = "def n = 2;\nprint(n * 3);";

        CliRunner.RunSource(Source, "pe").Stdout.Trim().ShouldBe("print(6);");

        var (exit, stdout, _) = CliRunner.RunSource(Source, "pe", "--dynamic=n");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("n * 3");
    }

    [Fact]
    public void Dynamic_AcceptsATypeAnnotation() =>
        CliRunner.RunSource("def n = 2;\nprint(n * 3);", "pe", "--dynamic=n:Int")
            .Stdout.ShouldContain("n * 3");

    [Fact]
    public void Dynamic_AcceptsSeveralNames() =>
        CliRunner.RunSource("def a = 1;\ndef b = 2;\nprint(a + b);", "pe", "--dynamic=a,b")
            .Stdout.ShouldContain("a + b");

    [Fact]
    public void Dynamic_OutsidePe_Returns64()
    {
        var (exit, _, stderr) = CliRunner.RunSource("print(1);", "run", "--dynamic=x");

        exit.ShouldBe(ExitCodes.Usage);
        stderr.ShouldContain("--dynamic");
    }

    [Fact]
    public void Stats_GoToStderr()
    {
        var (_, stdout, stderr) = CliRunner.RunSource("print(10 + 20);", "pe", "--stats");

        stdout.ShouldNotContain("dobras");
        stderr.ShouldContain("dobras: 1");
    }
}

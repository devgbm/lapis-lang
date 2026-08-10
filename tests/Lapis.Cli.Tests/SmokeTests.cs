using Lapis.Cli;

namespace Lapis.Cli.Tests;

/// <summary>Uso do CLI e códigos de saída (plano 10 §10.5).</summary>
public sealed class SmokeTests
{
    [Fact]
    public void NoArgs_PrintsUsage_AndReturns64()
    {
        var (exit, stdout, _) = CliRunner.Run();

        exit.ShouldBe(ExitCodes.Usage);
        stdout.ShouldContain("usage: lapis");
    }

    [Fact]
    public void Help_PrintsUsage_AndReturns0()
    {
        var (exit, stdout, _) = CliRunner.Run("--help");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("usage: lapis");
    }

    [Fact]
    public void Version_PrintsSpecVersion()
    {
        var (exit, stdout, _) = CliRunner.Run("--version");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("0.2");
    }

    [Fact]
    public void MissingFile_Returns64_WithoutStackTrace()
    {
        var (exit, stdout, stderr) = CliRunner.Run("nao-existe.ls");

        exit.ShouldBe(ExitCodes.Usage);
        stdout.ShouldBeEmpty();
        stderr.ShouldContain("não encontrado");
        stderr.ShouldNotContain("   at ");
    }

    [Fact]
    public void WrongExtension_Returns64()
    {
        var (exit, _, stderr) = CliRunner.Run("programa.txt");

        exit.ShouldBe(ExitCodes.Usage);
        stderr.ShouldContain(".ls");
    }

    [Fact]
    public void UnknownOption_Returns64()
    {
        var (exit, _, stderr) = CliRunner.Run("--nao-existe");

        exit.ShouldBe(ExitCodes.Usage);
        stderr.ShouldContain("desconhecida");
    }

    [Fact]
    public void TooManyArguments_Returns64() =>
        CliRunner.Run("a.ls", "b.ls", "c.ls").Exit.ShouldBe(ExitCodes.Usage);
}

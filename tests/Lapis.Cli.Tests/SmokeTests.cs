using Lapis.Cli;

namespace Lapis.Cli.Tests;

/// <summary>
/// Testes de fumaça do M0 (plans/01-solution-skeleton.md §"Testes de fumaça").
/// O caso <c>KnownFile_Returns70_NotImplemented</c> é um placeholder e deve ser
/// removido quando o M1 ligar o pipeline.
/// </summary>
public sealed class SmokeTests
{
    [Fact]
    public void NoArgs_PrintsUsage_AndReturns64()
    {
        var (exit, stdout, _) = RunCli();

        exit.ShouldBe(ExitCodes.Usage);
        stdout.ShouldContain("usage: lapis");
    }

    [Fact]
    public void Help_PrintsUsage_AndReturns0()
    {
        var (exit, stdout, _) = RunCli("--help");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("usage: lapis");
    }

    [Fact]
    public void Version_PrintsSpecVersion()
    {
        var (exit, stdout, _) = RunCli("--version");

        exit.ShouldBe(ExitCodes.Success);
        stdout.ShouldContain("0.2");
    }

    [Fact]
    public void MissingFile_Returns64_WithoutStackTrace()
    {
        var (exit, stdout, stderr) = RunCli("nao-existe.ls");

        exit.ShouldBe(ExitCodes.Usage);
        stdout.ShouldBeEmpty();
        stderr.ShouldContain("não encontrado");
        stderr.ShouldNotContain("   at ");
    }

    [Fact]
    public void WrongExtension_Returns64()
    {
        var (exit, _, stderr) = RunCli("programa.txt");

        exit.ShouldBe(ExitCodes.Usage);
        stderr.ShouldContain(".ls");
    }

    [Fact]
    public void UnknownOption_Returns64()
    {
        var (exit, _, stderr) = RunCli("--nao-existe");

        exit.ShouldBe(ExitCodes.Usage);
        stderr.ShouldContain("desconhecida");
    }

    [Fact]
    public void MultipleFiles_Returns64()
    {
        var (exit, _, stderr) = RunCli("a.ls", "b.ls");

        exit.ShouldBe(ExitCodes.Usage);
        stderr.ShouldContain("exatamente um arquivo");
    }

    /// <summary>Placeholder do M0: remover quando o pipeline for ligado no M1.</summary>
    [Fact]
    public void KnownFile_Returns70_NotImplemented()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lapis-smoke-{Guid.NewGuid():N}.ls");
        File.WriteAllText(path, "def x = 1;\n");

        try
        {
            var (exit, _, stderr) = RunCli(path);

            exit.ShouldBe(ExitCodes.InternalError);
            stderr.ShouldContain("não implementado");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (int Exit, string Stdout, string Stderr) RunCli(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = Program.Run(args, stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }
}

using Lapis.Cli;

namespace Lapis.Cli.Tests;

public static class CliRunner
{
    public static (int Exit, string Stdout, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = Program.Run(args, stdout, stderr);

        return (exit, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Escreve <paramref name="source"/> num arquivo temporário e roda o CLI sobre ele.</summary>
    public static (int Exit, string Stdout, string Stderr) RunSource(string source, params string[] leadingArgs)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lapis-{Guid.NewGuid():N}.ls");
        File.WriteAllText(path, source);

        try
        {
            return Run([.. leadingArgs, path]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static string ExamplePath(string fileName) =>
        Path.Combine(RepoLayout.Root, "examples", fileName);
}

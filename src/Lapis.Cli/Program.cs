using System.Diagnostics.CodeAnalysis;

namespace Lapis.Cli;

/// <summary>
/// Ponto de entrada do executável <c>lapis</c>.
///
/// Esta é a versão do M0 (plans/01-solution-skeleton.md): valida os argumentos e
/// devolve os códigos de saída definitivos, mas ainda não executa o pipeline —
/// quem o liga é o M1 (plans/10-cli.md).
/// </summary>
public static class Program
{
    private const string SpecVersion = "0.2";

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>
    /// Núcleo testável do CLI: recebe os writers em vez de usar <see cref="Console"/>
    /// diretamente, para que os testes possam capturar a saída.
    /// </summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            WriteUsage(stdout);
            return ExitCodes.Usage;
        }

        if (args is ["--version"] or ["-v"])
        {
            stdout.WriteLine($"lapis {ThisAssemblyVersion} (LapisLang spec {SpecVersion})");
            return ExitCodes.Success;
        }

        if (args is ["--help"] or ["-h"])
        {
            WriteUsage(stdout);
            return ExitCodes.Success;
        }

        if (!TryResolveSourceFile(args, stderr, out var path))
        {
            return ExitCodes.Usage;
        }

        // M1 liga o pipeline aqui (Lexer → Parser → Desugar → TypeChecker → Evaluator).
        stderr.WriteLine($"lapis: pipeline ainda não implementado (M0); não foi possível executar '{path}'.");
        return ExitCodes.InternalError;
    }

    private static bool TryResolveSourceFile(string[] args, TextWriter stderr, [NotNullWhen(true)] out string? path)
    {
        path = null;

        if (args.Length != 1)
        {
            stderr.WriteLine("lapis: esperado exatamente um arquivo de entrada.");
            return false;
        }

        var candidate = args[0];

        if (candidate.StartsWith('-'))
        {
            stderr.WriteLine($"lapis: opção desconhecida '{candidate}'.");
            return false;
        }

        if (!candidate.EndsWith(".ls", StringComparison.Ordinal))
        {
            stderr.WriteLine($"lapis: esperado um arquivo '.ls', recebido '{candidate}'.");
            return false;
        }

        if (!File.Exists(candidate))
        {
            stderr.WriteLine($"lapis: arquivo não encontrado: '{candidate}'.");
            return false;
        }

        path = candidate;
        return true;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("usage: lapis <arquivo>.ls");
        writer.WriteLine();
        writer.WriteLine("Executa um programa LapisLang.");
        writer.WriteLine();
        writer.WriteLine("Opções:");
        writer.WriteLine("  -h, --help       mostra esta ajuda");
        writer.WriteLine("  -v, --version    mostra a versão");
    }

    private static string ThisAssemblyVersion =>
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Lapis.Ast.Printing;
using Lapis.Diagnostics;
using Lapis.Evaluator;
using Lapis.Runtime;

namespace Lapis.Cli;

/// <summary>
/// Ponto de entrada do executável <c>lapis</c> (spec §33).
///
/// A forma mínima obrigatória é <c>lapis programa.ls</c>; os subcomandos de
/// pesquisa da spec §56 (<c>ast</c>, <c>desugar</c>, <c>tokens</c>, <c>check</c>)
/// vêm junto porque compartilham o mesmo orquestrador.
/// </summary>
public static class Program
{
    private const string SpecVersion = "0.2";

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>Núcleo testável: recebe os writers em vez de usar <see cref="Console"/>.</summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            return Dispatch(args, stdout, stderr);
        }
        catch (InternalCompilerException exception)
        {
            stderr.WriteLine("lapis: erro interno do compilador — isto é um bug.");
            stderr.WriteLine(exception.ToString());
            return ExitCodes.InternalError;
        }
    }

    private static int Dispatch(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            WriteUsage(stdout);
            return ExitCodes.Usage;
        }

        switch (args[0])
        {
            case "--help" or "-h":
                WriteUsage(stdout);
                return ExitCodes.Success;

            case "--version" or "-v":
                stdout.WriteLine($"lapis {AssemblyVersion} (LapisLang spec {SpecVersion})");
                return ExitCodes.Success;
        }

        var (command, path) = args switch
        {
            ["run" or "check" or "tokens" or "ast" or "desugar", var file] => (args[0], file),
            [var file] => ("run", file),
            _ => (null, null),
        };

        if (command is null || path is null)
        {
            stderr.WriteLine("lapis: argumentos inválidos.");
            WriteUsage(stderr);
            return ExitCodes.Usage;
        }

        if (!TryReadSource(path, stderr, out var source))
        {
            return ExitCodes.Usage;
        }

        return command switch
        {
            "run" => RunProgram(source, stdout, stderr),
            "check" => CheckProgram(source, stderr),
            "tokens" => PrintStage(source, PipelineStage.Tokens, stdout, stderr),
            "ast" => PrintStage(source, PipelineStage.Parse, stdout, stderr),
            "desugar" => PrintStage(source, PipelineStage.Desugar, stdout, stderr),
            _ => ExitCodes.Usage,
        };
    }

    // ------------------------------------------------------------- comandos

    private static int RunProgram(SourceText source, TextWriter stdout, TextWriter stderr)
    {
        var context = new RuntimeContext(new ConsoleOutput(stdout));
        var result = Pipeline.Compile(source, PipelineStage.Evaluate, context);

        ReportDiagnostics(result, source, stderr);

        if (result.HasErrors)
        {
            return ExitCodes.CompilationError;
        }

        var evaluation = result.Evaluation;

        if (evaluation is { Status: ExecutionStatus.Aborted })
        {
            stderr.WriteLine(DiagnosticRenderer.Render(
                Diagnostic.Error(
                    evaluation.Code ?? "LAP0300",
                    evaluation.Span ?? SourceSpan.Synthetic,
                    evaluation.Message ?? "execução abortada"),
                source));

            return ExitCodes.RuntimeAbort;
        }

        // O valor final do programa não é impresso: a saída vem só de `print`
        // (spec §34 — a saída de hello.ls é apenas `30`).
        return ExitCodes.Success;
    }

    private static int CheckProgram(SourceText source, TextWriter stderr)
    {
        var result = Pipeline.Compile(source, PipelineStage.TypeCheck);

        ReportDiagnostics(result, source, stderr);

        return result.HasErrors ? ExitCodes.CompilationError : ExitCodes.Success;
    }

    private static int PrintStage(SourceText source, PipelineStage stage, TextWriter stdout, TextWriter stderr)
    {
        var result = Pipeline.Compile(source, stage);

        ReportDiagnostics(result, source, stderr);

        if (result.HasErrors)
        {
            return ExitCodes.CompilationError;
        }

        switch (stage)
        {
            case PipelineStage.Tokens:
                foreach (var token in result.Tokens ?? [])
                {
                    var position = source.GetPosition(Math.Min(token.Span.Start, source.Length));
                    stdout.WriteLine($"{position.Line,4}:{position.Column,-4} {token.Kind,-18} {token.Text}");
                }

                break;

            case PipelineStage.Parse:
                stdout.WriteLine(SurfaceSExprPrinter.Print(result.Surface!));
                break;

            case PipelineStage.Desugar:
                stdout.WriteLine(CoreSExprPrinter.Print(result.Core!));
                break;
        }

        return ExitCodes.Success;
    }

    // ------------------------------------------------------------ auxiliar

    private static void ReportDiagnostics(CompilationResult result, SourceText source, TextWriter stderr)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            stderr.Write(DiagnosticRenderer.Render(diagnostic, source));
        }
    }

    private static bool TryReadSource(string path, TextWriter stderr, [NotNullWhen(true)] out SourceText? source)
    {
        source = null;

        if (path.StartsWith('-'))
        {
            stderr.WriteLine($"lapis: opção desconhecida '{path}'.");
            return false;
        }

        if (!path.EndsWith(".ls", StringComparison.Ordinal))
        {
            stderr.WriteLine($"lapis: esperado um arquivo '.ls', recebido '{path}'.");
            return false;
        }

        if (!File.Exists(path))
        {
            stderr.WriteLine($"lapis: arquivo não encontrado: '{path}'.");
            return false;
        }

        // Caminho relativo nos diagnósticos: não vaza a estrutura da máquina.
        var display = Path.IsPathRooted(path)
            ? Path.GetRelativePath(Directory.GetCurrentDirectory(), path)
            : path;

        source = SourceText.From(File.ReadAllText(path), display);
        return true;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("usage: lapis <arquivo>.ls");
        writer.WriteLine();
        writer.WriteLine("Comandos:");
        writer.WriteLine("  run <arquivo>.ls       executa o programa (padrão)");
        writer.WriteLine("  check <arquivo>.ls     apenas compila e reporta diagnósticos");
        writer.WriteLine("  tokens <arquivo>.ls    imprime os tokens");
        writer.WriteLine("  ast <arquivo>.ls       imprime a Surface AST");
        writer.WriteLine("  desugar <arquivo>.ls   imprime a Core AST");
        writer.WriteLine();
        writer.WriteLine("Opções:");
        writer.WriteLine("  -h, --help       mostra esta ajuda");
        writer.WriteLine("  -v, --version    mostra a versão");
    }

    private static string AssemblyVersion =>
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Lapis.Ast.Printing;
using Lapis.Diagnostics;
using Lapis.Evaluator;
using Lapis.PartialEvaluator;
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

        if (!TryParseOptions(args, stderr, out var options))
        {
            return ExitCodes.Usage;
        }

        var (command, path) = options.Positional switch
        {
            ["run" or "check" or "tokens" or "ast" or "expand" or "desugar" or "pe", var file] => (options.Positional[0], file),
            [var file] => ("run", file),
            _ => (null, null),
        };

        if (command is null || path is null)
        {
            stderr.WriteLine("lapis: argumentos inválidos.");
            WriteUsage(stderr);
            return ExitCodes.Usage;
        }

        // `--source` só existe onde há um printer de fonte: a Core AST (plano 02
        // §2.8). Não há printer de fonte da Surface, e fingir que há esconderia a
        // diferença entre as duas árvores.
        if (options.Source && command != "desugar")
        {
            stderr.WriteLine("lapis: '--source' só se aplica a 'desugar'.");
            return ExitCodes.Usage;
        }

        if (options.Dynamic.Count > 0 && command != "pe")
        {
            stderr.WriteLine("lapis: '--dynamic' só se aplica a 'pe'.");
            return ExitCodes.Usage;
        }

        if (!TryReadSource(path, stderr, out var source))
        {
            return ExitCodes.Usage;
        }

        return command switch
        {
            "run" => RunProgram(source, options, stdout, stderr),
            "check" => CheckProgram(source, options, stderr),
            "tokens" => PrintStage(source, PipelineStage.Tokens, options, stdout, stderr),
            "ast" => PrintStage(source, PipelineStage.Parse, options, stdout, stderr),
            "expand" => PrintStage(source, PipelineStage.Expand, options, stdout, stderr),
            "desugar" => PrintStage(source, PipelineStage.Desugar, options, stdout, stderr),
            "pe" => PartiallyEvaluate(source, options, stdout, stderr),
            _ => ExitCodes.Usage,
        };
    }

    /// <summary>
    /// Flags globais, aceitas em qualquer posição. São poucas e ortogonais aos
    /// comandos, então um laço explícito custa menos que uma dependência de
    /// parsing de linha de comando.
    /// </summary>
    private static bool TryParseOptions(string[] args, TextWriter stderr, out CliOptions options)
    {
        var positional = new List<string>();
        var json = false;
        var noColor = false;
        var sourceForm = false;
        var stats = false;
        var dynamic = new List<string>();

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--json": json = true; break;
                case "--no-color": noColor = true; break;
                case "--source": sourceForm = true; break;
                case "--stats": stats = true; break;

                default:
                    if (arg.StartsWith("--dynamic=", StringComparison.Ordinal))
                    {
                        dynamic.AddRange(arg["--dynamic=".Length..]
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        break;
                    }

                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        stderr.WriteLine($"lapis: opção desconhecida '{arg}'.");
                        options = default;
                        return false;
                    }

                    positional.Add(arg);
                    break;
            }
        }

        var color = ShouldUseColor(
            noColor,
            Console.IsErrorRedirected,
            System.Environment.GetEnvironmentVariable("NO_COLOR"));

        options = new CliOptions(positional.ToArray(), json, color && !json, sourceForm, stats, dynamic);
        return true;
    }

    /// <summary>
    /// Cor só quando alguém vai olhar: terminal de verdade, sem <c>--no-color</c>
    /// e sem <c>NO_COLOR</c> (a convenção de no-color.org).
    ///
    /// Recebe o ambiente por parâmetro em vez de consultá-lo: é o que torna a
    /// decisão testável sem mexer em variáveis do processo de teste.
    /// </summary>
    public static bool ShouldUseColor(bool noColor, bool errorRedirected, string? noColorVariable) =>
        !noColor && !errorRedirected && string.IsNullOrEmpty(noColorVariable);

    private readonly record struct CliOptions(
        string[] Positional,
        bool Json,
        bool Color,
        bool Source,
        bool Stats,
        IReadOnlyList<string> Dynamic);

    // ------------------------------------------------------------- comandos

    private static int RunProgram(SourceText source, CliOptions options, TextWriter stdout, TextWriter stderr)
    {
        var context = new RuntimeContext(new ConsoleOutput(stdout));

        // O que uma constraint imprime é saída do compilador, e vai para onde os
        // diagnósticos vão: stdout é do programa (plano 18 §18.1).
        var result = Pipeline.Compile(
            source,
            PipelineStage.Evaluate,
            context,
            compileTimeOutput: new ConsoleOutput(stderr));

        ReportDiagnostics(result.Diagnostics, source, options, stderr);

        if (result.Evaluation is { Status: ExecutionStatus.Aborted } aborted)
        {
            ReportDiagnostics(
                [
                    Diagnostic.Error(
                        aborted.Code ?? "LAP0300",
                        aborted.Span ?? SourceSpan.Synthetic,
                        aborted.Message ?? "execução abortada"),
                ],
                source,
                options,
                stderr);
        }

        // O valor final do programa não é impresso: a saída vem só de `print`
        // (spec §34 — a saída de hello.ls é apenas `30`).
        return ExitCodes.For(result);
    }

    private static int CheckProgram(SourceText source, CliOptions options, TextWriter stderr)
    {
        var result = Pipeline.Compile(source, PipelineStage.TypeCheck);

        ReportDiagnostics(result.Diagnostics, source, options, stderr);

        return ExitCodes.For(result);
    }

    /// <summary>
    /// <c>lapis pe</c> — o programa residual, impresso como código <c>.ls</c>.
    ///
    /// Ambiente estático inicial vazio: em v0.2 não há entrada externa, então todo
    /// top-level é estático por construção e um programa fechado tende ao resultado
    /// já avaliado. É <c>--dynamic</c> que torna o comando interessante — ele
    /// declara nomes como desconhecidos e obriga o PE a residualizar de verdade.
    /// </summary>
    private static int PartiallyEvaluate(
        SourceText source,
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr)
    {
        var result = Pipeline.Compile(source, PipelineStage.TypeCheck);

        ReportDiagnostics(result.Diagnostics, source, options, stderr);

        if (result.HasErrors)
        {
            return ExitCodes.CompilationError;
        }

        var environment = StaticEnvironment.Root();

        foreach (var name in options.Dynamic)
        {
            // `nome` ou `nome:Tipo`. O tipo é informativo nesta fatia — o residual
            // é re-checado do zero —, então uma anotação ausente não impede nada.
            var separator = name.IndexOf(':', StringComparison.Ordinal);
            var identifier = separator < 0 ? name : name[..separator];

            environment.DeclareDynamic(identifier, Ast.Types.AnyType.Instance);
        }

        var specialized = PartialEvaluator.PartialEvaluator.Specialize(
            result.Core!, environment, options: null, result.Typed);

        stdout.Write(CoreSourcePrinter.Print(specialized.Residual));

        if (options.Stats)
        {
            var statistics = specialized.Statistics;
            stderr.WriteLine(
                $"nós: {statistics.NodesBefore} → {statistics.NodesAfter}; "
                + $"dobras: {statistics.ConstantsFolded}; "
                + $"ramos eliminados: {statistics.BranchesEliminated}; "
                + $"bindings eliminados: {statistics.BindingsEliminated}");
        }

        return ExitCodes.Success;
    }

    private static int PrintStage(
        SourceText source,
        PipelineStage stage,
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr)
    {
        var result = Pipeline.Compile(source, stage);

        ReportDiagnostics(result.Diagnostics, source, options, stderr);

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
            case PipelineStage.Expand:
                stdout.WriteLine(SurfaceSExprPrinter.Print(result.Surface!));
                break;

            case PipelineStage.Desugar:
                stdout.Write(options.Source
                    ? CoreSourcePrinter.Print(result.Core!)
                    : CoreSExprPrinter.Print(result.Core!) + System.Environment.NewLine);
                break;
        }

        return ExitCodes.Success;
    }

    // ------------------------------------------------------------ auxiliar

    private static void ReportDiagnostics(
        IEnumerable<Diagnostic> diagnostics,
        SourceText source,
        CliOptions options,
        TextWriter stderr)
    {
        foreach (var diagnostic in diagnostics)
        {
            stderr.Write(options.Json
                ? DiagnosticJson.Render(diagnostic, source) + System.Environment.NewLine
                : DiagnosticRenderer.Render(diagnostic, source, options.Color));
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
        writer.WriteLine("  expand <arquivo>.ls    imprime a Surface AST depois das macros");
        writer.WriteLine("  desugar <arquivo>.ls   imprime a Core AST");
        writer.WriteLine("  pe <arquivo>.ls        imprime o programa residual (partial evaluation)");
        writer.WriteLine();
        writer.WriteLine("Opções:");
        writer.WriteLine("  --json           diagnósticos em JSON, um por linha");
        writer.WriteLine("  --no-color       desliga ANSI (idem NO_COLOR no ambiente)");
        writer.WriteLine("  --source         em 'desugar', imprime '.ls' em vez de S-expression");
        writer.WriteLine("  --dynamic=x,y    em 'pe', trata estes nomes top-level como desconhecidos");
        writer.WriteLine("  --stats          em 'pe', imprime as estatísticas em stderr");
        writer.WriteLine("  -h, --help       mostra esta ajuda");
        writer.WriteLine("  -v, --version    mostra a versão");
    }

    private static string AssemblyVersion =>
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}

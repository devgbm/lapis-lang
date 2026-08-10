using System.Collections.Immutable;
using Lapis.Ast.Core;
using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.Cli;

/// <summary>
/// Roda o pipeline completo sobre o <c>prelude.ls</c> embutido e colhe os nomes
/// que ele exporta.
///
/// Mora no orquestrador, e não em <c>Lapis.Runtime</c>, porque carregar o prelude
/// exige lexer, parser, desugar, checker e evaluator — colocá-lo no runtime
/// fecharia o ciclo <c>Runtime → Evaluator → TypeChecker → Runtime</c>
/// (plano 09 §9.3).
///
/// Se o prelude falhar em qualquer fase, é bug do compilador, nunca do usuário:
/// <see cref="InternalCompilerException"/>.
/// </summary>
public static class PreludeLoader
{
    private const string ResourceName = "Lapis.Runtime.prelude.ls";

    private static readonly Lazy<PreludeScope> Cached = new(LoadFresh, isThreadSafe: true);

    /// <summary>Carga com cache: o resultado é imutável e reutilizável entre programas.</summary>
    public static PreludeScope Load() => Cached.Value;

    /// <summary>Carga isolada, para testes que precisam de instâncias independentes.</summary>
    public static PreludeScope LoadFresh()
    {
        var source = SourceText.From(ReadEmbeddedSource(), "<prelude>");
        var diagnostics = new DiagnosticBag();

        var file = Parser.Parser.Parse(source, diagnostics);
        var core = Desugar.Desugarer.Desugar(file, diagnostics);
        var typed = TypeChecker.TypeChecker.Check(core, prelude: null, diagnostics);

        if (diagnostics.HasErrors)
        {
            var messages = string.Join("; ", diagnostics.Select(d => $"{d.Code} {d.Message}"));
            throw new InternalCompilerException($"o prelude não compila: {messages}");
        }

        var context = new RuntimeContext(new StringOutput());
        var evaluation = Evaluator.Evaluator.Run(typed, prelude: null, context);

        if (evaluation.Status != Evaluator.ExecutionStatus.Completed)
        {
            throw new InternalCompilerException($"o prelude abortou: {evaluation.Message}");
        }

        return new PreludeScope(CollectBindings(typed, context));
    }

    /// <summary>
    /// Reexecuta a cadeia de <c>Let</c> do topo para colher nome, tipo e valor de
    /// cada definição. É possível porque o programa inteiro é uma única expressão
    /// e o topo é uma cadeia de <c>Let</c> (plano 05 §5.2).
    /// </summary>
    private static ImmutableArray<PreludeBinding> CollectBindings(
        Ast.Typed.TypedProgram typed,
        RuntimeContext context)
    {
        var bindings = ImmutableArray.CreateBuilder<PreludeBinding>();
        var current = typed.Program.Body;

        while (current is CoreLet let)
        {
            if (!let.IsSynthetic)
            {
                var type = typed.TypeOf(let.Value);
                var value = EvaluateStandalone(typed, let.Value, context);
                bindings.Add(new PreludeBinding(let.Name, type, value));
            }

            current = let.Body;
        }

        return bindings.ToImmutable();
    }

    /// <summary>
    /// Avalia uma definição isolada do prelude. Funciona porque as declarações do
    /// prelude são fechadas — não referenciam nada além de si mesmas.
    /// </summary>
    private static Value EvaluateStandalone(
        Ast.Typed.TypedProgram typed,
        CoreExpr expression,
        RuntimeContext context)
    {
        var wrapper = new Ast.Typed.TypedProgram(
            new CoreProgram(expression, typed.Program.NodeCount),
            typed.NodeTypes,
            typed.Resolutions);

        var result = Evaluator.Evaluator.Run(wrapper, prelude: null, context);

        return result.Status == Evaluator.ExecutionStatus.Completed
            ? result.Value
            : throw new InternalCompilerException("uma definição do prelude não avaliou");
    }

    private static string ReadEmbeddedSource()
    {
        var assembly = typeof(PreludeScope).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InternalCompilerException($"recurso '{ResourceName}' não encontrado");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

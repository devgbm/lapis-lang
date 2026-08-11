using System.Collections.Immutable;
using Lapis.Ast.Core;
using Lapis.Ast.Surface;
using Lapis.Ast.Typed;
using Lapis.Diagnostics;
using Lapis.Evaluator;
using Lapis.Lexer;
using Lapis.Runtime;

namespace Lapis.Cli;

public enum PipelineStage
{
    Tokens,
    Parse,

    /// <summary>Surface AST depois da expansão de macros (plano 17).</summary>
    Expand,

    Desugar,
    TypeCheck,
    Evaluate,
}

public sealed record CompilationResult(
    ImmutableArray<Token>? Tokens,
    SourceFile? Surface,
    CoreProgram? Core,
    TypedProgram? Typed,
    EvaluationResult? Evaluation,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}

/// <summary>
/// O único lugar onde as fases são compostas (plano 10 §10.1).
///
/// Para na primeira fase que acumular erros; warnings não param nada. Os testes
/// de conformidade usam este mesmo tipo, de modo que testam o compilador de
/// verdade — não uma reimplementação do pipeline.
/// </summary>
public static class Pipeline
{
    /// <param name="prelude">
    /// Prelude a usar. O padrão é o compartilhado — carregá-lo é caro e o
    /// resultado é imutável. A suíte de conformidade passa um fresco em um teste,
    /// para provar que nenhum estado atravessa compilações (plano 11 §11.2).
    /// </param>
    public static CompilationResult Compile(
        SourceText source,
        PipelineStage stopAfter = PipelineStage.Evaluate,
        RuntimeContext? context = null,
        PreludeScope? prelude = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var diagnostics = new DiagnosticBag();

        var tokens = Lexer.Lexer.Tokenize(source, diagnostics);

        if (stopAfter == PipelineStage.Tokens || diagnostics.HasErrors)
        {
            return Result(diagnostics, tokens);
        }

        var surface = Parser.Parser.Parse(tokens, diagnostics);

        if (stopAfter == PipelineStage.Parse || diagnostics.HasErrors)
        {
            return Result(diagnostics, tokens, surface);
        }

        // Macros ficam entre o parser e o desugar, operando sobre a Surface AST.
        // Um arquivo sem `@` sai idêntico daqui (spec de macros §1).
        surface = Macros.MacroExpander.Expand(surface, diagnostics);

        if (stopAfter == PipelineStage.Expand || diagnostics.HasErrors)
        {
            return Result(diagnostics, tokens, surface);
        }

        var core = Desugar.Desugarer.Desugar(surface, diagnostics);

        if (stopAfter == PipelineStage.Desugar || diagnostics.HasErrors)
        {
            return Result(diagnostics, tokens, surface, core);
        }

        var scope = prelude ?? PreludeLoader.Load();
        var typed = TypeChecker.TypeChecker.Check(core, scope, diagnostics);

        if (stopAfter == PipelineStage.TypeCheck || diagnostics.HasErrors)
        {
            return Result(diagnostics, tokens, surface, core, typed);
        }

        var evaluation = Evaluator.Evaluator.Run(typed, scope, context ?? new RuntimeContext(new StringOutput()));

        return Result(diagnostics, tokens, surface, core, typed, evaluation);
    }

    private static CompilationResult Result(
        DiagnosticBag diagnostics,
        ImmutableArray<Token>? tokens = null,
        SourceFile? surface = null,
        CoreProgram? core = null,
        TypedProgram? typed = null,
        EvaluationResult? evaluation = null) =>
        new(tokens, surface, core, typed, evaluation, diagnostics.ToSortedArray());
}

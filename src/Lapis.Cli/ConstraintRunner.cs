using System.Collections.Immutable;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;
using Lapis.Macros;
using Lapis.Runtime;

namespace Lapis.Cli;

/// <summary>
/// Roda um <c>constraint</c> com o pipeline de verdade (plano 18 §18.5).
///
/// Mora no orquestrador porque é ele que conhece todas as fases: colocá-lo em
/// <c>Lapis.Macros</c> fecharia o ciclo <c>Macros → TypeChecker → … → Macros</c>.
/// É a mesma divisão do prelude, que já existia desde o plano 09.
///
/// O pipeline é o completo <b>menos macros</b> — um <c>constraint</c> não invoca
/// macros, e permitir que invocasse tornaria a expansão reentrante sem ganho
/// nenhum:
///
/// <code>
/// bloco do constraint → Desugar → TypeChecker → Evaluator → ConstraintOutcome
/// </code>
///
/// Uma instância por compilação, e com ela um <see cref="CompileContext"/> por
/// compilação: é o que faz duas compilações não se enxergarem.
/// </summary>
public sealed class ConstraintRunner : IConstraintRunner
{
    private readonly PreludeScope _prelude;
    private readonly CompileTimeScope _compileTime;
    private readonly DiagnosticBag _diagnostics;
    private readonly RuntimeContext _context;

    public ConstraintRunner(PreludeScope prelude, DiagnosticBag diagnostics, IOutput output)
    {
        ArgumentNullException.ThrowIfNull(prelude);
        ArgumentNullException.ThrowIfNull(diagnostics);

        _prelude = prelude;
        _diagnostics = diagnostics;
        Context = new CompileContext();
        _compileTime = new CompileTimeScope(Context, prelude);
        _context = new RuntimeContext(output);
    }

    /// <summary>
    /// O estado acumulado por esta compilação. Exposto para os testes poderem
    /// observar o que as constraints registraram — o programa expandido não o vê.
    /// </summary>
    public CompileContext Context { get; }

    public ConstraintOutcome Run(BlockExpression constraint, MatchResult bindings)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        ArgumentNullException.ThrowIfNull(bindings);

        // Diagnósticos da constraint entram no mesmo saco do programa: uma
        // constraint que não compila é um erro do arquivo, com o span dela.
        var before = _diagnostics.Count;
        var file = AsSourceFile(constraint, bindings);

        var core = Desugar.Desugarer.Desugar(file, _diagnostics);

        if (_diagnostics.Count != before)
        {
            return ConstraintOutcome.Failed;
        }

        var typed = TypeChecker.TypeChecker.Check(core, _prelude, _diagnostics, _compileTime);

        if (_diagnostics.Count != before)
        {
            return ConstraintOutcome.Failed;
        }

        var evaluation = Evaluator.Evaluator.Run(typed, _prelude, _context, _compileTime);

        if (evaluation.Status == Evaluator.ExecutionStatus.Completed)
        {
            return ConstraintOutcome.Accepted;
        }

        // `throw` é o único aborto que a constraint pode causar de propósito; os
        // demais (LAP0302, LAP0303) são defeitos dela, e viram diagnóstico
        // próprio em vez de virarem "esta construção é inválida".
        if (evaluation.Code != DiagnosticCodes.ConstraintRejected)
        {
            _diagnostics.ReportError(
                evaluation.Code ?? DiagnosticCodes.ConstraintRejected,
                evaluation.Span ?? constraint.Span,
                evaluation.Message ?? "a constraint abortou");

            return ConstraintOutcome.Failed;
        }

        return ConstraintOutcome.Rejected(evaluation.Message!, evaluation.Span ?? constraint.Span);
    }

    /// <summary>
    /// A constraint como programa: as capturas viram <c>def</c>, e o corpo dela
    /// vem logo em seguida.
    ///
    /// <b>Só capturas de literal são visíveis</b> (plano 18 §18.5). Um
    /// <c>Expression:e</c> capturou uma <b>árvore</b>, e uma constraint não executa
    /// o código da aplicação: ler <c>e</c> como valor será reflection, que é o
    /// plano 19. Até lá o nome simplesmente não existe no escopo, e quem o usar
    /// recebe o <c>LAP0201</c> normal.
    /// </summary>
    private static SourceFile AsSourceFile(BlockExpression constraint, MatchResult bindings)
    {
        var statements = ImmutableArray.CreateBuilder<Statement>();

        foreach (var (name, binding) in bindings.Bindings.OrderBy(b => b.Key, StringComparer.Ordinal))
        {
            if (binding is SingleBinding { Node: Expression literal } && IsLiteral(literal))
            {
                statements.Add(new DefStatement(name, null, literal)
                {
                    Span = literal.Span,
                    NameSpan = literal.Span,
                });
            }
        }

        statements.AddRange(constraint.Statements);

        // O valor de uma constraint é ignorado: ela decide por `throw`, não por
        // resultado. Uma cauda vira statement para não mudar isso.
        if (constraint.Tail is { } tail)
        {
            statements.Add(new ExpressionStatement(tail) { Span = tail.Span });
        }

        return new SourceFile(statements.ToImmutable()) { Span = constraint.Span };
    }

    private static bool IsLiteral(Expression expression) =>
        expression is IntLiteral or FloatLiteral or StrLiteral or BoolLiteral;
}

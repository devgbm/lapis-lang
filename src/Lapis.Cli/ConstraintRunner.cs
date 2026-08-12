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
    private readonly DeclarationTable _declarations;
    private readonly DiagnosticBag _diagnostics;
    private readonly RuntimeContext _context;

    /// <param name="declarations">
    /// Os <c>type</c> e <c>enum</c> do programa, lidos da sintaxe. É o que
    /// <c>reflect</c> enxerga dentro de um <c>constraint</c> (plano 19 §19.3).
    /// </param>
    public ConstraintRunner(
        PreludeScope prelude,
        DeclarationTable declarations,
        DiagnosticBag diagnostics,
        IOutput output)
    {
        ArgumentNullException.ThrowIfNull(prelude);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(diagnostics);

        _prelude = prelude;
        _declarations = declarations;
        _diagnostics = diagnostics;
        Context = new CompileContext();
        _context = new RuntimeContext(output);
    }

    /// <summary>
    /// O estado acumulado por esta compilação. Exposto para os testes poderem
    /// observar o que as constraints registraram — o programa expandido não o vê.
    /// </summary>
    public CompileContext Context { get; }

    public ConstraintOutcome Run(BlockExpression constraint, MatchResult bindings, SourceSpan invocation)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        ArgumentNullException.ThrowIfNull(bindings);

        // O ambiente é montado por invocação, e não uma vez só, porque o que
        // `reflect` enxerga depende de **onde** a macro foi invocada: uma macro só
        // vê os tipos declarados acima dela (Q8).
        var declarations = WithCapturedNames(_declarations.VisibleAt(invocation.Start), bindings);
        var compileTime = new CompileTimeScope(Context, _prelude, declarations);

        // Diagnósticos da constraint entram no mesmo saco do programa: uma
        // constraint que não compila é um erro do arquivo, com o span dela.
        var before = _diagnostics.Count;
        var file = AsSourceFile(constraint, bindings);

        var core = Desugar.Desugarer.Desugar(file, _diagnostics);

        if (_diagnostics.Count != before)
        {
            return ConstraintOutcome.Failed;
        }

        var typed = TypeChecker.TypeChecker.Check(core, _prelude, _diagnostics, compileTime);

        if (_diagnostics.Count != before)
        {
            return ConstraintOutcome.Failed;
        }

        var evaluation = Evaluator.Evaluator.Run(typed, _prelude, _context, compileTime);

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
    /// Uma captura de <c>Identifier</c> que nomeia um tipo declarado passa a
    /// valer como esse tipo dentro do <c>reflect</c>.
    ///
    /// Sem isto, <c>match Identifier:nome ... constraint { reflect(nome) }</c>
    /// procuraria um tipo literalmente chamado <c>nome</c> — e a macro que
    /// valida <b>o tipo que recebeu</b>, que é o uso inteiro de reflection em
    /// compile time, não teria como ser escrita.
    ///
    /// Não é reflection sobre AST (que fica para depois): a captura é um nome, e
    /// <c>reflect</c> já opera sobre nomes. O que se faz aqui é resolvê-lo.
    /// </summary>
    private static IReadOnlyDictionary<string, StructValue> WithCapturedNames(
        IReadOnlyDictionary<string, StructValue> declarations,
        MatchResult bindings)
    {
        var aliases = declarations.ToDictionary(StringComparer.Ordinal);

        foreach (var (name, binding) in bindings.Bindings)
        {
            if (binding is SingleBinding { Node: IdentifierExpression captured }
                && declarations.TryGetValue(captured.Name, out var info))
            {
                aliases[name] = info;
            }
        }

        return aliases;
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

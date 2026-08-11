using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Macros;

public enum ConstraintStatus
{
    /// <summary>A construção é válida; a expansão prossegue.</summary>
    Accepted,

    /// <summary>
    /// A constraint executou <c>throw</c>: a construção está errada e a
    /// compilação para. Nunca faz o mecanismo tentar outra regra — é isso que
    /// separa "não é esta a forma" de "esta forma está errada" (spec §7.1).
    /// </summary>
    Rejected,

    /// <summary>
    /// A própria constraint não compilou ou abortou. Os diagnósticos já foram
    /// reportados por quem a rodou, e o expander fica calado: somar
    /// <c>LAP0503</c> a um erro de tipo dentro da constraint só esconderia o erro
    /// de verdade.
    /// </summary>
    Failed,
}

/// <param name="Message">A mensagem passada a <c>throw</c>. É o texto do <c>LAP0503</c>.</param>
/// <param name="Span">Onde, dentro da macro, a rejeição nasceu — vira nota do diagnóstico.</param>
public sealed record ConstraintOutcome(ConstraintStatus Status, string? Message = null, SourceSpan? Span = null)
{
    public static readonly ConstraintOutcome Accepted = new(ConstraintStatus.Accepted);

    public static readonly ConstraintOutcome Failed = new(ConstraintStatus.Failed);

    public static ConstraintOutcome Rejected(string message, SourceSpan span) =>
        new(ConstraintStatus.Rejected, message, span);
}

/// <summary>
/// Executa o <c>constraint</c> de uma regra, entre o <c>match</c> e o
/// <c>expand</c> (spec de macros §8).
///
/// <b>Por que uma interface.</b> Rodar uma constraint exige desugar, checker e
/// evaluator, e <c>Lapis.Macros</c> depender deles fecharia o ciclo
/// <c>Macros → TypeChecker → … → Macros</c>. A saída é a mesma que o plano 09 já
/// usou para o prelude — <c>PreludeScope</c> é dado no Runtime, <c>PreludeLoader</c>
/// é carga no Cli: aqui se declara o contrato, e quem o implementa é o
/// orquestrador, que já conhece todas as fases (plano 18 §18.2).
///
/// <b>Desvio do plano.</b> O plano previa
/// <c>Run(constraint, bindings, CompileContext)</c>. O contexto saiu da assinatura
/// porque quem tem uma compilação inteira em mãos é a implementação, não o
/// expander: passá-lo a cada chamada obrigaria <c>Lapis.Macros</c> a conhecer
/// <c>Lapis.Runtime</c> só para repassar um objeto que nunca lê.
/// </summary>
public interface IConstraintRunner
{
    /// <param name="invocation">
    /// Onde a macro foi invocada. Não é só para diagnóstico: é o que decide o que
    /// <c>reflect</c> enxerga lá dentro, porque uma macro só vê os tipos
    /// declarados <b>acima</b> dela (Q8, plano 19 §19.3).
    /// </param>
    ConstraintOutcome Run(BlockExpression constraint, MatchResult bindings, SourceSpan invocation);
}

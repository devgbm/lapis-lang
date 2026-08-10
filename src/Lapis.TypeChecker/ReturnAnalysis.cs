using Lapis.Ast.Core;

namespace Lapis.TypeChecker;

/// <summary>
/// Análise de "retorna em todos os caminhos" (spec §12, §26).
///
/// Uma função cujo retorno não é <c>Void</c> precisa garantir estaticamente que
/// todo caminho termina em <c>return expressão</c>.
/// </summary>
public static class ReturnAnalysis
{
    /// <summary>
    /// <c>DR(e)</c> do plano 06 §6.4: a avaliação de <paramref name="expression"/>
    /// necessariamente executa um <c>return</c> da função corrente?
    /// </summary>
    public static bool DefinitelyReturns(CoreExpr expression) => expression switch
    {
        CoreReturn => true,

        // Um `return` no valor conta: `def x = return 1;` é tipável, já que
        // `Return` tem tipo Never.
        CoreLet n => DefinitelyReturns(n.Value) || DefinitelyReturns(n.Body),

        // Só conta se os dois ramos retornam — ou se a própria condição retorna.
        CoreIf n => DefinitelyReturns(n.Condition)
                    || (DefinitelyReturns(n.Then) && DefinitelyReturns(n.Else)),

        CoreBinary n => DefinitelyReturns(n.Left) || DefinitelyReturns(n.Right),

        CoreUnary n => DefinitelyReturns(n.Operand),

        CoreCall n => DefinitelyReturns(n.Callee) || n.Arguments.Any(DefinitelyReturns),

        // Um `return` dentro de uma lambda aninhada encerra **aquela** função,
        // não a que a contém (spec §12, último bullet).
        CoreLambda => false,

        _ => false,
    };
}

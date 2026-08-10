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
    public static bool DefinitelyReturns(CoreExpr expression) =>
        DefinitelyReturns(expression, new Dictionary<string, bool>(StringComparer.Ordinal));

    /// <param name="joins">
    /// Para cada rótulo em escopo, se saltar para ele necessariamente retorna.
    /// <c>Goto L</c> não retorna por si — ele desvia —, então a resposta é a do
    /// destino, e é isso que faz esta análise precisar de um ponto fixo.
    /// </param>
    private static bool DefinitelyReturns(CoreExpr expression, Dictionary<string, bool> joins) =>
        expression switch
        {
            CoreReturn => true,

            // Um salto condicional é um desvio de duas saídas, e a segunda é a
            // continuação — que, na Core, é o corpo deste `Let`. Só conta como
            // retorno se **as duas** retornam, exatamente como num `If`.
            //
            // Precisa ser tratado aqui, e não no caso de `CoreGotoIf`, porque o nó
            // sozinho não conhece a sua continuação.
            CoreLet { Value: CoreGotoIf jump } n =>
                DefinitelyReturns(jump.Condition, joins)
                || (Jumping(jump.Label, joins) && DefinitelyReturns(n.Body, joins)),

            // Um `return` no valor conta: `def x = return 1;` é tipável, já que
            // `Return` tem tipo Never.
            CoreLet n => DefinitelyReturns(n.Value, joins) || DefinitelyReturns(n.Body, joins),

            // Só conta se os dois ramos retornam — ou se a própria condição retorna.
            CoreIf n => DefinitelyReturns(n.Condition, joins)
                        || (DefinitelyReturns(n.Then, joins) && DefinitelyReturns(n.Else, joins)),

            // Todos os braços precisam retornar. A exaustividade é assumida porque o
            // checker já a exige (LAP0262): se o `match` não for exaustivo, os dois
            // diagnósticos aparecem, o que descreve corretamente as duas falhas.
            CoreMatch n => DefinitelyReturns(n.Scrutinee, joins)
                           || (!n.Arms.IsEmpty && n.Arms.All(a => DefinitelyReturns(a.Body, joins))),

            CoreBinary n => DefinitelyReturns(n.Left, joins) || DefinitelyReturns(n.Right, joins),

            CoreUnary n => DefinitelyReturns(n.Operand, joins),

            CoreCall n => DefinitelyReturns(n.Callee, joins) || n.Arguments.Any(a => DefinitelyReturns(a, joins)),

            // Saltar é sair deste caminho, não da função: quem responde é o destino.
            CoreGoto n => Jumping(n.Label, joins),

            // Fora da posição de valor de um `Let` um salto condicional não tem
            // continuação visível; a resposta conservadora é a correta.
            CoreGotoIf => false,

            CoreLabeled n => DefinitelyReturns(n, joins),

            // Um `return` dentro de uma lambda aninhada encerra **aquela** função,
            // não a que a contém (spec §12, último bullet).
            CoreLambda => false,

            _ => false,
        };

    /// <summary>
    /// Saltar para <paramref name="label"/> necessariamente retorna? Um rótulo
    /// desconhecido já é <c>LAP0520</c>; aqui ele não pode inventar um retorno
    /// que não existe.
    /// </summary>
    private static bool Jumping(string label, Dictionary<string, bool> joins) =>
        joins.TryGetValue(label, out var returns) && returns;

    /// <summary>
    /// Só a entrada decide: todo join é alcançado por um salto, e
    /// <c>DR(Goto L)</c> já consulta o join L.
    ///
    /// Com salto para trás os joins podem se referenciar em ciclo, então o valor
    /// de cada um é o <b>maior ponto fixo</b>: começa otimista (<c>true</c>) e
    /// itera até estabilizar. A leitura otimista é correta porque um join que só
    /// volta para o laço nunca cai fora da função sem passar por um <c>return</c>
    /// — ele repete, ou aborta em <c>LAP0303</c>.
    /// </summary>
    private static bool DefinitelyReturns(CoreLabeled node, Dictionary<string, bool> outer)
    {
        var joins = new Dictionary<string, bool>(outer, StringComparer.Ordinal);

        foreach (var join in node.Joins)
        {
            joins[join.Name] = true;
        }

        bool changed;

        do
        {
            changed = false;

            foreach (var join in node.Joins)
            {
                var current = DefinitelyReturns(join.Body, joins);

                if (joins[join.Name] != current)
                {
                    joins[join.Name] = current;
                    changed = true;
                }
            }
        }
        while (changed);

        return DefinitelyReturns(node.Entry, joins);
    }
}

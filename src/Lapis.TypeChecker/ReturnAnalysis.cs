using Lapis.Ast.Core;

namespace Lapis.TypeChecker;

/// <summary>
/// Análise de "retorna em todos os caminhos" (spec §12, §26).
///
/// Uma função cujo retorno não é <c>Void</c> precisa garantir estaticamente que
/// todo caminho termina em <c>return expressão</c>.
///
/// Sem ponto fixo desde o plano 26 (Q32): a versão com <c>goto</c>/<c>label</c>
/// precisava de um porque os joins podiam se referenciar em ciclo (salto para
/// trás). <c>loop</c> não tem grafo de joins — o corpo é um escopo léxico só —,
/// então a análise volta a ser uma recursão estrutural direta.
/// </summary>
public static class ReturnAnalysis
{
    /// <summary>
    /// <c>DR(e)</c> do plano 06 §6.4: a avaliação de <paramref name="expression"/>
    /// necessariamente executa um <c>return</c> da função corrente?
    /// </summary>
    public static bool DefinitelyReturns(CoreExpr expression) =>
        expression switch
        {
            CoreReturn => true,

            // `throw` não retorna: ele **diverge**. Mas a pergunta que esta análise
            // responde é "esta função pode cair pelo fim sem produzir valor?", e
            // por um caminho que aborta a compilação ela não pode.
            CoreThrow => true,

            // Um `return` no valor conta: `def x = return 1;` é tipável, já que
            // `Return` tem tipo Never.
            CoreLet n => DefinitelyReturns(n.Value) || DefinitelyReturns(n.Body),

            // Só conta se os dois ramos retornam — ou se a própria condição retorna.
            CoreIf n => DefinitelyReturns(n.Condition)
                        || (DefinitelyReturns(n.Then) && DefinitelyReturns(n.Else)),

            // `is` ramifica como `if`: o escrutinado está na posição da condição,
            // e os dois ramos precisam retornar (plano 25).
            CoreIs n => DefinitelyReturns(n.Scrutinee)
                        || (DefinitelyReturns(n.Then) && DefinitelyReturns(n.Else)),

            // Todos os braços precisam retornar. A exaustividade é assumida porque o
            // checker já a exige (LAP0262): se o `match` não for exaustivo, os dois
            // diagnósticos aparecem, o que descreve corretamente as duas falhas.
            CoreMatch n => DefinitelyReturns(n.Scrutinee)
                           || (!n.Arms.IsEmpty && n.Arms.All(a => DefinitelyReturns(a.Body))),

            CoreBinary n => DefinitelyReturns(n.Left) || DefinitelyReturns(n.Right),

            CoreUnary n => DefinitelyReturns(n.Operand),

            CoreCall n => DefinitelyReturns(n.Callee) || n.Arguments.Any(DefinitelyReturns),

            // `x = return 1;` é tipável: `Return` tem tipo Never e cabe em
            // qualquer posição.
            CoreAssign n => DefinitelyReturns(n.Value),

            // Um `loop` que ninguém quebra (ou só quebra por `return`/`throw`, já
            // cobertos acima) não continua depois de si: se cai fora, é porque
            // parou de existir uma volta que caia fora normalmente. Análogo ao
            // `DR(Goto L) = DR(L)` do plano 16 — o que muda é que agora a pergunta
            // é "existe break que alcança este loop", não "o destino retorna".
            CoreLoop n => !ReachesOwnBreak(n.Body, n.Label),

            // `break`/`continue` não retornam por si — eles desviam para o loop
            // (ou saem dele), não para fora da função. Continuação depois de um
            // `break`/`continue` **incondicional** é inalcançável, mas isso é
            // scope do checker de código morto, não desta análise.
            CoreBreak or CoreContinue => false,

            // Um `return` dentro de uma lambda aninhada encerra **aquela** função,
            // não a que a contém (spec §12, último bullet).
            CoreLambda => false,

            _ => false,
        };

    /// <summary>
    /// Existe, dentro de <paramref name="node"/>, algum <c>break</c> que alcança o
    /// <c>loop</c> identificado por <paramref name="label"/> (<c>null</c> = sem
    /// rótulo)? Reusa <see cref="CoreWalker"/> para não deixar de descer numa
    /// posição de filho por esquecimento — a mesma preocupação que levou a
    /// <c>FreeVariables</c> a precisar da tabela de resoluções (plano 15,
    /// M13-M15): perder um caso aqui é aceitar como "nunca retorna" uma função
    /// que na verdade retorna.
    /// </summary>
    private static bool ReachesOwnBreak(CoreExpr node, string? label)
    {
        var finder = new LoopBreakFinder(label);
        finder.Visit(node);
        return finder.Found;
    }

    /// <summary>
    /// Um <c>break</c> sem rótulo alcança o laço mais próximo — a busca para de
    /// contar <c>break</c> sem rótulo assim que desce para dentro de outro
    /// <c>loop</c>, mas continua contando um <c>break</c> rotulado, que salta por
    /// cima de quantos laços for preciso. Não desce em <see cref="CoreLambda"/>:
    /// <c>break</c>/<c>continue</c> não atravessam fronteira de função (o checker
    /// já recusa), e um rótulo homônimo lá dentro seria outro laço.
    /// </summary>
    private sealed class LoopBreakFinder(string? targetLabel) : CoreWalker
    {
        private bool _crossedNestedLoop;

        public bool Found { get; private set; }

        public override void Visit(CoreExpr node)
        {
            if (Found || node is CoreLambda)
            {
                return;
            }

            switch (node)
            {
                case CoreBreak b:
                    Found = b.Label is not null
                        ? b.Label == targetLabel
                        : targetLabel is null && !_crossedNestedLoop;

                    return;

                case CoreLoop loop:
                    var was = _crossedNestedLoop;
                    _crossedNestedLoop = true;
                    Visit(loop.Body);
                    _crossedNestedLoop = was;
                    return;

                default:
                    base.Visit(node);
                    return;
            }
        }
    }
}

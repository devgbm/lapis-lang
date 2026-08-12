using Lapis.Diagnostics;

namespace Lapis.Evaluator.Tests;

/// <summary>Execução de <c>var</c> e reatribuição (Q25).</summary>
public sealed class MutationEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void Assignment_ChangesTheValue() =>
        Output("var x = 1;\nprint(x);\nx = 2;\nprint(x);").ShouldBe("1\n2\n");

    [Fact]
    public void Assignment_SeesTheCurrentValue() =>
        Output("var x = 1;\nx = x + 10;\nprint(x);").ShouldBe("11\n");

    /// <summary>
    /// O que a Q25 destravou. O corpo de um <c>loop</c> roda sempre no mesmo
    /// escopo léxico; um <c>var</c> declarado antes dele é o slot que atravessa
    /// as voltas, e é ele que faz a condição de saída chegar (plano 26, M16).
    /// </summary>
    [Fact]
    public void BackwardLoop_MakesProgress() =>
        Output("""
            var i = 0;

            loop {
                i = i + 1;
                print(i);
                if i >= 3 { break; }
            }
            """).ShouldBe("1\n2\n3\n");

    [Fact]
    public void BackwardLoop_Accumulates() =>
        Output("""
            var soma = 0;
            var i = 1;

            loop {
                soma = soma + i;
                i = i + 1;
                if i > 10 { break; }
            }

            print(soma);
            """).ShouldBe("55\n");

    /// <summary>Um laço sem condição de saída continua abortando — o orçamento vale.</summary>
    [Fact]
    public void LoopWithoutExit_StillAborts() =>
        ExpectAbort("var i = 0;\nloop { i = i + 1; }")
            .ShouldBe(DiagnosticCodes.IterationLimitExceeded);

    /// <summary>
    /// Cada chamada tem o seu slot: um <c>var</c> local é criado de novo a cada
    /// entrada na função, e nada vaza de uma chamada para a seguinte.
    /// </summary>
    [Fact]
    public void LocalVar_IsFreshPerCall() =>
        Output("""
            def contar = fn(ate: Int) Int {
                var total = 0;
                var i = 0;

                loop {
                    total = total + i;
                    i = i + 1;
                    if i > ate { break; }
                }

                return total;
            };

            print(contar(3));
            print(contar(3));
            """).ShouldBe("6\n6\n");

    /// <summary>
    /// Sombrear num bloco interno cria outro slot: o de fora não é tocado.
    /// </summary>
    [Fact]
    public void InnerShadow_DoesNotTouchTheOuterSlot() =>
        Output("var x = 1;\ndef bloco = { var x = 2; x };\nprint(bloco);\nprint(x);")
            .ShouldBe("2\n1\n");

    /// <summary>Uma atribuição num ramo não executado não acontece.</summary>
    [Fact]
    public void Assignment_InsideUntakenBranch_DoesNotHappen() =>
        Output("var x = 1;\nif false { x = 99; }\nprint(x);").ShouldBe("1\n");

    [Fact]
    public void Assignment_InsideTakenBranch_Happens() =>
        Output("var x = 1;\nif true { x = 99; }\nprint(x);").ShouldBe("99\n");
}

using Lapis.Diagnostics;

namespace Lapis.Evaluator.Tests;

/// <summary>
/// Execução de <c>loop</c>/<c>break</c>/<c>continue</c> (plano 26, M16 — Q32).
/// Substitui <c>GotoEvaluationTests</c> (plano 16 §16.6, retirado).
/// </summary>
public sealed class LoopEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void Break_ExitsImmediately() =>
        Output("loop { break; print(1); }\nprint(2);").ShouldBe("2\n");

    [Fact]
    public void Break_WithValue_BecomesTheLoopResult() =>
        Output("def x = loop { break 5; };\nprint(x);").ShouldBe("5\n");

    [Fact]
    public void Break_WhenConditionTrue_Exits() =>
        Output("loop { if true { break; } print(1); }\nprint(2);").ShouldBe("2\n");

    [Fact]
    public void Break_WhenConditionFalse_ContinuesTheBody() =>
        Output("var vez = 0;\nloop { vez = vez + 1; if vez > 1 { break; } print(1); }\nprint(2);")
            .ShouldBe("1\n2\n");

    /// <summary>
    /// <c>continue</c> pula o resto do corpo e reinicia — sem executar o que vem
    /// depois dele na mesma volta.
    /// </summary>
    [Fact]
    public void Continue_SkipsTheRestOfTheBody() =>
        Output("""
            var i = 0;

            loop {
                i = i + 1;

                if i == 2 { continue; }

                print(i);

                if i >= 3 { break; }
            }
            """).ShouldBe("1\n3\n");

    /// <summary>Um laço aninhado consome o próprio <c>break</c> sem rótulo — o de fora não vê.</summary>
    [Fact]
    public void Break_Unlabeled_TargetsOnlyTheInnerLoop() =>
        Output("""
            var voltas = 0;

            loop {
                voltas = voltas + 1;

                loop {
                    break;
                }

                if voltas >= 2 { break; }
            }

            print(voltas);
            """).ShouldBe("2\n");

    /// <summary>Rotulado, o <c>break</c> alcança o laço de fora, por cima do de dentro.</summary>
    [Fact]
    public void Break_Labeled_TargetsTheOuterLoop() =>
        Output("""
            def x = loop :fora {
                loop {
                    break :fora, 1;
                }
            };

            print(x);
            """).ShouldBe("1\n");

    [Fact]
    public void Continue_Labeled_RestartsTheOuterLoop() =>
        Output("""
            var i = 0;
            var voltas = 0;

            loop :fora {
                i = i + 1;
                voltas = voltas + 1;

                loop {
                    if i < 3 { continue :fora; }
                    break;
                }

                break;
            }

            print(voltas);
            """).ShouldBe("3\n");

    [Fact]
    public void Break_InsideFunction_DoesNotEscape() =>
        Output("""
            def escolher = fn(usar: Bool) Int {
                if usar {
                    loop { break; }
                    return 42;
                }

                return 0;
            };

            print(escolher(true));
            print(escolher(false));
            """).ShouldBe("42\n0\n");

    /// <summary>
    /// A forma escrita à mão de <c>@while</c> (plano 26) — a base sobre a qual a
    /// macro é construída. A equivalência com a macro em si (<c>@unless</c>,
    /// <c>@while</c>) é coberta em <c>Lapis.Cli.Tests/PreludeMacroTests</c>, onde
    /// o prelude é expandido de verdade.
    /// </summary>
    [Fact]
    public void WhileByHand_MatchesTheMacroShape() =>
        Output("""
            var i = 0;

            loop {
                if i < 3 {
                    i = i + 1;
                    print(i);
                } else {
                    break;
                }
            }
            """).ShouldBe("1\n2\n3\n");

    // ---------------------------------------------------- orçamento e pilha

    /// <summary>
    /// Iteração, não recursão: cada volta é mais uma passagem pelo <c>while</c>
    /// do evaluator, e a pilha de C# não cresce. O teste falharia com
    /// <c>StackOverflowException</c>, que nem é capturável, se fosse recursão.
    /// </summary>
    [Fact]
    public void Loop_BackwardWithoutExit_AbortsWithoutGrowingTheStack() =>
        ExpectAbort("loop { }").ShouldBe(DiagnosticCodes.IterationLimitExceeded);

    /// <summary>
    /// Com condição sempre verdadeira, idem: nada muda entre as voltas — a
    /// condição continua verdadeira e o orçamento acaba.
    /// </summary>
    [Fact]
    public void Loop_ConditionAlwaysTrue_Aborts() =>
        ExpectAbort("def sempre = true;\nloop { if sempre { continue; } break; }")
            .ShouldBe(DiagnosticCodes.IterationLimitExceeded);

    /// <summary>Já um laço cuja condição de saída é imediata termina na primeira passagem.</summary>
    [Fact]
    public void Loop_ImmediateExit_Terminates() =>
        Output("loop { print(1); break; }\nprint(2);").ShouldBe("1\n2\n");
}

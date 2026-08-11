using Lapis.Diagnostics;

namespace Lapis.Evaluator.Tests;

/// <summary>Execução de saltos (plano 16 §16.6).</summary>
public sealed class GotoEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void Goto_SkipsStatements() =>
        Output("goto fim;\nprint(1);\nlabel fim;\nprint(2);").ShouldBe("2\n");

    [Fact]
    public void GotoIf_True_Jumps() =>
        Output("goto fim if true;\nprint(1);\nlabel fim;\nprint(2);").ShouldBe("2\n");

    [Fact]
    public void GotoIf_False_FallsThrough() =>
        Output("goto fim if false;\nprint(1);\nlabel fim;\nprint(2);").ShouldBe("1\n2\n");

    /// <summary>Um join que termina em salto continua no próximo — sem recursão.</summary>
    [Fact]
    public void Goto_ChainedJumps() =>
        Output("goto a;\nlabel a;\ngoto c;\nlabel b;\nprint(1);\nlabel c;\nprint(2);")
            .ShouldBe("2\n");

    [Fact]
    public void Goto_FallsIntoTheNextJoinWithoutJumping() =>
        Output("goto a;\nlabel a;\nprint(1);\nlabel b;\nprint(2);").ShouldBe("1\n2\n");

    /// <summary>Nomes anteriores ao salto continuam ligados no destino.</summary>
    [Fact]
    public void Goto_KeepsEarlierBindings() =>
        Output("def x = 7;\ngoto fim;\nlabel fim;\nprint(x);").ShouldBe("7\n");

    [Fact]
    public void Goto_InsideFunction_DoesNotEscape() =>
        Output("""
            def escolher = fn(usar: Bool) Int {
                goto padrao if usar;
                return 0;
                label padrao;
                return 42;
            };

            print(escolher(true));
            print(escolher(false));
            """).ShouldBe("42\n0\n");

    /// <summary>
    /// A forma com <c>goto</c> produz o mesmo que a forma com <c>if</c> — é o que
    /// justifica construir <c>@unless</c> sobre salto (plano 20).
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void UnlessByHand_MatchesIf(string condition)
    {
        var comIf = Output($"def c = {condition};\nif !c {{ print(\"corpo\"); }}");

        var comGoto = Output($"def c = {condition};\ngoto fim if c;\nprint(\"corpo\");\nlabel fim;");

        comGoto.ShouldBe(comIf);
    }

    // ---------------------------------------------------- salto para trás

    /// <summary>
    /// Salto para trás é iteração, não recursão: cada volta é mais uma passagem
    /// pelo <c>while</c> do evaluator, e a pilha de C# não cresce. Um milhão de
    /// voltas prova isso — o teste falharia com <c>StackOverflowException</c>,
    /// que nem é capturável.
    /// </summary>
    [Fact]
    public void Goto_BackwardLoop_AbortsWithoutGrowingTheStack() =>
        ExpectAbort("label repete;\ngoto repete;").ShouldBe(DiagnosticCodes.JumpLimitExceeded);

    /// <summary>
    /// Com condição, idem: nada muda entre as voltas porque os bindings são
    /// imutáveis, então a condição continua verdadeira e o orçamento acaba.
    /// </summary>
    [Fact]
    public void Goto_ConditionalBackwardLoop_Aborts() =>
        ExpectAbort("def sempre = true;\nlabel repete;\ngoto repete if sempre;")
            .ShouldBe(DiagnosticCodes.JumpLimitExceeded);

    /// <summary>Já um laço cuja condição é falsa termina na primeira passagem.</summary>
    [Fact]
    public void Goto_BackwardLoop_WithFalseCondition_Terminates() =>
        Output("def nunca = false;\nlabel repete;\nprint(1);\ngoto repete if nunca;\nprint(2);")
            .ShouldBe("1\n2\n");
}

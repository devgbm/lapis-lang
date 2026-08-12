namespace Lapis.Evaluator.Tests;

/// <summary><c>is</c>: comportamento em runtime (plano 25, M16 — fecha Q23).</summary>
public sealed class IsEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void NoBinding_True_PrintsTrue() =>
        Output("def e = Option<Int>.Some(1);\nprint(e is Some);").ShouldBe("true\n");

    [Fact]
    public void NoBinding_False_PrintsFalse() =>
        Output("def e: Option<Int> = Option<Int>.None;\nprint(e is Some);").ShouldBe("false\n");

    [Fact]
    public void Bind_ThenBranch_UnwrapsThePayload() =>
        Output("def e = Option<Int>.Some(42);\nif e is Some(v) { print(v); } else { print(0); }")
            .ShouldBe("42\n");

    [Fact]
    public void Bind_ElseBranch_DoesNotRun() =>
        Output("def e: Option<Int> = Option<Int>.None;\nif e is Some(v) { print(v); } else { print(0); }")
            .ShouldBe("0\n");

    [Fact]
    public void Bind_LeftOfAndAlso_ShortCircuitsNormally() =>
        Output("def e = Option<Int>.Some(1);\nprint(e is Some(v) && v == 1);").ShouldBe("true\n");

    [Fact]
    public void Bind_LeftOfAndAlso_FalseWhenVariantDoesNotMatch() =>
        Output("def e: Option<Int> = Option<Int>.None;\nprint(e is Some(v) && v == 1);").ShouldBe("false\n");

    [Fact]
    public void Bind_InLoopViaIf_UnwrapsEachIteration() =>
        Output(
            """
            var i = 0;

            loop {
                var atual = Option<Int>.None;

                if i == 0 { atual = Option<Int>.Some(1); }
                if i == 2 { atual = Option<Int>.Some(3); }

                if atual is Some(v) { print(v); } else { print(0); }
                i = i + 1;
                if i < 3 { continue; } else { break; }
            }
            """)
        .ShouldBe("1\n0\n3\n");
}

namespace Lapis.Evaluator.Tests;

public sealed class MatchEvaluationTests : EvaluatorTestBase
{
    private const string Color = "def Color = enum { Red, Green, Blue };\n";

    [Fact]
    public void Match_SelectsMatchingVariant() =>
        Output($"{Color}print(match Color.Green {{ Color.Red => 1, Color.Green => 2, _ => 3 }});")
            .ShouldBe("2\n");

    [Fact]
    public void Match_FirstMatchingArmWins() =>
        Output($"{Color}print(match Color.Red {{ _ => 1, Color.Red => 2 }});").ShouldBe("1\n");

    [Fact]
    public void Match_Wildcard_CatchesRest() =>
        Output($"{Color}print(match Color.Blue {{ Color.Red => 1, _ => 99 }});").ShouldBe("99\n");

    [Fact]
    public void Match_BindingPattern_BindsScrutinee() =>
        Output($"{Color}print(match Color.Blue {{ c => c }});").ShouldBe("Color.Blue\n");

    [Fact]
    public void Match_BindsPayload() =>
        Output("def B = enum { Wrap(Int) };\nprint(match B.Wrap(42) { B.Wrap(v) => v });").ShouldBe("42\n");

    [Fact]
    public void Match_NestedPattern() =>
        Output("""
            def B = enum { Wrap(Int) };
            def O = enum { Outer(B) };

            print(match O.Outer(B.Wrap(7)) { O.Outer(B.Wrap(v)) => v });
            """).ShouldBe("7\n");

    [Fact]
    public void Match_LiteralPatterns() =>
        Output("""
            def classifica = fn(n: Int) Str {
                match n {
                    0 => return "zero",
                    1 => return "um",
                    _ => return "muitos"
                }
            };

            print(classifica(0));
            print(classifica(1));
            print(classifica(9));
            """).ShouldBe("zero\num\nmuitos\n");

    [Fact]
    public void Match_NegativeLiteralPattern() =>
        Output("print(match -1 { -1 => \"menos um\", _ => \"outro\" });").ShouldBe("menos um\n");

    [Fact]
    public void Match_StringLiteralPatterns() =>
        Output("print(match \"b\" { \"a\" => 1, \"b\" => 2, _ => 3 });").ShouldBe("2\n");

    [Fact]
    public void Match_BoolPatterns() =>
        Output("print(match true { true => 1, false => 2 });").ShouldBe("1\n");

    /// <summary>O escrutinado é avaliado uma única vez.</summary>
    [Fact]
    public void Match_ScrutineeEvaluatedOnce() =>
        Output($$"""
            {{Color}}
            def trace = fn() Color {
                print("avaliou");
                return Color.Red;
            };

            def x = match trace() { Color.Red => 1, _ => 2 };
            """).ShouldBe("avaliou\n");

    [Fact]
    public void Match_ArmWithReturn_ExitsFunction() =>
        Output($$"""
            {{Color}}
            def f = fn(c: Color) Int {
                match c {
                    Color.Red => return 1,
                    _ => return 2
                }

                print("nunca");
                return 3;
            };

            print(f(Color.Red));
            """).ShouldBe("1\n");

    /// <summary>Spec §22: o exemplo `unwrapOr`.</summary>
    [Fact]
    public void Section22_UnwrapOr() =>
        Output("""
            def unwrapOr = fn(result: Result<Int, IndexError>, fallback: Int) Int {
                match result {
                    Result.Ok(value) => return value,
                    Result.Err(error) => return fallback
                }
            };

            def values = [10, 20, 30];

            print(unwrapOr(values[1], 0));
            print(unwrapOr(values[9], 0));
            """).ShouldBe("20\n0\n");

    /// <summary>Spec §22: `match` como expressão de bloco comum.</summary>
    [Fact]
    public void Section22_MatchAsExpression() =>
        Output("""
            def values = [10, 20, 30];

            def x = match values[1] {
                Result.Ok(value) => value,
                Result.Err(error) => 0
            };

            print(x);
            """).ShouldBe("20\n");
}

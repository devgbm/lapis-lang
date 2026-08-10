namespace Lapis.Evaluator.Tests;

public sealed class ArrayEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void Array_Literal() => Eval("[1, 2, 3]").ShouldBe("[1, 2, 3]");

    [Fact]
    public void Array_OfStrings_PrintsQuoted() => Eval("[\"a\", \"b\"]").ShouldBe("[\"a\", \"b\"]");

    [Fact]
    public void Array_Nested() => Eval("[[1], [2, 3]]").ShouldBe("[[1], [2, 3]]");

    [Fact]
    public void Array_ElementsEvaluateLeftToRight() =>
        Output("""
            def trace = fn(n: Int) Int {
                print(n);
                return n;
            };

            def a = [trace(1), trace(2), trace(3)];
            """).ShouldBe("1\n2\n3\n");

    [Fact]
    public void Array_Equality_IsElementwise()
    {
        Eval("[1, 2] == [1, 2]").ShouldBe("true");
        Eval("[1, 2] == [2, 1]").ShouldBe("false");
    }
}

/// <summary>
/// Os três casos da spec §50, mais as fronteiras. Indexar <b>nunca</b> lança e
/// nunca aborta (spec §30).
/// </summary>
public sealed class IndexEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void Index_First_IsOk() => Eval("[1, 2, 3][0]").ShouldBe("Result.Ok(1)");

    [Fact]
    public void Index_Last_IsOk() => Eval("[1, 2, 3][2]").ShouldBe("Result.Ok(3)");

    [Fact]
    public void Index_PastEnd_IsErr() =>
        Eval("[1, 2, 3][3]").ShouldBe("Result.Err(IndexError.OutOfBounds)");

    [Fact]
    public void Index_Negative_IsErr() =>
        Eval("[1, 2, 3][-1]").ShouldBe("Result.Err(IndexError.OutOfBounds)");

    [Fact]
    public void Index_EmptyArray_IsErr() =>
        Eval("[1][5]").ShouldBe("Result.Err(IndexError.OutOfBounds)");

    [Fact]
    public void Index_FarOutOfBounds_IsErr() =>
        Eval("[1][9223372036854775807]").ShouldBe("Result.Err(IndexError.OutOfBounds)");

    [Fact]
    public void Index_Nested_WrapsInnerArray() => Eval("[[1, 2]][0]").ShouldBe("Result.Ok([1, 2])");

    [Fact]
    public void Index_ComputedIndex() => Eval("[10, 20, 30][1 + 1]").ShouldBe("Result.Ok(30)");

    [Fact]
    public void Index_TargetBeforeIndex() =>
        Output("""
            def trace = fn(n: Int) Int {
                print(n);
                return n;
            };

            def arrays = [[1]];
            def r = arrays[trace(0)];
            """).ShouldBe("0\n");

    /// <summary>
    /// Spec §30: fora de limites é semântica do programa, não erro do evaluator.
    /// Para qualquer índice, o resultado é sempre um <c>Result</c>.
    /// </summary>
    [Theory]
    [InlineData("-9223372036854775807")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("1000")]
    public void Index_NeverThrows(string index)
    {
        var outcome = Run($"print([1, 2, 3][{index}]);");

        outcome.Status.ShouldBe(ExecutionStatus.Completed);
        outcome.Output.ShouldStartWith("Result.");
    }
}

public sealed class EnumEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void EnumDef_ProducesTypeValue() =>
        Eval("{ def Color = enum { Red }; Color }").ShouldBe("<tipo Color>");

    [Fact]
    public void NullaryVariant() =>
        Output("def Color = enum { Red, Green }; print(Color.Green);").ShouldBe("Color.Green\n");

    [Fact]
    public void VariantWithPayload() =>
        Output("def Box = enum { Wrap(Int) }; print(Box.Wrap(42));").ShouldBe("Box.Wrap(42)\n");

    [Fact]
    public void VariantWithMultiplePayloads() =>
        Output("def P = enum { Pair(Int, Str) }; print(P.Pair(1, \"a\"));")
            .ShouldBe("P.Pair(1, \"a\")\n");

    [Fact]
    public void Variant_Equality_ConsidersVariant() =>
        Output("""
            def Color = enum { Red, Green };

            print(Color.Red == Color.Red);
            print(Color.Red == Color.Green);
            """).ShouldBe("true\nfalse\n");

    [Fact]
    public void Variant_Equality_ConsidersPayload() =>
        Output("""
            def Box = enum { Wrap(Int) };

            print(Box.Wrap(1) == Box.Wrap(1));
            print(Box.Wrap(1) == Box.Wrap(2));
            """).ShouldBe("true\nfalse\n");

    [Fact]
    public void IndexError_FromPrelude() =>
        Output("print(IndexError.OutOfBounds);").ShouldBe("IndexError.OutOfBounds\n");

    /// <summary>Q3: enums são impressos qualificados, como devem ser escritos.</summary>
    [Fact]
    public void Enums_PrintQualified() =>
        Output("def Color = enum { Red }; print(Color.Red);").ShouldContain("Color.Red");
}

public sealed class SpecSection50Tests : EvaluatorTestBase
{
    /// <summary>Os três casos literais da spec §50, num só programa.</summary>
    [Fact]
    public void ThreeCasesFromSpec() =>
        Output("""
            print([1,2,3][0]);
            print([1,2,3][2]);
            print([1,2,3][3]);
            """).ShouldBe("""
            Result.Ok(1)
            Result.Ok(3)
            Result.Err(IndexError.OutOfBounds)

            """.ReplaceLineEndings("\n"));

    /// <summary>Spec §42: o caso que o partial evaluator vai otimizar no M8.</summary>
    [Fact]
    public void Section42_Example() =>
        Output("""
            def values = [10, 20, 30];
            def x = values[1];

            print(x);
            """).ShouldBe("Result.Ok(20)\n");

    /// <summary>Spec §5: programa completo com array e indexação.</summary>
    [Fact]
    public void Section5_Example() =>
        Output("""
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            def numbers = [1, 2, 3];

            def result = numbers[1];

            print(result);
            """).ShouldBe("Result.Ok(2)\n");
}

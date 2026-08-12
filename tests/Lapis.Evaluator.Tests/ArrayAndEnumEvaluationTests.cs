namespace Lapis.Evaluator.Tests;

public sealed class SpanEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void Span_Literal() => Eval(".[1, 2, 3]").ShouldBe("[1, 2, 3]");

    [Fact]
    public void Span_OfStrings_PrintsQuoted() => Eval(".[\"a\", \"b\"]").ShouldBe("[\"a\", \"b\"]");

    [Fact]
    public void Span_Nested() => Eval(".[.[1], .[2, 3]]").ShouldBe("[[1], [2, 3]]");

    [Fact]
    public void Span_ElementsEvaluateLeftToRight() =>
        Output("""
            def trace = fn(n: Int) Int {
                print(n);
                return n;
            };

            def a = .[trace(1), trace(2), trace(3)];
            """).ShouldBe("1\n2\n3\n");

    [Fact]
    public void Span_Equality_IsElementwise()
    {
        Eval(".[1, 2] == .[1, 2]").ShouldBe("true");
        Eval(".[1, 2] == .[2, 1]").ShouldBe("false");
    }

    /// <summary>
    /// <c>length</c> é o tamanho que o span carrega em execução (plano 24 §24.6).
    /// Aqui o span é <c>var</c>, então o checker não sabe o tamanho e a leitura
    /// acontece de fato no valor.
    /// </summary>
    [Fact]
    public void Span_Length_IsReadAtRuntime() =>
        Output("var a = .[1, 2, 3];\nprint(a.length);").ShouldBe("3\n");

    /// <summary>E com o tamanho no tipo, o mesmo número sai sem tocar no valor.</summary>
    [Fact]
    public void Span_Length_OfKnownSize() =>
        Output("def a = .[1, 2, 3];\nprint(a.length);").ShouldBe("3\n");

    [Fact]
    public void Span_Length_OfEmpty() =>
        Output("def a: [Int;0] = .[];\nprint(a.length);").ShouldBe("0\n");
}

/// <summary>
/// Os três casos da spec §50, mais as fronteiras — revisados pelo plano 24.
///
/// Onde o tamanho está no tipo a indexação é total e o elemento sai nu; onde não
/// está, sai um <c>Option</c>. Nenhum dos dois lança ou aborta (spec §30).
/// </summary>
public sealed class IndexEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void Index_KnownSize_First_IsTotal() => Eval(".[1, 2, 3][0]").ShouldBe("1");

    [Fact]
    public void Index_KnownSize_Last_IsTotal() => Eval(".[1, 2, 3][2]").ShouldBe("3");

    [Fact]
    public void Index_UnknownSize_InBounds_IsSome() =>
        Output("var a = .[1, 2, 3];\nprint(a[0]);").ShouldBe("Option.Some(1)\n");

    [Fact]
    public void Index_UnknownSize_PastEnd_IsNone() =>
        Output("var a = .[1, 2, 3];\nprint(a[3]);").ShouldBe("Option.None\n");

    [Fact]
    public void Index_UnknownSize_Negative_IsNone() =>
        Output("var a = .[1, 2, 3];\nprint(a[0 - 1]);").ShouldBe("Option.None\n");

    [Fact]
    public void Index_UnknownSize_EmptySpan_IsNone() =>
        Output("var a: [Int;?] = .[];\nprint(a[0]);").ShouldBe("Option.None\n");

    [Fact]
    public void Index_UnknownSize_FarOutOfBounds_IsNone() =>
        Output("var a = .[1];\nprint(a[9223372036854775807]);").ShouldBe("Option.None\n");

    [Fact]
    public void Index_Nested_YieldsInnerSpan() => Eval(".[.[1, 2]][0]").ShouldBe("[1, 2]");

    /// <summary>
    /// Índice computado não é constante para o checker, então a indexação volta a
    /// ser parcial mesmo com o tamanho no tipo.
    /// </summary>
    [Fact]
    public void Index_ComputedIndex_IsOption() =>
        Output("var i = 1;\nprint(.[10, 20, 30][i + 1]);").ShouldBe("Option.Some(30)\n");

    [Fact]
    public void Index_TargetBeforeIndex() =>
        Output("""
            def trace = fn(n: Int) Int {
                print(n);
                return n;
            };

            var spans = .[.[1]];
            def r = spans[trace(0)];
            """).ShouldBe("0\n");

    /// <summary>
    /// Spec §30: fora de limites é semântica do programa, não erro do evaluator.
    /// Onde o tamanho não está no tipo, qualquer índice produz um <c>Option</c>.
    /// </summary>
    [Theory]
    [InlineData("0 - 9223372036854775807")]
    [InlineData("0 - 1")]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("1000")]
    public void Index_NeverThrows(string index)
    {
        var outcome = Run($"var a = .[1, 2, 3];\nprint(a[{index}]);");

        outcome.Status.ShouldBe(ExecutionStatus.Completed);
        outcome.Output.ShouldStartWith("Option.");
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
    public void Option_FromPrelude() =>
        Output("print(Option<Int>.None);").ShouldBe("Option.None\n");

    /// <summary>Q3: enums são impressos qualificados, como devem ser escritos.</summary>
    [Fact]
    public void Enums_PrintQualified() =>
        Output("def Color = enum { Red }; print(Color.Red);").ShouldContain("Color.Red");
}

public sealed class SpecSection50Tests : EvaluatorTestBase
{
    /// <summary>
    /// Os três casos literais da spec §50, num só programa. O span é <c>var</c>:
    /// é o caso em que o tamanho não está no tipo, que é do que a §50 fala.
    /// </summary>
    [Fact]
    public void ThreeCasesFromSpec() =>
        Output("""
            var a = .[1,2,3];

            print(a[0]);
            print(a[2]);
            print(a[3]);
            """).ShouldBe("""
            Option.Some(1)
            Option.Some(3)
            Option.None

            """.ReplaceLineEndings("\n"));

    /// <summary>
    /// Spec §42: o caso que o partial evaluator otimiza. Com o tamanho no tipo, o
    /// checker já resolveu a indexação e o valor sai nu.
    /// </summary>
    [Fact]
    public void Section42_Example() =>
        Output("""
            def values = .[10, 20, 30];
            def x = values[1];

            print(x);
            """).ShouldBe("20\n");

    /// <summary>Spec §5: programa completo com span e indexação.</summary>
    [Fact]
    public void Section5_Example() =>
        Output("""
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            def numbers = .[1, 2, 3];

            def result = numbers[1];

            print(result);
            """).ShouldBe("2\n");
}

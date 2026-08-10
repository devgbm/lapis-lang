using Lapis.Ast;
using Lapis.Diagnostics;
using Lapis.Runtime;
using Environment = Lapis.Runtime.Environment;

namespace Lapis.Evaluator.Tests.Runtime;

public sealed class ValueTests
{
    [Fact]
    public void Values_HaveStructuralEquality() => new IntValue(1).ShouldBe(new IntValue(1));

    [Fact]
    public void Values_OfDifferentKinds_AreNotEqual() =>
        ((Value)new IntValue(1)).ShouldNotBe(new FloatValue(1.0));

    [Fact]
    public void BoolValue_UsesSingletons() => BoolValue.Of(true).ShouldBeSameAs(BoolValue.True);

    [Fact]
    public void VoidValue_IsSingleton() => VoidValue.Instance.ShouldBeSameAs(VoidValue.Instance);

    [Fact]
    public void StrValue_EqualityIsOrdinal() => new StrValue("a").ShouldNotBe(new StrValue("A"));
}

public sealed class EnvironmentTests
{
    [Fact]
    public void Lookup_FindsLocalBinding()
    {
        var environment = Environment.Empty.Extend("x", new IntValue(1));

        environment.TryLookup("x", out var value).ShouldBeTrue();
        value.ShouldBe(new IntValue(1));
    }

    [Fact]
    public void Lookup_FindsParentBinding()
    {
        var environment = Environment.Empty.Extend("x", new IntValue(1)).Extend("y", new IntValue(2));

        environment.TryLookup("x", out var value).ShouldBeTrue();
        value.ShouldBe(new IntValue(1));
    }

    [Fact]
    public void Lookup_MissingBinding_ReturnsFalse() =>
        Environment.Empty.TryLookup("x", out _).ShouldBeFalse();

    [Fact]
    public void Shadowing_InnerWins()
    {
        var environment = Environment.Empty.Extend("x", new IntValue(1)).Extend("x", new IntValue(2));

        environment.TryLookup("x", out var value).ShouldBeTrue();
        value.ShouldBe(new IntValue(2));
    }

    [Fact]
    public void Extend_DoesNotMutateParent()
    {
        var parent = Environment.Empty.Extend("x", new IntValue(1));

        parent.Extend("x", new IntValue(2));

        parent.TryLookup("x", out var value).ShouldBeTrue();
        value.ShouldBe(new IntValue(1));
    }

    [Fact]
    public void Extend_IsPersistent_AcrossSiblings()
    {
        var parent = Environment.Empty.Extend("x", new IntValue(0));

        var a = parent.Extend("y", new IntValue(1));
        var b = parent.Extend("y", new IntValue(2));

        a.TryLookup("y", out var first).ShouldBeTrue();
        b.TryLookup("y", out var second).ShouldBeTrue();
        first.ShouldBe(new IntValue(1));
        second.ShouldBe(new IntValue(2));
    }

    [Fact]
    public void DeepChain_FindsRootBinding()
    {
        var environment = Environment.Empty.Extend("raiz", new IntValue(7));

        for (var i = 0; i < 1_000; i++)
        {
            environment = environment.Extend($"n{i}", new IntValue(i));
        }

        environment.TryLookup("raiz", out var value).ShouldBeTrue();
        value.ShouldBe(new IntValue(7));
    }

    [Fact]
    public void ExtendAll_LastBindingOfSameNameWins()
    {
        var environment = Environment.Empty.ExtendAll([("x", new IntValue(1)), ("x", new IntValue(2))]);

        environment.TryLookup("x", out var value).ShouldBeTrue();
        value.ShouldBe(new IntValue(2));
    }

    [Fact]
    public void ExtendAll_Empty_ReturnsSameEnvironment()
    {
        var environment = Environment.Empty.Extend("x", new IntValue(1));

        environment.ExtendAll([]).ShouldBeSameAs(environment);
    }
}

public sealed class PrimitiveTests
{
    [Fact]
    public void Add_Int() => Primitives.Add(new IntValue(1), new IntValue(2)).ShouldBe(new IntValue(3));

    [Fact]
    public void Add_Float() =>
        Primitives.Add(new FloatValue(1.5), new FloatValue(2.5)).ShouldBe(new FloatValue(4.0));

    [Fact]
    public void Add_Str() =>
        Primitives.Add(new StrValue("a"), new StrValue("b")).ShouldBe(new StrValue("ab"));

    [Fact]
    public void Add_MismatchedTypes_Throws() =>
        Should.Throw<InternalCompilerException>(() => Primitives.Add(new IntValue(1), new StrValue("a")));

    [Fact]
    public void Divide_Int_Truncates() =>
        Primitives.Divide(new IntValue(7), new IntValue(2)).ShouldBe(new IntValue(3));

    [Fact]
    public void Divide_Int_TruncatesTowardZero() =>
        Primitives.Divide(new IntValue(-7), new IntValue(2)).ShouldBe(new IntValue(-3));

    /// <summary>Q9: divisão inteira por zero é total e produz o maior <c>Int</c>.</summary>
    [Theory]
    [InlineData(1L)]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void Divide_Int_ByZero_IsMaxValue(long dividend) =>
        Primitives.Divide(new IntValue(dividend), new IntValue(0)).ShouldBe(new IntValue(long.MaxValue));

    [Fact]
    public void Divide_Float_ByZero_IsInfinity() =>
        Primitives.Divide(new FloatValue(1), new FloatValue(0))
            .ShouldBe(new FloatValue(double.PositiveInfinity));

    /// <summary>A única divisão que estoura em Int64; envolve como o resto da aritmética.</summary>
    [Fact]
    public void Divide_MinValueByMinusOne_Wraps() =>
        Primitives.Divide(new IntValue(long.MinValue), new IntValue(-1)).ShouldBe(new IntValue(long.MinValue));

    [Fact]
    public void Divide_IsTotal_ForEveryIntPair()
    {
        long[] samples = [long.MinValue, -7, -1, 0, 1, 7, long.MaxValue];

        foreach (var a in samples)
        {
            foreach (var b in samples)
            {
                Should.NotThrow(() => Primitives.Divide(new IntValue(a), new IntValue(b)));
            }
        }
    }

    [Fact]
    public void Negate_MinValue_Wraps() =>
        Primitives.Negate(new IntValue(long.MinValue)).ShouldBe(new IntValue(long.MinValue));

    [Fact]
    public void Compare_Str_IsOrdinalNotLinguistic()
    {
        // Ordinal: 'B' (66) < 'a' (97). Numa comparação linguística seria o contrário.
        Primitives.Compare(BinaryOperator.Less, new StrValue("B"), new StrValue("a")).Value.ShouldBeTrue();
    }

    [Fact]
    public void StructuralEquals_OnClosure_Throws()
    {
        var closure = MakeClosure();

        Should.Throw<InternalCompilerException>(() => Primitives.StructuralEquals(closure, closure));
    }

    private static ClosureValue MakeClosure()
    {
        var factory = new Ast.Core.CoreFactory();
        var lambda = factory.Lambda(
            SourceSpan.Synthetic, [], [], null, factory.Unit(SourceSpan.Synthetic), SourceSpan.Synthetic);

        return new ClosureValue(lambda, Environment.Empty, Ast.Types.FunctionType.Of([], Ast.Types.PrimitiveType.Void));
    }
}

public sealed class ValueFormatterTests
{
    [Theory]
    [InlineData(30L, "30")]
    [InlineData(-1L, "-1")]
    [InlineData(0L, "0")]
    public void Format_Int(long value, string expected) =>
        ValueFormatter.Format(new IntValue(value)).ShouldBe(expected);

    /// <summary>Um Float nunca perde o ponto decimal: <c>1.0</c> não vira <c>1</c>.</summary>
    [Theory]
    [InlineData(1.0, "1.0")]
    [InlineData(3.14, "3.14")]
    [InlineData(-0.5, "-0.5")]
    public void Format_Float_KeepsDecimalPoint(double value, string expected) =>
        ValueFormatter.Format(new FloatValue(value)).ShouldBe(expected);

    [Fact]
    public void Format_Bool() => ValueFormatter.Format(BoolValue.True).ShouldBe("true");

    [Fact]
    public void Format_Void() => ValueFormatter.Format(VoidValue.Instance).ShouldBe("()");

    [Fact]
    public void Format_Str_HasNoQuotes() => ValueFormatter.Format(new StrValue("hi")).ShouldBe("hi");

    [Fact]
    public void FormatDebug_Str_HasQuotes() =>
        ValueFormatter.FormatDebug(new StrValue("hi")).ShouldBe("\"hi\"");

    [Fact]
    public void FormatDebug_Str_EscapesSpecialCharacters() =>
        ValueFormatter.FormatDebug(new StrValue("a\nb")).ShouldBe("\"a\\nb\"");
}

public sealed class OutputTests
{
    [Fact]
    public void StringOutput_AccumulatesWrites()
    {
        var output = new StringOutput();

        output.Write("a");
        output.Write("b");

        output.Text.ShouldBe("ab");
    }

    [Fact]
    public void Print_AppendsNewline()
    {
        var output = new StringOutput();
        var context = new RuntimeContext(output);

        Natives.Print.Implementation([new IntValue(1)], context);

        output.Text.ShouldBe("1\n");
    }

    [Fact]
    public void Print_ReturnsVoid()
    {
        var context = new RuntimeContext(new StringOutput());

        Natives.Print.Implementation([new IntValue(1)], context).ShouldBe(VoidValue.Instance);
    }

    /// <summary>Regressão: o conjunto de nativos deve permanecer mínimo (plano 09 §9.2).</summary>
    [Fact]
    public void Natives_ListIsMinimal() => Natives.All.Select(n => n.Name).ShouldBe(["print"]);
}

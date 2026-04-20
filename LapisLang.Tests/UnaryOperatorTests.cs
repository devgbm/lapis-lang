using LapisLang.Core;

namespace LapisLang.Tests;

public class UnaryOperatorTests
{
    [Fact]
    public void Negate_Integer() => Assert.Equal(-5L, H.EvalAs<IntegerSymbol>("-5").Value);

    [Fact]
    public void Negate_Zero() => Assert.Equal(0L, H.EvalAs<IntegerSymbol>("-0").Value);

    [Fact]
    public void Identity_Integer() => Assert.Equal(7L, H.EvalAs<IntegerSymbol>("+7").Value);

    [Fact]
    public void Negate_Decimal() => Assert.Equal(-3.14m, H.EvalAs<DecimalSymbol>("-3.14").Value);

    [Fact]
    public void Identity_Decimal() => Assert.Equal(2.5m, H.EvalAs<DecimalSymbol>("+2.5").Value);

    [Fact]
    public void Not_True() => Assert.False(H.EvalAs<BooleanSymbol>("not true").Value);

    [Fact]
    public void Not_False() => Assert.True(H.EvalAs<BooleanSymbol>("not false").Value);

    [Fact]
    public void Not_Not_True() => Assert.True(H.EvalAs<BooleanSymbol>("not (not true)").Value);

    [Fact]
    public void Typeof_Integer_Returns_Integer_Type()
    {
        var result = H.Eval("typeof 42");
        Assert.Equal(LangDefaults.Types.Integer, result);
    }

    [Fact]
    public void Typeof_Boolean_Returns_Boolean_Type()
    {
        var result = H.Eval("typeof true");
        Assert.Equal(LangDefaults.Types.Boolean, result);
    }

    [Fact]
    public void Typeof_String_Returns_String_Type()
    {
        var result = H.Eval("typeof 'hello'");
        Assert.Equal(LangDefaults.Types.String, result);
    }

    [Fact]
    public void Typeof_Decimal_Returns_Decimal_Type()
    {
        var result = H.Eval("typeof 1.0");
        Assert.Equal(LangDefaults.Types.Decimal, result);
    }
}

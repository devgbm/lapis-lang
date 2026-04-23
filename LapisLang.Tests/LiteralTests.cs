using LapisLang.Core;

namespace LapisLang.Tests;

public class LiteralTests
{
    [Fact]
    public void Integer_Literal_Returns_IntegerSymbol()
    {
        var result = H.EvalAs<IntegerSymbol>("42");
        Assert.Equal(42L, result.Value);
    }

    [Fact]
    public void Integer_Zero_Returns_IntegerSymbol()
    {
        var result = H.EvalAs<IntegerSymbol>("0");
        Assert.Equal(0L, result.Value);
    }

    [Fact]
    public void Boolean_True_Returns_BooleanSymbol()
    {
        var result = H.EvalAs<BooleanSymbol>("true");
        Assert.True(result.Value);
    }

    [Fact]
    public void Boolean_False_Returns_BooleanSymbol()
    {
        var result = H.EvalAs<BooleanSymbol>("false");
        Assert.False(result.Value);
    }

    [Fact]
    public void String_Literal_Returns_StringSymbol()
    {
        var result = H.EvalAs<StringSymbol>("'hello'");
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void String_Empty_Returns_StringSymbol()
    {
        var result = H.EvalAs<StringSymbol>("''");
        Assert.Equal("", result.Value);
    }

    [Fact]
    public void Decimal_Literal_Returns_DecimalSymbol()
    {
        var result = H.EvalAs<DecimalSymbol>("3.14");
        Assert.Equal(3.14m, result.Value);
    }

    [Fact]
    public void Integer_Literal_Has_Integer_Type()
    {
        var result = H.EvalAs<IntegerSymbol>("99");
        Assert.Equal(LangDefaults.Types.Integer, result.Type);
    }

    [Fact]
    public void Boolean_Literal_Has_Boolean_Type()
    {
        var result = H.EvalAs<BooleanSymbol>("true");
        Assert.Equal(LangDefaults.Types.Boolean, result.Type);
    }

    [Fact]
    public void String_Literal_Has_String_Type()
    {
        var result = H.EvalAs<StringSymbol>("'hi'");
        Assert.Equal(LangDefaults.Types.String, result.Type);
    }

    [Fact]
    public void Decimal_Literal_Has_Decimal_Type()
    {
        var result = H.EvalAs<DecimalSymbol>("1.0");
        Assert.Equal(LangDefaults.Types.Decimal, result.Type);
    }

}

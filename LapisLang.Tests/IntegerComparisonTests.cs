using LapisLang.Core;

namespace LapisLang.Tests;

public class IntegerComparisonTests
{
    [Fact]
    public void Equality_True() => Assert.True(H.EvalAs<BooleanSymbol>("1 == 1").Value);

    [Fact]
    public void Equality_False() => Assert.False(H.EvalAs<BooleanSymbol>("1 == 2").Value);

    [Fact]
    public void Inequality_True() => Assert.True(H.EvalAs<BooleanSymbol>("1 != 2").Value);

    [Fact]
    public void Inequality_False() => Assert.False(H.EvalAs<BooleanSymbol>("1 != 1").Value);

    [Fact]
    public void LessThan_True() => Assert.True(H.EvalAs<BooleanSymbol>("1 < 2").Value);

    [Fact]
    public void LessThan_False() => Assert.False(H.EvalAs<BooleanSymbol>("2 < 1").Value);

    [Fact]
    public void LessThan_Equal_Is_False() => Assert.False(H.EvalAs<BooleanSymbol>("1 < 1").Value);

    [Fact]
    public void GreaterThan_True() => Assert.True(H.EvalAs<BooleanSymbol>("2 > 1").Value);

    [Fact]
    public void GreaterThan_False() => Assert.False(H.EvalAs<BooleanSymbol>("1 > 2").Value);

    [Fact]
    public void LessOrEqual_Equal() => Assert.True(H.EvalAs<BooleanSymbol>("1 <= 1").Value);

    [Fact]
    public void LessOrEqual_Less() => Assert.True(H.EvalAs<BooleanSymbol>("1 <= 2").Value);

    [Fact]
    public void LessOrEqual_Greater_Is_False() => Assert.False(H.EvalAs<BooleanSymbol>("2 <= 1").Value);

    [Fact]
    public void GreaterOrEqual_Equal() => Assert.True(H.EvalAs<BooleanSymbol>("2 >= 2").Value);

    [Fact]
    public void GreaterOrEqual_Greater() => Assert.True(H.EvalAs<BooleanSymbol>("3 >= 2").Value);

    [Fact]
    public void GreaterOrEqual_Less_Is_False() => Assert.False(H.EvalAs<BooleanSymbol>("1 >= 2").Value);

    [Fact]
    public void Comparison_Result_Has_Boolean_Type()
    {
        Assert.Equal(LangDefaults.Types.Boolean, H.EvalAs<BooleanSymbol>("1 == 1").Type);
    }
}

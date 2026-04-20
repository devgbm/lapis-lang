using LapisLang.Core;

namespace LapisLang.Tests;

public class BooleanLogicTests
{
    [Fact]
    public void And_True_True() => Assert.True(H.EvalAs<BooleanSymbol>("true and true").Value);

    [Fact]
    public void And_True_False() => Assert.False(H.EvalAs<BooleanSymbol>("true and false").Value);

    [Fact]
    public void And_False_True() => Assert.False(H.EvalAs<BooleanSymbol>("false and true").Value);

    [Fact]
    public void And_False_False() => Assert.False(H.EvalAs<BooleanSymbol>("false and false").Value);

    [Fact]
    public void Or_True_False() => Assert.True(H.EvalAs<BooleanSymbol>("true or false").Value);

    [Fact]
    public void Or_False_True() => Assert.True(H.EvalAs<BooleanSymbol>("false or true").Value);

    [Fact]
    public void Or_False_False() => Assert.False(H.EvalAs<BooleanSymbol>("false or false").Value);

    [Fact]
    public void Or_True_True() => Assert.True(H.EvalAs<BooleanSymbol>("true or true").Value);

    [Fact]
    public void Boolean_Equality_True() => Assert.True(H.EvalAs<BooleanSymbol>("true == true").Value);

    [Fact]
    public void Boolean_Equality_False() => Assert.False(H.EvalAs<BooleanSymbol>("true == false").Value);

    [Fact]
    public void Boolean_Inequality() => Assert.True(H.EvalAs<BooleanSymbol>("true != false").Value);
}

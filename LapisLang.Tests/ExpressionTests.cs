using LapisLang.Core;

namespace LapisLang.Tests;

public class ExpressionTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("0", 0)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("'text'", "text")]
    public void Expressions(string expression, object? expected)
    {
        var result = LapisInterpreter.Evaluate(expression);
        Assert.Equivalent(expected, result.value);
    }
}
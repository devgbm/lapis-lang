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
    public void LiteralExpressions(string expression, object? expected)
    {
        var result = LapisInterpreter.Evaluate(expression);
        Assert.Equivalent(expected, result.value);
    }

    [Theory]
    [InlineData("1+1", 2)]
    [InlineData("1-1", 0)]
    [InlineData("2*2", 4)]
    [InlineData("3/2", 1)]
    [InlineData("5%3", 2)]
    [InlineData("true and true", true)]
    [InlineData("true and false", false)]
    [InlineData("false and true", false)]
    [InlineData("false and false", false)]
    [InlineData("true or true", true)]
    [InlineData("false or true", true)]
    [InlineData("true or false", true)]
    [InlineData("false or false", false)]
    [InlineData("1 == 1", true)]
    [InlineData("1 == 2", false)]
    [InlineData("1 != 2", true)]
    [InlineData("1 > 0", true)]
    [InlineData("1 > 1", false)]
    [InlineData("1 > 2", false)]
    [InlineData("1 < 0", false)]
    [InlineData("1 < 1", false)]
    [InlineData("1 < 2", true)]
    [InlineData("1 >= 0", true)]
    [InlineData("1 >= 1", true)]
    [InlineData("1 >= 2", false)]
    [InlineData("1 <= 0", false)]
    [InlineData("1 <= 1", true)]
    [InlineData("1 <= 2", true)]
    public void BinaryExpressions(string expression, object? expected)
    {
        var result = LapisInterpreter.Evaluate(expression);
        Assert.Equivalent(expected, result.value);
    }

    [Theory]
    [InlineData("-1", -1)]
    [InlineData("+1", 1)]
    [InlineData("not true", false)]
    [InlineData("not false", true)]
    public void UnaryExpressions(string expression, object? expected)
    {
        var result = LapisInterpreter.Evaluate(expression);
        Assert.Equivalent(expected, result.value);
    }

    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("4 + 3 / 2", 5)]
    [InlineData("4 + 5 % 3", 6)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("(4 + 3) / 2", 3)]
    [InlineData("(4 + 5) % 3", 0)]
    [InlineData("-1 + 2 * 3", 5)]
    [InlineData("-(1 + 2) * 3", -9)]
    [InlineData("(-1 + 2) * 3", 3)]
    [InlineData("not true or true ", true)]
    [InlineData("not (true or true)", false)]
    public void ExpressionPrecedenceAndParenthesis(string expression, object? expected)
    {
        var result = LapisInterpreter.Evaluate(expression);
        Assert.Equivalent(expected, result.value);
    }
}
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

    [Theory]
    [InlineData("@integer", RuneKind.Type)]
    [InlineData("@string", RuneKind.Type)]
    [InlineData("@boolean", RuneKind.Type)]
    [InlineData("@decimal", RuneKind.Type)]
    [InlineData("@", RuneKind.Namespace)]
    public void NameExpressions(string expression, RuneKind runeKind)
    {
        var result = LapisInterpreter.Evaluate(expression);
        var value = Assert.IsAssignableFrom<Rune>(result.value);
        Assert.Equal(runeKind, value.Kind);
        Assert.Equal(expression, value.FullName);
    }

    [Theory]
    [InlineData("var a: @integer = 1;", "a", 1)]
    [InlineData("var a: @integer = 0;", "a", 0)]
    [InlineData("var a: @integer = -1;", "a", -1)]
    [InlineData("var a: @decimal = 1.0;", "a", 1.0)]
    [InlineData("var a: @decimal = 0.0;", "a", 0.0)]
    [InlineData("var a: @decimal = -1.0;", "a", -1.0)]
    [InlineData("var a: @boolean = true;", "a", true)]
    [InlineData("var a: @boolean = false;", "a", false)]
    [InlineData("var a: @string = 'gabriel';", "a", "gabriel")]
    public void VariableDeclaration(string declaration, string evaluation, object? expected)
    {
        var context = new EvaluationContext(LangDefaults.RootNamespace());
        var declarationResult = LapisInterpreter.Evaluate(declaration, context);
        Assert.Null(declarationResult.value);

        var evaluationResult = LapisInterpreter.Evaluate(evaluation, context);

        Assert.Equivalent(expected, evaluationResult.value);
    }

    [Fact]
    public void DeclareAndInstantiateTypes()
    {
        var context = new EvaluationContext(LangDefaults.RootNamespace());
        EvaluationResult result;

        result = LapisInterpreter.Evaluate("var Point: @type = type { x: @decimal; y: @decimal; };", context);
        result = LapisInterpreter.Evaluate("var Line: @type = type { a: Point; b: Point; };", context);
        result = LapisInterpreter.Evaluate("var pointA: @Point = @Point { x: 1.0; y: 2.0; };", context);
        result = LapisInterpreter.Evaluate("var pointB: @Point = @Point { x: 3.0; y: 4.0; };", context);
        result = LapisInterpreter.Evaluate("var line: @Line = @Line { a: pointA; b: pointB; };", context);
        result = LapisInterpreter.Evaluate("line.a.x + line.a.y + line.b.x + line.b.y", context);
        Assert.Equivalent(10.0, result.value);
    }
}
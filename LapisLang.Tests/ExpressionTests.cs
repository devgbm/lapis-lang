using System;
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
    [InlineData("integer", typeof(TypeSymbol))]
    [InlineData("string", typeof(TypeSymbol))]
    [InlineData("boolean", typeof(TypeSymbol))]
    [InlineData("decimal", typeof(TypeSymbol))]
    [InlineData("@", typeof(ScopeSymbol))]
    public void NameExpressions(string expression, Type runeType)
    {
        var result = LapisInterpreter.Evaluate(expression);
        Assert.IsType(runeType, result.value);
    }

    [Theory]
    [InlineData("def a = 1;", "a", 1)]
    [InlineData("def a = 0;", "a", 0)]
    [InlineData("def a = -1;", "a", -1)]
    [InlineData("def a = 1.0;", "a", 1.0)]
    [InlineData("def a = 0.0;", "a", 0.0)]
    [InlineData("def a = -1.0;", "a", -1.0)]
    [InlineData("def a = true;", "a", true)]
    [InlineData("def a = false;", "a", false)]
    [InlineData("def a = 'gabriel';", "a", "gabriel")]
    public void VariableDeclaration(string declaration, string evaluation, object? expected)
    {
        var scope = DefaultSymbols.CreateDefaultScope();
        var declarationResult = LapisInterpreter.Evaluate(declaration, scope);
        Assert.Null(declarationResult.value);

        var evaluationResult = LapisInterpreter.Evaluate(evaluation, scope);

        Assert.Equivalent(expected, evaluationResult.value);
    }

    [Fact]
    public void DeclareAndInstantiateTypes()
    {
        var scope = DefaultSymbols.CreateDefaultScope();
        EvaluationResult result;

        result = LapisInterpreter.Evaluate("def Point = type { x: decimal; y: decimal; };", scope);
        result = LapisInterpreter.Evaluate("def Line = type { a: Point; b: Point; };", scope);
        result = LapisInterpreter.Evaluate("def pointA = Point { x: 1.0; y: 2.0; };", scope);
        result = LapisInterpreter.Evaluate("def pointB = Point { x: 3.0; y: 4.0; };", scope);
        result = LapisInterpreter.Evaluate("def line = Line { a: pointA; b: pointB; };", scope);
        result = LapisInterpreter.Evaluate("line.a.x + line.a.y + line.b.x + line.b.y", scope);
        Assert.Equivalent(10.0, result.value);
    }

    [Fact]
    public void DeclareAndRunFunctions()
    {
        // var context = new EvaluationContext(LangDefaults.RootNamespace());
        // EvaluationResult result;

        // result = LapisInterpreter.Evaluate("var add: @function = func(@integer left, @integer right) @integer { return left + right; };", context);
        // result = LapisInterpreter.Evaluate("add(3.0, 4.5)", context);
        // Assert.Equivalent(10.0, result.value);
    }
}
using LapisLang.Core;

namespace LapisLang.Tests;

public class DefinitionTests
{
    [Fact]
    public void Def_Integer_Can_Be_Retrieved()
    {
        var scope = new EvaluationScope();
        H.Eval("def x = 42;", scope);
        Assert.Equal(42L, H.EvalAs<IntegerSymbol>("x", scope).Value);
    }

    [Fact]
    public void Def_Boolean_Can_Be_Retrieved()
    {
        var scope = new EvaluationScope();
        H.Eval("def flag = true;", scope);
        Assert.True(H.EvalAs<BooleanSymbol>("flag", scope).Value);
    }

    [Fact]
    public void Def_String_Can_Be_Retrieved()
    {
        var scope = new EvaluationScope();
        H.Eval("def greeting = 'hello';", scope);
        Assert.Equal("hello", H.EvalAs<StringSymbol>("greeting", scope).Value);
    }

    [Fact]
    public void Def_Expression_Is_Evaluated()
    {
        var scope = new EvaluationScope();
        H.Eval("def result = 2 + 3;", scope);
        Assert.Equal(5L, H.EvalAs<IntegerSymbol>("result", scope).Value);
    }

    [Fact]
    public void Def_Cannot_Be_Redeclared()
    {
        var scope = new EvaluationScope();
        H.Eval("def x = 1;", scope);
        Assert.True(H.HasErrors("def x = 2;", scope));
    }

    [Fact]
    public void Undefined_Name_Produces_Error()
    {
        Assert.True(H.HasErrors("undefinedVariable"));
    }
}

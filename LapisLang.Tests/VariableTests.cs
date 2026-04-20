using LapisLang.Core;

namespace LapisLang.Tests;

public class VariableTests
{
    [Fact]
    public void Var_Integer_Can_Be_Retrieved()
    {
        var scope = new EvaluationScope();
        H.Eval("var x = 10;", scope);
        Assert.Equal(10L, H.EvalAs<IntegerSymbol>("x", scope).Value);
    }

    [Fact]
    public void Var_Can_Be_Reassigned()
    {
        var scope = new EvaluationScope();
        H.Eval("var x = 10;", scope);
        H.Eval("x = 20;", scope);
        Assert.Equal(20L, H.EvalAs<IntegerSymbol>("x", scope).Value);
    }

    [Fact]
    public void Var_Cannot_Be_Assigned_Wrong_Type()
    {
        var scope = new EvaluationScope();
        H.Eval("var x = 10;", scope);
        Assert.True(H.HasErrors("x = true;", scope));
    }

    [Fact]
    public void Var_Cannot_Hold_Type_Reference()
    {
        Assert.True(H.HasErrors("var t = Int;"));
    }
}

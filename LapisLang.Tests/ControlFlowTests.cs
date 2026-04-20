using LapisLang.Core;

namespace LapisLang.Tests;

public class ControlFlowTests
{
    [Fact]
    public void If_True_Branch_Executes()
    {
        var scope = new EvaluationScope();
        H.Eval("def max = func (Int a, Int b) Int: { if (a > b) { return a; } else { return b; } };", scope);
        Assert.Equal(5L, H.EvalAs<IntegerSymbol>("max(5, 3)", scope).Value);
    }

    [Fact]
    public void If_False_Branch_Executes_Else()
    {
        var scope = new EvaluationScope();
        H.Eval("def max = func (Int a, Int b) Int: { if (a > b) { return a; } else { return b; } };", scope);
        Assert.Equal(7L, H.EvalAs<IntegerSymbol>("max(3, 7)", scope).Value);
    }

    [Fact]
    public void If_Equal_Values()
    {
        var scope = new EvaluationScope();
        H.Eval("def max = func (Int a, Int b) Int: { if (a > b) { return a; } else { return b; } };", scope);
        Assert.Equal(4L, H.EvalAs<IntegerSymbol>("max(4, 4)", scope).Value);
    }

    [Fact]
    public void If_With_Bool_Result()
    {
        var scope = new EvaluationScope();
        H.Eval("def isPositive = func (Int n) Bool: { if (n > 0) { return true; } else { return false; } };", scope);
        Assert.True(H.EvalAs<BooleanSymbol>("isPositive(5)", scope).Value);
        Assert.False(H.EvalAs<BooleanSymbol>("isPositive(-1)", scope).Value);
    }

    [Fact]
    public void If_Condition_Must_Be_Boolean()
    {
        Assert.True(H.HasErrors("(func () Int: { if (1) { return 1; } else { return 2; } })()"));
    }
}

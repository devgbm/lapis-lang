using LapisLang.Core;

namespace LapisLang.Tests;

public class FunctionTests
{
    [Fact]
    public void Function_Returns_Constant()
    {
        var result = H.EvalAs<IntegerSymbol>("(func () Int: { return 42; })()");
        Assert.Equal(42L, result.Value);
    }

    [Fact]
    public void Function_With_One_Arg_Returns_Arg()
    {
        var result = H.EvalAs<IntegerSymbol>("(func (Int a) Int: { return a; })(7)");
        Assert.Equal(7L, result.Value);
    }

    [Fact]
    public void Function_Adds_Two_Integers()
    {
        var result = H.EvalAs<IntegerSymbol>("(func (Int a, Int b) Int: { return a + b; })(3, 4)");
        Assert.Equal(7L, result.Value);
    }

    [Fact]
    public void Function_With_Bool_Return()
    {
        var result = H.EvalAs<BooleanSymbol>("(func (Int a, Int b) Bool: { return a > b; })(5, 3)");
        Assert.True(result.Value);
    }

    [Fact]
    public void Named_Function_Can_Be_Called()
    {
        var scope = new EvaluationScope();
        H.Eval("def double = func (Int n) Int: { return n + n; };", scope);
        Assert.Equal(10L, H.EvalAs<IntegerSymbol>("double(5)", scope).Value);
    }

    [Fact]
    public void Named_Function_Multiple_Calls()
    {
        var scope = new EvaluationScope();
        H.Eval("def square = func (Int n) Int: { return n * n; };", scope);
        Assert.Equal(9L, H.EvalAs<IntegerSymbol>("square(3)", scope).Value);
        Assert.Equal(25L, H.EvalAs<IntegerSymbol>("square(5)", scope).Value);
    }

    [Fact]
    public void Function_Wrong_Arg_Count_Produces_Error()
    {
        Assert.True(H.HasErrors("(func (Int a) Int: { return a; })(1, 2)"));
    }

    [Fact]
    public void Function_Wrong_Arg_Type_Produces_Error()
    {
        Assert.True(H.HasErrors("(func (Int a) Int: { return a; })(true)"));
    }

    [Fact]
    public void Function_Has_Function_Type()
    {
        var scope = new EvaluationScope();
        H.Eval("def f = func (Int a) Int: { return a; };", scope);
        var result = H.Eval("f", scope);
        Assert.IsType<FuncSymbol>(result);
    }
}

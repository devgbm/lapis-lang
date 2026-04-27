using LapisLang.Core;

namespace LapisLang.Tests;

public class EnumMethodTests
{
    // ── static methods ────────────────────────────────────────────────────

    [Fact]
    public void Static_Method_On_Enum_Type()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def OptInt.empty = func() OptInt: { return OptInt.None; };", scope);
        var result = H.Eval("OptInt.empty()", scope);
        Assert.IsType<EnumInstanceSymbol>(result);
        Assert.Equal("None", ((EnumInstanceSymbol)result).VariantName);
    }

    [Fact]
    public void Static_Method_Returns_Data_Variant()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def OptInt.of = func(Int v) OptInt: { return OptInt.Some(v); };", scope);
        var result = (EnumInstanceSymbol)H.Eval("OptInt.of(7)", scope);
        Assert.Equal("Some", result.VariantName);
        Assert.Equal(7L, ((IntegerSymbol)result.Fields["value"]).Value);
    }

    // ── instance methods ──────────────────────────────────────────────────

    [Fact]
    public void Instance_Method_Definition_And_Call()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def OptInt.isNone = func(self) Bool: { return self == OptInt.None; };", scope);
        var result = H.EvalAs<BooleanSymbol>("OptInt.None.isNone()", scope);
        Assert.True(result.Value);
    }

    [Fact]
    public void Instance_Method_Returns_False_For_Data_Variant()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def OptInt.isNone = func(self) Bool: { return self == OptInt.None; };", scope);
        var result = H.EvalAs<BooleanSymbol>("OptInt.Some(1).isNone()", scope);
        Assert.False(result.Value);
    }

    [Fact]
    public void Instance_Method_On_Variable()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def OptInt.isNone = func(self) Bool: { return self == OptInt.None; };", scope);
        H.Eval("var x = OptInt.None;", scope);
        var result = H.EvalAs<BooleanSymbol>("x.isNone()", scope);
        Assert.True(result.Value);
    }

    [Fact]
    public void Instance_Method_Returns_Int_Result()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def OptInt.tag = func(self) Int: { if(self == OptInt.None) { return 0; } return 1; };", scope);
        var noneTag = H.EvalAs<IntegerSymbol>("OptInt.None.tag()", scope);
        var someTag = H.EvalAs<IntegerSymbol>("OptInt.Some(5).tag()", scope);
        Assert.Equal(0L, noneTag.Value);
        Assert.Equal(1L, someTag.Value);
    }

    // ── field access inside method ────────────────────────────────────────

    [Fact]
    public void Instance_Method_Accesses_Data_Variant_Field()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def OptInt.unwrapOr = func(self, Int default) Int: { if(self == OptInt.None) { return default; } return self.value; };", scope);
        var result = H.EvalAs<IntegerSymbol>("OptInt.Some(42).unwrapOr(0)", scope);
        Assert.Equal(42L, result.Value);
    }

    [Fact]
    public void Instance_Method_Returns_Default_When_None()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def OptInt.unwrapOr = func(self, Int default) Int: { if(self == OptInt.None) { return default; } return self.value; };", scope);
        var result = H.EvalAs<IntegerSymbol>("OptInt.None.unwrapOr(99)", scope);
        Assert.Equal(99L, result.Value);
    }
}

using LapisLang.Core;

namespace LapisLang.Tests;

public class EnumEqualityTests
{
    // ── unit variant equality ──────────────────────────────────────────────

    [Fact]
    public void Unit_Variants_Equal_When_Same()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("var x = OptInt.None;", scope);
        var result = H.EvalAs<BooleanSymbol>("x == OptInt.None", scope);
        Assert.True(result.Value);
    }

    [Fact]
    public void Unit_Variants_Not_Equal_When_Different()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("var x = OptInt.Some(1);", scope);
        var result = H.EvalAs<BooleanSymbol>("x == OptInt.None", scope);
        Assert.False(result.Value);
    }

    [Fact]
    public void BangEquals_Unit_Variants()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("var x = OptInt.None;", scope);
        var result = H.EvalAs<BooleanSymbol>("x != OptInt.None", scope);
        Assert.False(result.Value);
    }

    // ── data variant equality ─────────────────────────────────────────────

    [Fact]
    public void Data_Variants_Equal_When_Same_Value()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = H.EvalAs<BooleanSymbol>("OptInt.Some(42) == OptInt.Some(42)", scope);
        Assert.True(result.Value);
    }

    [Fact]
    public void Data_Variants_Not_Equal_When_Different_Value()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = H.EvalAs<BooleanSymbol>("OptInt.Some(1) == OptInt.Some(2)", scope);
        Assert.False(result.Value);
    }

    [Fact]
    public void Data_Variant_Not_Equal_To_Unit_Variant()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = H.EvalAs<BooleanSymbol>("OptInt.Some(1) == OptInt.None", scope);
        Assert.False(result.Value);
    }

    // ── equality inside if (via function return) ──────────────────────────

    [Fact]
    public void Equality_In_If_Takes_True_Branch()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def check = func(OptInt x) Int: { if(x == OptInt.None) { return 1; } return 0; };", scope);
        var result = H.EvalAs<IntegerSymbol>("check(OptInt.None)", scope);
        Assert.Equal(1L, result.Value);
    }

    [Fact]
    public void Equality_In_If_Takes_Else_Branch()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def check = func(OptInt x) Int: { if(x == OptInt.None) { return 1; } return 2; };", scope);
        var result = H.EvalAs<IntegerSymbol>("check(OptInt.Some(5))", scope);
        Assert.Equal(2L, result.Value);
    }
}

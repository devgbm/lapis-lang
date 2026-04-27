using LapisLang.Core;

namespace LapisLang.Tests;

// Validates variant-tag checks: `x == OptInt.Some` tests whether x carries
// the Some variant regardless of its payload, and `x.value` retrieves the field
// after the check confirms the variant.
public class EnumVariantCheckTests
{
    // ── basic variant tag equality ─────────────────────────────────────────

    [Fact]
    public void Variant_Tag_Check_True_For_Matching_Variant()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = H.EvalAs<BooleanSymbol>("OptInt.Some(5) == OptInt.Some", scope);
        Assert.True(result.Value);
    }

    [Fact]
    public void Variant_Tag_Check_False_For_Different_Variant()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = H.EvalAs<BooleanSymbol>("OptInt.None == OptInt.Some", scope);
        Assert.False(result.Value);
    }

    [Fact]
    public void Variant_Tag_Check_BangEquals()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = H.EvalAs<BooleanSymbol>("OptInt.Some(5) != OptInt.Some", scope);
        Assert.False(result.Value);
    }

    [Fact]
    public void Variant_Tag_Check_BangEquals_Different_Variant()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = H.EvalAs<BooleanSymbol>("OptInt.None != OptInt.Some", scope);
        Assert.True(result.Value);
    }

    // ── variant check with variable ────────────────────────────────────────

    [Fact]
    public void Variant_Tag_Check_On_Variable_Some()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("var x = OptInt.Some(42);", scope);
        var result = H.EvalAs<BooleanSymbol>("x == OptInt.Some", scope);
        Assert.True(result.Value);
    }

    [Fact]
    public void Variant_Tag_Check_On_Variable_None()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("var x = OptInt.None;", scope);
        var result = H.EvalAs<BooleanSymbol>("x == OptInt.Some", scope);
        Assert.False(result.Value);
    }

    // ── if branch + field access ───────────────────────────────────────────

    [Fact]
    public void If_Variant_Check_Enters_True_Branch()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def getOrZero = func(OptInt x) Int: { if(x == OptInt.Some) { return x.value; } return 0; };", scope);
        var result = H.EvalAs<IntegerSymbol>("getOrZero(OptInt.Some(7))", scope);
        Assert.Equal(7L, result.Value);
    }

    [Fact]
    public void If_Variant_Check_Enters_Else_Branch()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def getOrZero = func(OptInt x) Int: { if(x == OptInt.Some) { return x.value; } return 0; };", scope);
        var result = H.EvalAs<IntegerSymbol>("getOrZero(OptInt.None)", scope);
        Assert.Equal(0L, result.Value);
    }

    [Fact]
    public void Field_Access_After_Variant_Check_Returns_Correct_Value()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("def getValue = func(OptInt x) Int: { if(x == OptInt.Some) { return x.value; } return -1; };", scope);
        Assert.Equal(99L,  H.EvalAs<IntegerSymbol>("getValue(OptInt.Some(99))", scope).Value);
        Assert.Equal(-1L, H.EvalAs<IntegerSymbol>("getValue(OptInt.None)", scope).Value);
    }

    // ── multi-variant enum ─────────────────────────────────────────────────

    [Fact]
    public void Variant_Check_On_Three_Variant_Enum()
    {
        var scope = new EvaluationScope();
        H.Eval("def Shape = enum { Circle(Int radius), Rect(Int w, Int h), Empty };", scope);
        H.Eval("def area = func(Shape s) Int: { if(s == Shape.Circle) { return s.radius; } if(s == Shape.Rect) { return s.w; } return 0; };", scope);
        Assert.Equal(5L,  H.EvalAs<IntegerSymbol>("area(Shape.Circle(5))", scope).Value);
        Assert.Equal(4L,  H.EvalAs<IntegerSymbol>("area(Shape.Rect(4, 3))", scope).Value);
        Assert.Equal(0L,  H.EvalAs<IntegerSymbol>("area(Shape.Empty)", scope).Value);
    }
}

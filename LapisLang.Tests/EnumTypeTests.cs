using LapisLang.Core;

namespace LapisLang.Tests;

public class EnumTypeTests
{
    [Fact]
    public void Enum_Expression_Returns_EnumTypeSymbol()
    {
        var result = H.Eval("enum { None, Some(Int value) }");
        Assert.IsType<EnumTypeSymbol>(result);
    }

    [Fact]
    public void Enum_Has_Correct_Variant_Count()
    {
        var result = (EnumTypeSymbol)H.Eval("enum { None, Some(Int value) }");
        Assert.Equal(2, result.Variants.Length);
    }

    [Fact]
    public void Enum_Variant_Names_Are_Correct()
    {
        var result = (EnumTypeSymbol)H.Eval("enum { None, Some(Int value) }");
        Assert.Equal("None", result.Variants[0].Name);
        Assert.Equal("Some", result.Variants[1].Name);
    }

    [Fact]
    public void Unit_Variant_Has_No_Fields()
    {
        var result = (EnumTypeSymbol)H.Eval("enum { None, Some(Int value) }");
        Assert.Empty(result.Variants[0].Fields);
    }

    [Fact]
    public void Data_Variant_Has_Correct_Fields()
    {
        var result = (EnumTypeSymbol)H.Eval("enum { None, Some(Int value) }");
        Assert.Single(result.Variants[1].Fields);
        Assert.Equal("value", result.Variants[1].Fields[0].Name);
        Assert.Equal(LangDefaults.Types.Integer, result.Variants[1].Fields[0].Type);
    }

    [Fact]
    public void Enum_Type_Has_Type_Kind()
    {
        var result = H.Eval("enum { None }");
        Assert.Equal(LangDefaults.Types.Type, result.Type);
    }

    [Fact]
    public void Unit_Variant_Access_Returns_EnumInstanceSymbol()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = H.Eval("OptInt.None", scope);
        Assert.IsType<EnumInstanceSymbol>(result);
    }

    [Fact]
    public void Unit_Variant_Has_Correct_VariantName()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = (EnumInstanceSymbol)H.Eval("OptInt.None", scope);
        Assert.Equal("None", result.VariantName);
    }

    [Fact]
    public void Unit_Variant_Has_Correct_EnumType()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = (EnumInstanceSymbol)H.Eval("OptInt.None", scope);
        Assert.Equal("OptInt", result.EnumType.DebugName);
    }

    [Fact]
    public void Data_Variant_Constructor_Returns_EnumInstanceSymbol()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = H.Eval("OptInt.Some(42)", scope);
        Assert.IsType<EnumInstanceSymbol>(result);
    }

    [Fact]
    public void Data_Variant_Has_Correct_VariantName()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = (EnumInstanceSymbol)H.Eval("OptInt.Some(42)", scope);
        Assert.Equal("Some", result.VariantName);
    }

    [Fact]
    public void Data_Variant_Has_Correct_Field_Value()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = (EnumInstanceSymbol)H.Eval("OptInt.Some(42)", scope);
        Assert.Equal(42L, ((IntegerSymbol)result.Fields["value"]).Value);
    }

    [Fact]
    public void Enum_Instance_Type_Is_EnumType()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        var result = (EnumInstanceSymbol)H.Eval("OptInt.Some(7)", scope);
        Assert.IsType<EnumTypeSymbol>(result.Type);
    }

    [Fact]
    public void Enum_With_Multiple_Fields_Per_Variant()
    {
        var scope = new EvaluationScope();
        H.Eval("def Point2D = enum { Origin, At(Int x, Int y) };", scope);
        var result = (EnumInstanceSymbol)H.Eval("Point2D.At(3, 4)", scope);
        Assert.Equal("At", result.VariantName);
        Assert.Equal(3L, ((IntegerSymbol)result.Fields["x"]).Value);
        Assert.Equal(4L, ((IntegerSymbol)result.Fields["y"]).Value);
    }

    [Fact]
    public void Enum_Stored_In_Variable()
    {
        var scope = new EvaluationScope();
        H.Eval("def OptInt = enum { None, Some(Int value) };", scope);
        H.Eval("var x = OptInt.Some(99);", scope);
        var result = (EnumInstanceSymbol)H.Eval("x", scope);
        Assert.Equal("Some", result.VariantName);
        Assert.Equal(99L, ((IntegerSymbol)result.Fields["value"]).Value);
    }
}

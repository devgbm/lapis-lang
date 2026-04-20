using LapisLang.Core;

namespace LapisLang.Tests;

public class StructTypeTests
{
    [Fact]
    public void Type_Expression_Returns_StructTypeSymbol()
    {
        var result = H.Eval("type { x: Int; }");
        Assert.IsType<StructTypeSymbol>(result);
    }

    [Fact]
    public void Type_Expression_Has_Correct_Field_Count()
    {
        var result = (StructTypeSymbol)H.Eval("type { x: Int; y: Int; }");
        Assert.Equal(2, result.Fields.Length);
    }

    [Fact]
    public void Type_Expression_Has_Correct_Field_Names()
    {
        var result = (StructTypeSymbol)H.Eval("type { name: Str; age: Int; }");
        Assert.Equal("name", result.Fields[0].Name);
        Assert.Equal("age", result.Fields[1].Name);
    }

    [Fact]
    public void Type_Expression_Has_Correct_Field_Types()
    {
        var result = (StructTypeSymbol)H.Eval("type { name: Str; age: Int; }");
        Assert.Equal(LangDefaults.Types.String, result.Fields[0].Type);
        Assert.Equal(LangDefaults.Types.Integer, result.Fields[1].Type);
    }

    [Fact]
    public void Type_Symbol_Has_Type_Kind()
    {
        var result = H.Eval("type { x: Int; }");
        Assert.Equal(LangDefaults.Types.Type, result.Type);
    }

    [Fact]
    public void Anonymous_Instance_Returns_InstanceSymbol()
    {
        var result = H.Eval(".{ x: 1; y: 2 }");
        Assert.IsType<InstanceSymbol>(result);
    }

    [Fact]
    public void Anonymous_Instance_Has_Correct_Attribute_Values()
    {
        var result = (InstanceSymbol)H.Eval(".{ x: 10; y: 20 }");
        Assert.Equal(10L, ((IntegerSymbol)result.Atributes["x"]).Value);
        Assert.Equal(20L, ((IntegerSymbol)result.Atributes["y"]).Value);
    }

    [Fact]
    public void Member_Access_On_Instance()
    {
        var result = H.EvalAs<IntegerSymbol>(".{ x: 99; y: 0 }.x");
        Assert.Equal(99L, result.Value);
    }

    [Fact]
    public void Member_Access_String_Field()
    {
        var result = H.EvalAs<StringSymbol>(".{ name: 'Alice'; age: 30 }.name");
        Assert.Equal("Alice", result.Value);
    }

    [Fact]
    public void Named_Instance_With_Defined_Type()
    {
        var scope = new EvaluationScope();
        H.Eval("def Point = type { x: Int; y: Int; };", scope);
        var result = (InstanceSymbol)H.Eval("Point { x: 3; y: 4 }", scope);
        Assert.Equal(3L, ((IntegerSymbol)result.Atributes["x"]).Value);
        Assert.Equal(4L, ((IntegerSymbol)result.Atributes["y"]).Value);
    }

    [Fact]
    public void Named_Instance_Member_Access()
    {
        var scope = new EvaluationScope();
        H.Eval("def Point = type { x: Int; y: Int; };", scope);
        var result = H.EvalAs<IntegerSymbol>("Point { x: 3; y: 4 }.y", scope);
        Assert.Equal(4L, result.Value);
    }

    [Fact]
    public void Function_Accepting_Struct_Instance()
    {
        var scope = new EvaluationScope();
        H.Eval("def Point = type { x: Int; y: Int; };", scope);
        H.Eval("def getX = func (Point p) Int: { return p.x; };", scope);
        var result = H.EvalAs<IntegerSymbol>("getX(Point { x: 7; y: 0 })", scope);
        Assert.Equal(7L, result.Value);
    }
}

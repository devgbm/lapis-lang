using LapisLang.Core;

namespace LapisLang.Tests;

public class MethodTests
{
    [Fact]
    public void Static_Method_Definition_And_Call()
    {
        var scope = new EvaluationScope();
        H.Eval("def Int.add = func(Int a, Int b) Int: { return a + b; };", scope);
        var result = H.EvalAs<IntegerSymbol>("Int.add(1, 2)", scope);
        Assert.Equal(3L, result.Value);
    }

    [Fact]
    public void Static_Method_No_Params()
    {
        var scope = new EvaluationScope();
        H.Eval("def Int.zero = func() Int: { return 0; };", scope);
        var result = H.EvalAs<IntegerSymbol>("Int.zero()", scope);
        Assert.Equal(0L, result.Value);
    }

    [Fact]
    public void Instance_Method_On_Primitive()
    {
        var scope = new EvaluationScope();
        H.Eval("def Int.plus = func(self, Int b) Int: { return self + b; };", scope);
        var result = H.EvalAs<IntegerSymbol>("1.plus(2)", scope);
        Assert.Equal(3L, result.Value);
    }

    [Fact]
    public void Instance_Method_No_Extra_Params()
    {
        var scope = new EvaluationScope();
        H.Eval("def Int.doubled = func(self) Int: { return self + self; };", scope);
        var result = H.EvalAs<IntegerSymbol>("5.doubled()", scope);
        Assert.Equal(10L, result.Value);
    }

    [Fact]
    public void Instance_Method_On_Struct()
    {
        var scope = new EvaluationScope();
        H.Eval("def Pt = type { x: Int; y: Int; };", scope);
        H.Eval("def Pt.sum = func(self) Int: { return self.x + self.y; };", scope);
        var result = H.EvalAs<IntegerSymbol>("Pt{ x: 3; y: 4; }.sum()", scope);
        Assert.Equal(7L, result.Value);
    }

    [Fact]
    public void Method_Definition_Reports_Error_For_Unknown_Type()
    {
        Assert.True(H.HasErrors("def UnknownType.foo = func() Int: { return 1; };"));
    }

    [Fact]
    public void Static_Constant_Int_On_Primitive_Type()
    {
        var scope = new EvaluationScope();
        H.Eval("def Int.PiDigits = 3141592;", scope);
        var result = H.EvalAs<IntegerSymbol>("Int.PiDigits", scope);
        Assert.Equal(3141592L, result.Value);
    }

    [Fact]
    public void Static_Constant_Str_On_Primitive_Type()
    {
        var scope = new EvaluationScope();
        H.Eval("def Str.Empty = '';", scope);
        var result = H.EvalAs<StringSymbol>("Str.Empty", scope);
        Assert.Equal("", result.Value);
    }

    [Fact]
    public void Static_Constant_Bool_On_Primitive_Type()
    {
        var scope = new EvaluationScope();
        H.Eval("def Bool.Yes = true;", scope);
        var result = H.EvalAs<BooleanSymbol>("Bool.Yes", scope);
        Assert.True(result.Value);
    }

    [Fact]
    public void Static_Constant_On_User_Defined_Type()
    {
        var scope = new EvaluationScope();
        H.Eval("def Pt = type { x: Int; y: Int; };", scope);
        H.Eval("def Pt.Origin = Pt{ x: 0; y: 0; };", scope);
        var x = H.EvalAs<IntegerSymbol>("Pt.Origin.x", scope);
        Assert.Equal(0L, x.Value);
    }

    [Fact]
    public void Static_Constant_Accessible_Via_Def()
    {
        var scope = new EvaluationScope();
        H.Eval("def Int.MaxSmall = 100;", scope);
        H.Eval("def limit = Int.MaxSmall;", scope);
        var result = H.EvalAs<IntegerSymbol>("limit", scope);
        Assert.Equal(100L, result.Value);
    }

    [Fact]
    public void Static_Constant_Duplicate_Definition_Reports_Error()
    {
        var scope = new EvaluationScope();
        H.Eval("def Int.Tag = 1;", scope);
        Assert.True(H.HasErrors("def Int.Tag = 2;", scope));
    }

    [Fact]
    public void Nested_Call_Resolve_Target_Correctly()
    {
        var scope = new EvaluationScope();
        H.Eval("def Pt = type { x: Int; y: Int; };", scope);
        H.Eval("def Pt.avg = func(self, Pt other) Pt :{ return Pt{ x: (self.x + other.x) / 2; y: (self.y + other.y) / 2; }; };", scope);
        H.Eval("def Pt.create = func(Int x, Int y) Pt: { return Pt { x: x; y: y; }; };", scope);
        H.Eval("def Pt.sum = func(self) Int: { return self.x + self.y; };", scope);
        var result = H.EvalAs<IntegerSymbol>("Pt.create(0,0).avg(Pt.create(2, 2)).sum()", scope);
        Assert.Equal(2L, result.Value);
    }
}

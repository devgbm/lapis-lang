using LapisLang.Core;

namespace LapisLang.Tests;

public class NativeFuncTests
{
    // --- Int.toString (built-in native instance method) ---

    [Fact]
    public void NativeInstance_Int_ToString_Positive()
    {
        var result = H.EvalAs<StringSymbol>("42.toString()");
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void NativeInstance_Int_ToString_Zero()
    {
        var result = H.EvalAs<StringSymbol>("0.toString()");
        Assert.Equal("0", result.Value);
    }

    [Fact]
    public void NativeInstance_Int_ToString_Negative()
    {
        var result = H.EvalAs<StringSymbol>("(-7).toString()");
        Assert.Equal("-7", result.Value);
    }

    [Fact]
    public void NativeInstance_Int_ToString_Large()
    {
        var result = H.EvalAs<StringSymbol>("1000000.toString()");
        Assert.Equal("1000000", result.Value);
    }

    // --- Custom native instance method on a struct type (via C#) ---

    [Fact]
    public void NativeInstance_Custom_On_Struct()
    {
        var scope = new EvaluationScope();
        H.Eval("def Vec = type { x: Int; y: Int; };", scope);

        var vecType = (TypeSymbol)scope.GetVariable("Vec");
        var funcType = new FuncTypeSymbol(
            [new ParameterSymbol("self", vecType, false)],
            LangDefaults.Types.Integer,
            BindFlag.IsInstance);
        var native = new NativeFuncSymbol(funcType,
            (ExprSymbol self) =>
            {
                var inst = (InstanceSymbol)self;
                var x = ((IntegerSymbol)inst.Atributes["x"]).Value;
                var y = ((IntegerSymbol)inst.Atributes["y"]).Value;
                return x + y;
            });
        RegisterNativeOnStructType(scope, "Vec", "manhattanLen", native);

        var result = H.EvalAs<IntegerSymbol>("Vec{ x: 3; y: 4; }.manhattanLen()", scope);
        Assert.Equal(7L, result.Value);
    }

    [Fact]
    public void NativeInstance_Custom_On_Struct_With_Arg()
    {
        var scope = new EvaluationScope();
        H.Eval("def Box = type { w: Int; h: Int; };", scope);

        var boxType = (TypeSymbol)scope.GetVariable("Box");
        var funcType = new FuncTypeSymbol(
            [new ParameterSymbol("self", boxType, false), new ParameterSymbol("scale", LangDefaults.Types.Integer, true)],
            LangDefaults.Types.Integer,
            BindFlag.IsInstance);
        var native = new NativeFuncSymbol(funcType,
            (ExprSymbol self, object? scale) =>
            {
                var inst = (InstanceSymbol)self;
                var w = ((IntegerSymbol)inst.Atributes["w"]).Value;
                var h = ((IntegerSymbol)inst.Atributes["h"]).Value;
                return w * h * (long)scale!;
            });
        RegisterNativeOnStructType(scope, "Box", "scaledArea", native);

        var result = H.EvalAs<IntegerSymbol>("Box{ w: 3; h: 4; }.scaledArea(2)", scope);
        Assert.Equal(24L, result.Value);
    }

    // --- Native static function on a namespace (Console.write) ---

    [Fact]
    public void NativeStatic_Console_Log_Does_Not_Throw()
    {
        var ex = Record.Exception(() => H.Eval("Console.write('hello')"));
        Assert.Null(ex);
    }

    // --- Custom native static method registered on a struct type (via C#) ---

    [Fact]
    public void NativeStatic_Custom_On_Struct_Type()
    {
        var scope = new EvaluationScope();
        H.Eval("def Color = type { r: Int; g: Int; b: Int; };", scope);

        var funcType = new FuncTypeSymbol([], LangDefaults.Types.Integer);
        var native = new NativeFuncSymbol(funcType,
            () => 0xFF000000L);
        RegisterNativeOnStructType(scope, "Color", "Black", native);

        var result = H.EvalAs<IntegerSymbol>("Color.Black()", scope);
        Assert.Equal(0xFF000000L, result.Value);
    }

    private static void RegisterNativeOnStructType(
        EvaluationScope scope, string typeName, string memberName, NativeFuncSymbol native)
    {
        ((TypeSymbol)scope.GetVariable(typeName)).DefineMember(memberName, native);
    }
}

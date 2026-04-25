using LapisLang.Core;

namespace LapisLang.Tests;

public class RuntimeOnlyBindingTests
{
    // --- def rejeita expressões IsRuntimeOnly ---

    [Fact]
    public void Def_Console_Write_Call_Produces_Error()
    {
        Assert.True(H.HasErrors("def x = Console.write('hello');"));
    }

    [Fact]
    public void Def_Compiler_Runtime_Call_Produces_Error()
    {
        Assert.True(H.HasErrors("def x = Compiler.runtime();"));
    }

    // --- propagação via expressões compostas ---

    [Fact]
    public void Def_Binary_With_RuntimeOnly_Operand_Produces_Error()
    {
        // IsRuntimeOnly propaga do operando esquerdo para o BinaryExprSymbol
        Assert.True(H.HasErrors("def x = Compiler.runtime() + 1;"));
    }

    [Fact]
    public void Def_Binary_With_RuntimeOnly_Right_Operand_Produces_Error()
    {
        Assert.True(H.HasErrors("def x = 1 + Compiler.runtime();"));
    }

    [Fact]
    public void Def_Chained_Call_With_RuntimeOnly_Receiver_Produces_Error()
    {
        // Compiler.runtime() retorna Int (IsRuntimeOnly); .toString() recebe self IsRuntimeOnly → propaga
        Assert.True(H.HasErrors("def x = Compiler.runtime().toString();"));
    }

    [Fact]
    public void Def_Call_With_RuntimeOnly_Argument_Produces_Error()
    {
        // Int.parse é puro, mas o argumento vem de Compiler.runtime().toString() (IsRuntimeOnly)
        Assert.True(H.HasErrors("def x = Int.parse(Compiler.runtime().toString());"));
    }

    // --- def aceita expressões puramente compile-time ---

    [Fact]
    public void Def_Literal_Is_Accepted()
    {
        Assert.False(H.HasErrors("def x = 42;"));
    }

    [Fact]
    public void Def_Pure_Arithmetic_Is_Accepted()
    {
        Assert.False(H.HasErrors("def x = 2 + 3;"));
    }

    [Fact]
    public void Def_Function_Wrapping_Console_Write_Is_Accepted()
    {
        // A definição da função em si não é IsRuntimeOnly; só a chamada seria
        Assert.False(H.HasErrors("def f = func () Void: { Console.write('hi'); };"));
    }

    [Fact]
    public void Def_Function_Using_Compiler_Runtime_Internally_Is_Accepted()
    {
        // A função usa Compiler.runtime() no corpo, mas o FuncSymbol em si não é IsRuntimeOnly
        Assert.False(H.HasErrors("def f = func () Int: { return Compiler.runtime(); };"));
    }

    // --- var aceita expressões IsRuntimeOnly ---

    [Fact]
    public void Var_RuntimeOnly_Call_Is_Accepted()
    {
        // var não tem a restrição de compile-time do def
        Assert.False(H.HasErrors("var x = Compiler.runtime();"));
    }
}

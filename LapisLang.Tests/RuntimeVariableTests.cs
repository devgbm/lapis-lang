using LapisLang.Core;

namespace LapisLang.Tests;

// Tests that guard against regressions in runtime variable resolution.
//
// Root cause that these tests cover:
//   1. MemberSymbol.IsCompileTime was inherited from the base expression instead of the member,
//      causing calls like `Int.parse(Console.read())` to be incorrectly classified as compile-time.
//   2. CallSymbol.IsCompileTime did not check arguments, so a call whose arguments were runtime
//      was still treated as compile-time even when arguments were not.
//   3. EvaluationScope.GetVariable looked up BoundScope before _parent, returning the unevaluated
//      CallSymbol stored by the binder instead of the runtime value in the parent scope.
//
// Compiler.runtime() is a native function with IsCompileTime=false that always returns 0 at
// runtime. It is used here as a stand-in for Console.read() to trigger the runtime evaluation
// path without blocking on user input.

public class RuntimeVariableTests
{
    [Fact]
    public void Runtime_Variable_Is_Not_Evaluated_At_Compile_Time()
    {
        // If Compiler.runtime() were incorrectly classified as compile-time, the binder would
        // evaluate it during binding, producing VoidSymbol instead of IntegerSymbol.
        var scope = new EvaluationScope();
        H.Eval("def x = Compiler.runtime();", scope);
        var result = H.EvalAs<IntegerSymbol>("x", scope);
        Assert.Equal(0L, result.Value);
    }

    [Fact]
    public void Runtime_Variable_Resolves_In_Function_Scope()
    {
        // Regression for EvaluationScope.GetVariable ordering bug.
        // `x` is runtime, so it lives in the outer scope's _variables dictionary.
        // When `f(x)` is called, the derived scope must walk _parent to find `x`,
        // not BoundScope (which holds the unevaluated CallSymbol from the binder).
        var scope = new EvaluationScope();
        H.Eval("def x = Compiler.runtime();", scope);
        H.Eval("def f = func (Int n) Int: { return n; };", scope);
        var result = H.EvalAs<IntegerSymbol>("f(x)", scope);
        Assert.Equal(0L, result.Value);
    }

    [Fact]
    public void Function_Call_With_Runtime_Argument_Is_Not_Compile_Time()
    {
        // CallSymbol.IsCompileTime must be false when any argument is runtime.
        // Before the fix, only the callable's IsCompileTime was checked, so a call
        // like `addTwo(runtimeValue)` was incorrectly treated as compile-time.
        var scope = new EvaluationScope();
        H.Eval("def x = Compiler.runtime();", scope);
        H.Eval("def addTwo = func (Int n) Int: { return n + 2; };", scope);
        var result = H.EvalAs<IntegerSymbol>("addTwo(x)", scope);
        Assert.Equal(2L, result.Value);
    }

    [Fact]
    public void Runtime_Variable_In_Nested_Function_Calls()
    {
        var scope = new EvaluationScope();
        H.Eval("def x = Compiler.runtime();", scope);
        H.Eval("def addOne = func (Int n) Int: { return n + 1; };", scope);
        var result = H.EvalAs<IntegerSymbol>("addOne(addOne(x))", scope);
        Assert.Equal(2L, result.Value);
    }

    [Fact]
    public void Runtime_Variable_Used_In_Conditional_Inside_Function()
    {
        // Regression for the EvaluateBinaryExpr crash: when `n` resolved to the unevaluated
        // CallSymbol instead of an IntegerSymbol, the binary operator cast would fail.
        var scope = new EvaluationScope();
        H.Eval("def x = Compiler.runtime();", scope);
        H.Eval("def isZero = func (Int n) Bool: { if (n == 0) { return true; } else { return false; } };", scope);
        var result = H.EvalAs<BooleanSymbol>("isZero(x)", scope);
        Assert.True(result.Value);
    }

    [Fact]
    public void Recursive_Function_With_Runtime_Argument()
    {
        // Reproduces the fibonacci crash scenario: a recursive function called with a
        // runtime-defined argument. Compiler.runtime() returns 0, so fib(0) = 0.
        var scope = new EvaluationScope();
        H.Eval("""
            def fib = func (Int n) Int: {
                if (n <= 1) {
                    return n;
                } else {
                    return fib(n - 1) + fib(n - 2);
                }
            };
            """, scope);
        H.Eval("def num = Compiler.runtime();", scope);
        var result = H.EvalAs<IntegerSymbol>("fib(num)", scope);
        Assert.Equal(0L, result.Value);
    }

    [Fact]
    public void Recursive_Function_With_Compile_Time_Argument_Still_Works()
    {
        // Ensures recursive functions still work correctly with compile-time arguments.
        var scope = new EvaluationScope();
        H.Eval("""
            def fib = func (Int n) Int: {
                if (n <= 1) {
                    return n;
                } else {
                    return fib(n - 1) + fib(n - 2);
                }
            };
            """, scope);
        var result = H.EvalAs<IntegerSymbol>("fib(6)", scope);
        Assert.Equal(8L, result.Value);
    }
}

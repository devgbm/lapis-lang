using Lapis.Diagnostics;

namespace Lapis.Evaluator.Tests;

public sealed class CoreEvaluationTests : EvaluatorTestBase
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData("1.5", "1.5")]
    [InlineData("1.0", "1.0")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("\"hello\"", "hello")]
    [InlineData("()", "()")]
    public void Literals(string expression, string expected) => Eval(expression).ShouldBe(expected);

    /// <summary>Exemplo da spec §28.</summary>
    [Fact]
    public void Arithmetic_TenPlusTwenty() => Eval("10 + 20").ShouldBe("30");

    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("(1 + 2) * 3", "9")]
    [InlineData("1 - 2 - 3", "-4")]
    [InlineData("8 / 4 / 2", "1")]
    [InlineData("7 / 2", "3")]
    [InlineData("-7 / 2", "-3")]
    [InlineData("-10", "-10")]
    [InlineData("1.0 + 2.5", "3.5")]
    [InlineData("\"a\" + \"b\"", "ab")]
    public void Operators(string expression, string expected) => Eval(expression).ShouldBe(expected);

    [Theory]
    [InlineData("1 < 2", "true")]
    [InlineData("2 < 1", "false")]
    [InlineData("2 <= 2", "true")]
    [InlineData("3 > 2", "true")]
    [InlineData("3 >= 4", "false")]
    [InlineData("1 == 1", "true")]
    [InlineData("1 != 1", "false")]
    [InlineData("\"a\" < \"b\"", "true")]
    [InlineData("\"abc\" == \"abc\"", "true")]
    [InlineData("!true", "false")]
    [InlineData("!false", "true")]
    public void Comparisons(string expression, string expected) => Eval(expression).ShouldBe(expected);

    [Fact]
    public void IntDivisionByZero_Aborts() =>
        ExpectAbort("def x = 1 / 0;").ShouldBe(DiagnosticCodes.DivisionByZero);

    [Fact]
    public void FloatDivisionByZero_IsInfinity() => Eval("1.0 / 0.0").ShouldBe("Infinity");

    [Fact]
    public void Let_Sequential() => Output("def x = 1; def y = x + 1; print(y);").ShouldBe("2\n");

    [Fact]
    public void Shadowing_InnerWins() =>
        Output("def x = 1; { def x = 2; print(x); }").ShouldBe("2\n");

    [Fact]
    public void Shadowing_OuterUnaffectedAfterBlock() =>
        Output("def x = 1; { def x = 2; print(x); } print(x);").ShouldBe("2\n1\n");

    /// <summary>Spec §9: o valor de um bloco é a última expressão.</summary>
    [Fact]
    public void Block_ValueIsTail() => Eval("{ def x = 1; x + 1 }").ShouldBe("2");

    [Fact]
    public void Block_WithoutTail_IsVoid() => Eval("{ 1; }").ShouldBe("()");

    [Fact]
    public void Block_Empty_IsVoid() => Eval("{ }").ShouldBe("()");

    [Fact]
    public void If_TakesThenBranch() => Eval("if true { 1 } else { 2 }").ShouldBe("1");

    [Fact]
    public void If_TakesElseBranch() => Eval("if false { 1 } else { 2 }").ShouldBe("2");

    [Fact]
    public void If_WithoutElse_IsVoid() => Eval("if false { 1; }").ShouldBe("()");

    [Fact]
    public void If_ConditionIsEvaluated() =>
        Output("def x = 5; if x > 3 { print(\"maior\"); } else { print(\"menor\"); }").ShouldBe("maior\n");
}

public sealed class EvaluationOrderTests : EvaluatorTestBase
{
    private const string Tracer = """
        def trace = fn(n: Int) Int {
            print(n);
            return n;
        };
        """;

    [Fact]
    public void Binary_EvaluatesLeftBeforeRight() =>
        Output($"{Tracer}\ndef x = trace(1) + trace(2);").ShouldBe("1\n2\n");

    [Fact]
    public void Binary_LeftBeforeRight_EvenForMultiplication() =>
        Output($"{Tracer}\ndef x = trace(1) * trace(2);").ShouldBe("1\n2\n");

    [Fact]
    public void Call_ArgumentsLeftToRight() =>
        Output($"{Tracer}\ndef add = fn(a: Int, b: Int) Int {{ return a + b; }};\ndef x = add(trace(1), trace(2));")
            .ShouldBe("1\n2\n");

    [Fact]
    public void Call_CalleeBeforeArguments() =>
        Output($$"""
            {{Tracer}}
            def pick = fn() fn(Int) Int {
                print(0);
                return trace;
            };

            def x = pick()(trace(1));
            """).ShouldBe("0\n1\n1\n");

    /// <summary>Curto-circuito: <c>&amp;&amp;</c> e <c>||</c> viraram <c>If</c> no desugar.</summary>
    [Fact]
    public void AndAlso_ShortCircuits() =>
        Output($$"""
            def sideEffect = fn() Bool {
                print("executou");
                return true;
            };

            def x = false && sideEffect();
            """).ShouldBeEmpty();

    [Fact]
    public void OrElse_ShortCircuits() =>
        Output($$"""
            def sideEffect = fn() Bool {
                print("executou");
                return true;
            };

            def x = true || sideEffect();
            """).ShouldBeEmpty();

    [Fact]
    public void AndAlso_EvaluatesRightWhenLeftIsTrue() =>
        Output($$"""
            def sideEffect = fn() Bool {
                print("executou");
                return true;
            };

            def x = true && sideEffect();
            """).ShouldBe("executou\n");

    [Fact]
    public void DeadBranch_IsNotEvaluated() =>
        Output("if false { print(\"nao\"); } else { print(\"sim\"); }").ShouldBe("sim\n");
}

public sealed class FunctionTests : EvaluatorTestBase
{
    [Fact]
    public void Call_Simple() =>
        Output("""
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            print(add(10, 20));
            """).ShouldBe("30\n");

    /// <summary>Spec §32: a closure captura o ambiente do ponto de definição.</summary>
    [Fact]
    public void Closure_CapturesEnvironment() =>
        Output("""
            def multiplier = 10;

            def multiply = fn(x: Int) Int {
                return x * multiplier;
            };

            print(multiply(5));
            """).ShouldBe("50\n");

    [Fact]
    public void Closure_CapturesAtDefinitionTime() =>
        Output("""
            def n = 1;

            def get = fn() Int {
                return n;
            };

            def outro = {
                def n = 99;
                get()
            };

            print(outro);
            """).ShouldBe("1\n");

    [Fact]
    public void HigherOrder_FunctionAsArgument() =>
        Output("""
            def apply = fn(f: fn(Int) Int, v: Int) Int {
                return f(v);
            };

            def inc = fn(x: Int) Int {
                return x + 1;
            };

            print(apply(inc, 5));
            """).ShouldBe("6\n");

    [Fact]
    public void HigherOrder_FunctionReturningClosure() =>
        Output("""
            def make = fn(n: Int) fn(Int) Int {
                return fn(x: Int) Int {
                    return x + n;
                };
            };

            def add10 = make(10);

            print(add10(5));
            """).ShouldBe("15\n");

    [Fact]
    public void Closure_PrintsAsSignature() =>
        Eval("fn(a: Int) Int { return a; }").ShouldBe("<fn(Int) Int>");
}

public sealed class ReturnTests : EvaluatorTestBase
{
    [Fact]
    public void Return_ProducesValue() => Output("def f = fn() Int { return 1; }; print(f());").ShouldBe("1\n");

    /// <summary>Retorno antecipado encerra a função: nada depois é executado.</summary>
    [Fact]
    public void Return_Early_SkipsRestOfBody() =>
        Output("""
            def f = fn() Int {
                return 1;
                print(99);
                return 3;
            };

            print(f());
            """).ShouldBe("1\n");

    /// <summary>Spec §12: exemplo <c>abs</c>.</summary>
    [Fact]
    public void Return_FromInsideIf() =>
        Output("""
            def abs = fn(x: Int) Int {
                if x < 0 {
                    return -x;
                }

                return x;
            };

            print(abs(-5));
            print(abs(5));
            """).ShouldBe("5\n5\n");

    [Fact]
    public void Return_FromNestedBlock() =>
        Output("def f = fn() Int { { return 1; } }; print(f());").ShouldBe("1\n");

    /// <summary>Spec §12: <c>return</c> encerra a função mais próxima, não a externa.</summary>
    [Fact]
    public void Return_StopsAtNearestFunction() =>
        Output("""
            def outer = fn() Int {
                def inner = fn() Int {
                    return 1;
                };

                def v = inner();

                print(v);

                return 2;
            };

            print(outer());
            """).ShouldBe("1\n2\n");

    [Fact]
    public void VoidFunction_FallingOffEnd_IsVoid() =>
        Output("def f = fn() Void { print(1); }; print(f());").ShouldBe("1\n()\n");

    [Fact]
    public void VoidFunction_BareReturn_IsVoid() =>
        Output("def f = fn() Void { return; }; print(f());").ShouldBe("()\n");

    [Fact]
    public void VoidFunction_BareReturn_SkipsRest() =>
        Output("def f = fn() Void { return; print(99); }; def x = f();").ShouldBeEmpty();

    [Fact]
    public void Return_InBinaryOperand_ReturnsFromFunction() =>
        Output("""
            def f = fn() Int {
                def x = 1 + (return 2);

                print(99);

                return 3;
            };

            print(f());
            """).ShouldBe("2\n");

    [Fact]
    public void Return_InArgumentPosition_ReturnsFromFunction() =>
        Output("""
            def f = fn() Int {
                print(return 7);

                return 3;
            };

            print(f());
            """).ShouldBe("7\n");
}

public sealed class SpecExampleTests : EvaluatorTestBase
{
    /// <summary>Teste-âncora do M1: spec §34 e §60.</summary>
    [Fact]
    public void Section34_Hello_Prints30() =>
        Output("""
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            def main = fn() Void {
                def result = add(10, 20);

                print(result);
            };

            main();
            """).ShouldBe("30\n");

    /// <summary>Spec §3: expressões top-level são avaliadas em ordem.</summary>
    [Fact]
    public void Section3_TopLevelOrder() =>
        Output("""
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            def result = add(10, 20);

            print(result);
            """).ShouldBe("30\n");

    /// <summary>Spec §4: `main` não tem tratamento especial.</summary>
    [Fact]
    public void Section4_MainIsNotSpecial() =>
        Output("""
            def main = fn() Int {
                print("Hello");
                return 0;
            };

            main();
            """).ShouldBe("Hello\n");

    /// <summary>Spec §9: valor do bloco.</summary>
    [Fact]
    public void Section9_BlockValue() =>
        Output("""
            def r = {
                def x = 10;
                def y = 20;

                x + y
            };

            print(r);
            """).ShouldBe("30\n");
}

public sealed class InvariantTests : EvaluatorTestBase
{
    [Fact]
    public void Evaluation_IsDeterministic()
    {
        const string Source = """
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            print(add(1, 2));
            print(add(3, 4));
            """;

        Output(Source).ShouldBe(Output(Source));
    }

    [Fact]
    public void EmptyProgram_ProducesNoOutput() => Output(string.Empty).ShouldBeEmpty();

    [Fact]
    public void ProgramValue_IsNotPrinted() => Output("def x = 42;").ShouldBeEmpty();

    [Theory]
    [InlineData("def x = 1;")]
    [InlineData("def f = fn(a: Int) Int { return a; }; print(f(1));")]
    [InlineData("def x = { def y = 1; y };")]
    [InlineData("if true { print(1); } else { print(2); }")]
    public void WellTypedProgram_DoesNotThrow(string source) => Should.NotThrow(() => Run(source));
}

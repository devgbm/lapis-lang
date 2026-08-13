using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

public sealed class ScopeTests : TypeCheckerTestBase
{
    [Fact]
    public void UnknownVariable_ReportsLap0201() =>
        ShouldFailWith("x;", DiagnosticCodes.UnknownVariable);

    [Fact]
    public void UsedBeforeDefinition_ReportsLap0201() =>
        ShouldFailWith("x; def x = 1;", DiagnosticCodes.UnknownVariable);

    /// <summary>
    /// <c>Let(x, v, body)</c> continua não expondo <c>x</c> em <c>v</c> — a
    /// exceção da Q34 é só para <b>função</b>, onde a assinatura dá o tipo sem
    /// olhar o corpo. Fora dela a Q8 segue valendo.
    /// </summary>
    [Fact]
    public void SelfReference_InOwnInitializer_ReportsLap0201() =>
        ShouldFailWith("def x = x + 1;", DiagnosticCodes.UnknownVariable);

    /// <summary>E a exceção: uma função enxerga a si mesma (Q34).</summary>
    [Fact]
    public void SelfReference_InsideALambda_Recurses() =>
        ShouldPass("def f = fn(n: Int) Int { if n <= 0 { return 0; } return f(n - 1); };");

    [Fact]
    public void Shadowing_InInnerScope_IsAllowed() =>
        ShouldPass("def x = 1; { def x = \"s\"; print(x); }");

    [Fact]
    public void Redefinition_InSameScope_ReportsLap0202() =>
        ShouldFailWith("def x = 1; def x = 2;", DiagnosticCodes.DuplicateDefinition);

    [Fact]
    public void DuplicateParameter_ReportsLap0202() =>
        ShouldFailWith("def f = fn(a: Int, a: Int) Int { return a; };", DiagnosticCodes.DuplicateDefinition);

    [Fact]
    public void UnknownVariable_SuggestsCloseName()
    {
        var diagnostic = ShouldFailWith("def result = 1; print(reslt);", DiagnosticCodes.UnknownVariable);

        diagnostic.Notes.ShouldContain(n => n.Message.Contains("result"));
    }

    [Fact]
    public void UnknownVariable_DistantName_HasNoSuggestion()
    {
        var diagnostic = ShouldFailWith("def result = 1; print(zzzzzzzz);", DiagnosticCodes.UnknownVariable);

        diagnostic.Notes.ShouldBeEmpty();
    }

    [Fact]
    public void Closure_CapturesOuterBinding() =>
        ShouldPass("""
            def multiplier = 10;

            def multiply = fn(x: Int) Int {
                return x * multiplier;
            };
            """);
}

public sealed class LiteralAndOperatorTests : TypeCheckerTestBase
{
    [Theory]
    [InlineData("def x = 1;", "Int")]
    [InlineData("def x = 1.5;", "Float")]
    [InlineData("def x = true;", "Bool")]
    [InlineData("def x = \"s\";", "Str")]
    [InlineData("def x = ();", "Void")]
    public void Literals_HaveExpectedTypes(string source, string expected) =>
        TypeOfFirstDef(source).ToDisplayString().ShouldBe(expected);

    [Theory]
    [InlineData("def x = 1 + 2;", "Int")]
    [InlineData("def x = 1.0 * 2.0;", "Float")]
    [InlineData("def x = \"a\" + \"b\";", "Str")]
    [InlineData("def x = 1 < 2;", "Bool")]
    [InlineData("def x = \"a\" < \"b\";", "Bool")]
    [InlineData("def x = 1 == 2;", "Bool")]
    [InlineData("def x = true != false;", "Bool")]
    [InlineData("def x = -1;", "Int")]
    [InlineData("def x = !true;", "Bool")]
    public void Operators_HaveExpectedTypes(string source, string expected) =>
        TypeOfFirstDef(source).ToDisplayString().ShouldBe(expected);

    /// <summary>Sem promoção implícita Int→Float (plano 06 §6.5).</summary>
    [Fact]
    public void MixedNumericArithmetic_ReportsLap0280() =>
        ShouldFailWith("def x = 1 + 1.0;", DiagnosticCodes.OperatorNotApplicable);

    [Fact]
    public void EqualityOfDifferentTypes_ReportsLap0280() =>
        ShouldFailWith("def x = 1 == \"a\";", DiagnosticCodes.OperatorNotApplicable);

    [Fact]
    public void EqualityOfFunctions_ReportsLap0281() =>
        ShouldFailWith(
            "def f = fn() Int { return 1; }; def g = fn() Int { return 2; }; def x = f == g;",
            DiagnosticCodes.FunctionsNotComparable);

    [Fact]
    public void StrSubtraction_ReportsLap0280() =>
        ShouldFailWith("def x = \"a\" - \"b\";", DiagnosticCodes.OperatorNotApplicable);

    [Fact]
    public void NegateBool_ReportsLap0280() =>
        ShouldFailWith("def x = -true;", DiagnosticCodes.OperatorNotApplicable);

    [Fact]
    public void NotInt_ReportsLap0280() =>
        ShouldFailWith("def x = !1;", DiagnosticCodes.OperatorNotApplicable);

    [Fact]
    public void BoolArithmetic_ReportsLap0280() =>
        ShouldFailWith("def x = true + false;", DiagnosticCodes.OperatorNotApplicable);

    [Fact]
    public void LogicalOperators_RequireBool() =>
        ShouldFailWith("def x = 1 && true;", DiagnosticCodes.ConditionMustBeBool);

    [Fact]
    public void LogicalOperators_OnBool_AreFine() =>
        TypeOfFirstDef("def x = true && false;").ToDisplayString().ShouldBe("Bool");
}

public sealed class AnnotationTests : TypeCheckerTestBase
{
    [Fact]
    public void MatchingAnnotation_IsAccepted() => ShouldPass("def x: Int = 1;");

    [Fact]
    public void MismatchedAnnotation_ReportsLap0210() =>
        ShouldFailWith("def x: Str = 1;", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void UnknownTypeName_ReportsLap0204() =>
        ShouldFailWith("def x: Foo = 1;", DiagnosticCodes.UnknownType);

    [Fact]
    public void FunctionTypeAnnotation_IsAccepted() =>
        ShouldPass("def f = fn(a: Int) Int { return a; }; def g: fn(Int) Int = f;");

    [Fact]
    public void FunctionTypeAnnotation_WrongArity_ReportsLap0210() =>
        ShouldFailWith(
            "def f = fn(a: Int) Int { return a; }; def g: fn(Int, Int) Int = f;",
            DiagnosticCodes.TypeMismatch);
}

public sealed class CallTests : TypeCheckerTestBase
{
    private const string Add = "def add = fn(a: Int, b: Int) Int { return a + b; };";

    [Fact]
    public void Call_ReturnsDeclaredType() =>
        TypeOfFirstDef(Add).ToDisplayString().ShouldBe("fn(Int, Int) Int");

    [Fact]
    public void Call_WithCorrectArguments_TypesAsReturn() =>
        ShouldPass($"{Add}\ndef r: Int = add(1, 2);");

    [Fact]
    public void Call_TooFewArguments_ReportsLap0221() =>
        ShouldFailWith($"{Add}\nadd(1);", DiagnosticCodes.ArgumentCountMismatch);

    [Fact]
    public void Call_TooManyArguments_ReportsLap0221() =>
        ShouldFailWith($"{Add}\nadd(1, 2, 3);", DiagnosticCodes.ArgumentCountMismatch);

    [Fact]
    public void Call_WrongArgumentType_ReportsLap0222() =>
        ShouldFailWith($"{Add}\nadd(1, \"s\");", DiagnosticCodes.ArgumentTypeMismatch);

    [Fact]
    public void Call_NonFunction_ReportsLap0220() =>
        ShouldFailWith("def x = 1; x(2);", DiagnosticCodes.NotCallable);

    [Fact]
    public void Call_ResultAssignedToWrongType_ReportsLap0210() =>
        ShouldFailWith($"{Add}\ndef r: Str = add(1, 2);", DiagnosticCodes.TypeMismatch);

    [Fact]
    public void HigherOrder_FunctionAsArgument() =>
        ShouldPass("""
            def apply = fn(f: fn(Int) Int, v: Int) Int {
                return f(v);
            };

            def inc = fn(x: Int) Int {
                return x + 1;
            };

            def r = apply(inc, 1);
            """);

    [Fact]
    public void HigherOrder_FunctionReturningFunction() =>
        ShouldPass("""
            def make = fn(n: Int) fn(Int) Int {
                return fn(x: Int) Int {
                    return x + n;
                };
            };
            """);
}

public sealed class GenericInferenceTests : TypeCheckerTestBase
{
    /// <summary>Q7: <c>print</c> é <c>fn&lt;T&gt;(T) Void</c> e o argumento é inferido.</summary>
    [Theory]
    [InlineData("print(1);")]
    [InlineData("print(1.5);")]
    [InlineData("print(true);")]
    [InlineData("print(\"s\");")]
    [InlineData("print(());")]
    public void Print_InfersItsTypeArgument(string source) => ShouldPass(source);

    [Fact]
    public void Print_ReturnsVoid() => ShouldPass("def x: Void = print(1);");

    [Fact]
    public void Print_OnClosure_IsAllowed() =>
        ShouldPass("def f = fn() Int { return 1; }; print(f);");

    [Fact]
    public void Print_WithWrongArity_ReportsLap0221() =>
        ShouldFailWith("print(1, 2);", DiagnosticCodes.ArgumentCountMismatch);

    [Fact]
    public void UserCanShadowPrint() =>
        ShouldPass("def print = fn(x: Int) Void { }; print(1);");
}

public sealed class ReturnTests : TypeCheckerTestBase
{
    [Fact]
    public void Return_MatchingType_IsAccepted() =>
        ShouldPass("def f = fn() Int { return 1; };");

    [Fact]
    public void Return_WrongType_ReportsLap0270() =>
        ShouldFailWith("def f = fn() Int { return \"s\"; };", DiagnosticCodes.ReturnTypeMismatch);

    [Fact]
    public void EmptyReturn_InNonVoid_ReportsLap0271() =>
        ShouldFailWith("def f = fn() Int { return; };", DiagnosticCodes.EmptyReturnInNonVoid);

    [Fact]
    public void EmptyReturn_InVoid_IsAccepted() =>
        ShouldPass("def f = fn() Void { return; };");

    [Fact]
    public void Void_FallingOffTheEnd_IsAccepted() =>
        ShouldPass("def f = fn() Void { print(1); };");

    [Fact]
    public void Void_ImplicitReturnType_IsAccepted() =>
        ShouldPass("def f = fn() { print(1); };");

    [Fact]
    public void NonVoid_WithoutReturn_ReportsLap0272() =>
        ShouldFailWith("def f = fn() Int { 1 };", DiagnosticCodes.MissingReturn);

    [Fact]
    public void NonVoid_ReturnOnlyInThenBranch_ReportsLap0272() =>
        ShouldFailWith(
            "def f = fn(c: Bool) Int { if c { return 1; } };",
            DiagnosticCodes.MissingReturn);

    [Fact]
    public void NonVoid_ReturnInBothBranches_IsAccepted() =>
        ShouldPass("def f = fn(c: Bool) Int { if c { return 1; } else { return 2; } };");

    /// <summary>Spec §12: exemplo <c>abs</c>.</summary>
    [Fact]
    public void NonVoid_EarlyReturnThenFinalReturn_IsAccepted() =>
        ShouldPass("""
            def abs = fn(x: Int) Int {
                if x < 0 {
                    return -x;
                }

                return x;
            };
            """);

    [Fact]
    public void Return_InsideNestedBlock_Counts() =>
        ShouldPass("def f = fn() Int { { return 1; } };");

    /// <summary>
    /// Spec §12, último bullet: <c>return</c> encerra a função mais próxima, então
    /// o da lambda interna não satisfaz a externa.
    /// </summary>
    [Fact]
    public void Return_InNestedLambda_DoesNotSatisfyOuter() =>
        ShouldFailWith(
            "def f = fn() Int { def g = fn() Int { return 1; }; };",
            DiagnosticCodes.MissingReturn);

    [Fact]
    public void Return_InNestedLambda_ChecksAgainstInnerSignature() =>
        ShouldFailWith(
            "def f = fn() Int { def g = fn() Str { return 1; }; return 2; };",
            DiagnosticCodes.ReturnTypeMismatch);

    [Fact]
    public void Return_AtTopLevel_ReportsLap0274() =>
        ShouldFailWith("return 1;", DiagnosticCodes.ReturnOutsideFunction);

    [Fact]
    public void UnreachableCodeAfterReturn_WarnsLap0273()
    {
        var codes = Codes("def f = fn() Int { return 1; print(2); };");

        codes.ShouldContain(DiagnosticCodes.UnreachableAfterReturn);
    }

    [Fact]
    public void NoWarning_WhenReturnIsLast() =>
        Codes("def f = fn() Int { return 1; };").ShouldNotContain(DiagnosticCodes.UnreachableAfterReturn);

    /// <summary>O tipo bottom faz `if` com return num ramo só tipar (Q13).</summary>
    [Fact]
    public void ReturnInOneBranch_JoinsWithOtherBranchType() =>
        ShouldPass("def f = fn(c: Bool) Int { def x: Int = if c { return 0; } else { 2 }; return x; };");
}

public sealed class ConditionalTests : TypeCheckerTestBase
{
    [Fact]
    public void Condition_MustBeBool_ReportsLap0230() =>
        ShouldFailWith("if 1 { print(1); }", DiagnosticCodes.ConditionMustBeBool);

    [Fact]
    public void Branches_WithDifferentTypes_ReportLap0231() =>
        ShouldFailWith("def x = if true { 1 } else { \"s\" };", DiagnosticCodes.IncompatibleBranches);

    [Fact]
    public void Branches_WithSameType_Join() =>
        TypeOfFirstDef("def x = if true { 1 } else { 2 };").ToDisplayString().ShouldBe("Int");

    [Fact]
    public void IfWithoutElse_IsVoid() =>
        TypeOfFirstDef("def x = if true { 1; };").ToDisplayString().ShouldBe("Void");
}

public sealed class OutputTests : TypeCheckerTestBase
{
    [Fact]
    public void EveryNode_HasAType()
    {
        var program = Check("""
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            def r = add(1, 2);

            print(r);
            """);

        program.NodeTypes.Count.ShouldBe(program.Program.NodeCount);
    }

    [Fact]
    public void Call_HasCallResolution()
    {
        var program = Check("print(1);");

        program.Resolutions.Values.OfType<Ast.Typed.CallResolution>().ShouldNotBeEmpty();
    }

    [Fact]
    public void Variable_HasVariableResolution()
    {
        var program = Check("def x = 1; print(x);");

        program.Resolutions.Values.OfType<Ast.Typed.VariableResolution>().ShouldNotBeEmpty();
    }

    /// <summary>Um erro de origem gera exatamente um diagnóstico, sem cascata.</summary>
    [Fact]
    public void ErrorType_DoesNotCascade()
    {
        var errors = CheckWithDiagnostics("def y = x + 1 * 2 - 3;")
            .Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

        errors.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnknownVariable);
    }

    [Fact]
    public void HelloExample_TypeChecks() =>
        ShouldPass("""
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            def main = fn() Void {
                def result = add(10, 20);

                print(result);
            };

            main();
            """);
}

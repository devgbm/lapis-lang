using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

public sealed class MatchTypeTests : TypeCheckerTestBase
{
    private const string Color = "def Color = enum { Red, Green, Blue };\n";

    [Fact]
    public void Match_AllVariants_IsExhaustive() =>
        ShouldPass($"{Color}def f = fn(c: Color) Int {{ match c {{ Color.Red => return 1, Color.Green => return 2, Color.Blue => return 3 }} }};");

    /// <summary>Q6: `match` é expressão, logo precisa cobrir todos os casos.</summary>
    [Fact]
    public void Match_MissingVariants_ReportsLap0262()
    {
        var diagnostic = ShouldFailWith(
            $"{Color}def f = fn(c: Color) Int {{ match c {{ Color.Red => return 1 }} }};",
            DiagnosticCodes.NonExhaustiveMatch);

        diagnostic.Message.ShouldContain("Color.Green");
        diagnostic.Message.ShouldContain("Color.Blue");
    }

    [Fact]
    public void Match_Wildcard_MakesExhaustive() =>
        ShouldPass($"{Color}def f = fn(c: Color) Int {{ match c {{ Color.Red => return 1, _ => return 0 }} }};");

    [Fact]
    public void Match_BindingPattern_MakesExhaustive() =>
        ShouldPass($"{Color}def f = fn(c: Color) Int {{ match c {{ outra => return 0 }} }};");

    [Fact]
    public void Match_OnInt_RequiresCatchAll() =>
        ShouldFailWith(
            "def f = fn(n: Int) Int { match n { 0 => return 1 } };",
            DiagnosticCodes.NonExhaustiveMatch);

    [Fact]
    public void Match_OnInt_WithCatchAll_IsAccepted() =>
        ShouldPass("def f = fn(n: Int) Int { match n { 0 => return 1, _ => return 2 } };");

    [Fact]
    public void Match_ArmsMustAgree_ReportsLap0261() =>
        ShouldFailWith(
            $"{Color}def x = match Color.Red {{ Color.Red => 1, _ => \"s\" }};",
            DiagnosticCodes.IncompatibleMatchArms);

    [Fact]
    public void Match_ArmsWithSameType_JoinToIt() =>
        TypeOfDef($"{Color}def x = match Color.Red {{ Color.Red => 1, _ => 2 }};", "x")
            .ToDisplayString().ShouldBe("Int");

    [Fact]
    public void Match_ArmAfterWildcard_WarnsLap0263() =>
        Codes($"{Color}def x = match Color.Red {{ _ => 1, Color.Red => 2 }};")
            .ShouldContain(DiagnosticCodes.UnreachableArm);

    [Fact]
    public void Match_DuplicateVariantArm_WarnsLap0263() =>
        Codes($"{Color}def x = match Color.Red {{ Color.Red => 1, Color.Red => 2, _ => 3 }};")
            .ShouldContain(DiagnosticCodes.UnreachableArm);

    [Fact]
    public void Match_UnknownVariant_ReportsLap0251() =>
        ShouldFailWith(
            $"{Color}def x = match Color.Red {{ Color.Purple => 1, _ => 2 }};",
            DiagnosticCodes.UnknownVariant);

    [Fact]
    public void Match_PatternOfAnotherEnum_ReportsLap0260() =>
        ShouldFailWith(
            $"{Color}def Other = enum {{ X }};\ndef v = match Color.Red {{ Other.X => 1, _ => 2 }};",
            DiagnosticCodes.PatternTypeMismatch);

    [Fact]
    public void Match_VariantArityMismatch_ReportsLap0264() =>
        ShouldFailWith(
            "def B = enum { Wrap(Int) };\ndef x = match B.Wrap(1) { B.Wrap(a, b) => 1 };",
            DiagnosticCodes.VariantArityMismatch);

    [Fact]
    public void Match_LiteralPatternOfWrongType_ReportsLap0260() =>
        ShouldFailWith(
            "def f = fn(n: Int) Int { match n { \"s\" => return 1, _ => return 2 } };",
            DiagnosticCodes.PatternTypeMismatch);

    [Fact]
    public void Match_PatternBindsPayload() =>
        ShouldPass("def B = enum { Wrap(Int) };\ndef x: Int = match B.Wrap(1) { B.Wrap(v) => v };");

    [Fact]
    public void Match_PayloadBinding_HasPayloadType() =>
        ShouldFailWith(
            "def B = enum { Wrap(Int) };\ndef x: Str = match B.Wrap(1) { B.Wrap(v) => v };",
            DiagnosticCodes.TypeMismatch);

    /// <summary>
    /// Os argumentos de tipo da instância são substituídos na carga: em
    /// <c>r: Result&lt;Int, Str&gt;</c>, o `v` de `Result.Ok(v)` é `Int`.
    /// </summary>
    [Fact]
    public void Match_OnGenericEnum_SubstitutesTypeArguments() =>
        ShouldPass("""
            def f = fn(r: Result<Int, Str>) Int {
                match r {
                    Result.Ok(v) => return v,
                    Result.Err(e) => return 0
                }
            };
            """);

    [Fact]
    public void Match_OnGenericEnum_PayloadTypeIsChecked() =>
        ShouldFailWith("""
            def f = fn(r: Result<Int, Str>) Str {
                match r {
                    Result.Ok(v) => return v,
                    Result.Err(e) => return "e"
                }
            };
            """, DiagnosticCodes.ReturnTypeMismatch);

    [Fact]
    public void Match_ArmScope_DoesNotLeak() =>
        ShouldFailWith(
            "def B = enum { Wrap(Int) };\ndef x = match B.Wrap(1) { B.Wrap(v) => v };\ndef y = v;",
            DiagnosticCodes.UnknownVariable);

    [Fact]
    public void Match_NestedPattern() =>
        ShouldPass("""
            def B = enum { Wrap(Int) };
            def O = enum { Outer(B) };

            def x: Int = match O.Outer(B.Wrap(1)) {
                O.Outer(B.Wrap(v)) => v
            };
            """);

    /// <summary>
    /// O escrutinado é o <c>Option</c> de uma indexação sem tamanho conhecido
    /// (plano 24): o span é <c>var</c> justamente para alargar o tamanho para
    /// <c>?</c>.
    /// </summary>
    [Fact]
    public void Match_ScrutineeOnIndexOption() =>
        ShouldPass("""
            var numbers = .[1, 2, 3];

            def x: Int = match numbers[0] {
                Option.Some(v) => v,
                Option.None => 0
            };
            """);

    /// <summary>Todos os braços retornam ⇒ a função retorna em todos os caminhos.</summary>
    [Fact]
    public void Match_WithReturnInEveryArm_SatisfiesReturnAnalysis() =>
        ShouldPass("""
            def unwrapOr = fn(r: Result<Int, Str>, fallback: Int) Int {
                match r {
                    Result.Ok(value) => return value,
                    Result.Err(error) => return fallback
                }
            };
            """);

    [Fact]
    public void Match_WithReturnInSomeArms_ReportsLap0272() =>
        ShouldFailWith(
            $"{Color}def f = fn(c: Color) Int {{ match c {{ Color.Red => return 1, _ => 0 }} }};",
            DiagnosticCodes.MissingReturn);
}

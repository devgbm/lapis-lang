using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

public sealed class MatchParseTests : ParserTestBase
{
    [Fact]
    public void Match_Simple() =>
        ShouldPrintAs(
            "match c { Color.Red => 1, _ => 2 };",
            "(match (name c) (arm Color.Red (int 1)) (arm _ (int 2)))");

    [Fact]
    public void Match_WithPayloadPattern() =>
        ShouldPrintAs(
            "match r { Result.Ok(v) => v, Result.Err(e) => 0 };",
            "(match (name r) (arm Result.Ok(v) (name v)) (arm Result.Err(e) (int 0)))");

    [Fact]
    public void Match_NestedPattern() =>
        ShouldPrintAs(
            "match x { A.B(C.D(v)) => v };",
            "(match (name x) (arm A.B(C.D(v)) (name v)))");

    [Fact]
    public void Match_LiteralPatterns() =>
        ShouldPrintAs(
            "match n { 1 => \"um\", _ => \"outro\" };",
            "(match (name n) (arm 1 (str \"um\")) (arm _ (str \"outro\")))");

    [Fact]
    public void Match_NegativeLiteralPattern() =>
        ShouldPrintAs("match n { -1 => 0, _ => 1 };", "(match (name n) (arm -1 (int 0)) (arm _ (int 1)))");

    [Fact]
    public void Match_BoolPatterns() =>
        ShouldPrintAs(
            "match b { true => 1, false => 2 };",
            "(match (name b) (arm true (int 1)) (arm false (int 2)))");

    /// <summary>Q3: um identificador sozinho é sempre binding, nunca variante.</summary>
    [Fact]
    public void Match_BareIdentifier_IsBindingPattern()
    {
        var arm = SingleMatch("match x { outro => 1 };").Arms.ShouldHaveSingleItem();

        arm.Pattern.ShouldBeOfType<BindingPattern>().Name.ShouldBe("outro");
    }

    [Fact]
    public void Match_QualifiedName_IsVariantPattern()
    {
        var arm = SingleMatch("match x { Color.Red => 1 };").Arms.ShouldHaveSingleItem();

        var pattern = arm.Pattern.ShouldBeOfType<VariantPattern>();
        pattern.EnumName.ShouldBe("Color");
        pattern.VariantName.ShouldBe("Red");
        pattern.Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void Match_TrailingComma_IsAccepted() =>
        SingleMatch("match x { _ => 1, };").Arms.Length.ShouldBe(1);

    [Fact]
    public void Match_ArmWithReturn() =>
        SingleMatch("match x { _ => return 1 };")
            .Arms.ShouldHaveSingleItem().Body.ShouldBeOfType<ReturnExpression>();

    [Fact]
    public void Match_NoArms_ReportsLap0107() =>
        Codes("match x { };").ShouldContain(DiagnosticCodes.MatchRequiresArm);

    [Fact]
    public void Match_MissingFatArrow_ReportsError() => Codes("match x { _ 1 };").ShouldNotBeEmpty();

    [Fact]
    public void Match_InvalidPattern_ReportsLap0113() =>
        Codes("match x { + => 1 };").ShouldContain(DiagnosticCodes.ExpectedPattern);

    /// <summary>
    /// O escrutinado usa a gramática de expressão sem restrição: com Q2 (construção
    /// com ponto inicial) não há como confundir o `{` do match com um struct.
    /// </summary>
    [Fact]
    public void Match_ScrutineeIsUnrestricted() =>
        SingleMatch("match p { _ => 1 };").Scrutinee.ShouldBeOfType<IdentifierExpression>();

    [Fact]
    public void Match_ScrutineeCanBeIndex() =>
        SingleMatch("match a[0] { _ => 1 };").Scrutinee.ShouldBeOfType<IndexExpression>();

    /// <summary>Como `if`, um `match` usado como statement dispensa o `;` (Q16).</summary>
    [Fact]
    public void Match_AsStatement_NeedsNoSemicolon() =>
        Should.NotThrow(() => Parse("def f = fn() Int { match x { _ => return 1 } };"));

    private static MatchExpression SingleMatch(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<MatchExpression>();
}

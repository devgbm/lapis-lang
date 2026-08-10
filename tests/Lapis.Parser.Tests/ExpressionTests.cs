using Lapis.Ast.Surface;

namespace Lapis.Parser.Tests;

public sealed class LiteralTests : ParserTestBase
{
    [Fact]
    public void Int() => ShouldPrintAs("1;", "(int 1)");

    [Fact]
    public void Float() => ShouldPrintAs("1.5;", "(float 1.5)");

    [Fact]
    public void Bool() => ShouldPrintAs("true;", "(bool true)");

    [Fact]
    public void Str() => ShouldPrintAs("\"s\";", "(str \"s\")");

    [Fact]
    public void Unit() => ShouldPrintAs("();", "(unit)");

    [Fact]
    public void Identifier() => ShouldPrintAs("x;", "(name x)");

    /// <summary>
    /// A spec §6 lista <c>-10</c> como literal, mas o parser produz uma negação;
    /// quem dobra para um literal negativo é o desugar (plano 05 §5.2).
    /// </summary>
    [Fact]
    public void NegativeInt_IsUnaryNegation() => ShouldPrintAs("-10;", "(unary - (int 10))");

    [Fact]
    public void NegativeFloat_IsUnaryNegation() => ShouldPrintAs("-0.5;", "(unary - (float 0.5))");
}

public sealed class OperatorTests : ParserTestBase
{
    [Fact]
    public void Precedence_MulBeforeAdd() =>
        ShouldPrintAs("1 + 2 * 3;", "(binary + (int 1) (binary * (int 2) (int 3)))");

    [Fact]
    public void Precedence_AddBeforeComparison() =>
        ShouldPrintAs("1 + 2 < 3;", "(binary < (binary + (int 1) (int 2)) (int 3))");

    [Fact]
    public void Precedence_ComparisonBeforeEquality() =>
        ShouldPrintAs("a < b == c;", "(binary == (binary < (name a) (name b)) (name c))");

    [Fact]
    public void Precedence_AndBeforeOr() =>
        ShouldPrintAs("a && b || c;", "(binary || (binary && (name a) (name b)) (name c))");

    [Fact]
    public void Precedence_EqualityBeforeAnd() =>
        ShouldPrintAs("a == b && c;", "(binary && (binary == (name a) (name b)) (name c))");

    [Fact]
    public void Associativity_SubtractionIsLeft() =>
        ShouldPrintAs("1 - 2 - 3;", "(binary - (binary - (int 1) (int 2)) (int 3))");

    [Fact]
    public void Associativity_DivisionIsLeft() =>
        ShouldPrintAs("8 / 4 / 2;", "(binary / (binary / (int 8) (int 4)) (int 2))");

    [Fact]
    public void Parens_OverridePrecedence() =>
        ShouldPrintAs("(1 + 2) * 3;", "(binary * (binary + (int 1) (int 2)) (int 3))");

    [Fact]
    public void Unary_Not() => ShouldPrintAs("!a;", "(unary ! (name a))");

    [Fact]
    public void Unary_BindsTighterThanBinary() =>
        ShouldPrintAs("-a + b;", "(binary + (unary - (name a)) (name b))");

    [Fact]
    public void Unary_Nested() => ShouldPrintAs("!!a;", "(unary ! (unary ! (name a)))");

    [Fact]
    public void ChainedComparison_IsRejected() =>
        Codes("a < b < c;").ShouldContain(Diagnostics.DiagnosticCodes.ChainedComparison);

    [Fact]
    public void ParenthesizedComparison_IsAllowed() =>
        Codes("(a < b) < c;").ShouldBeEmpty();

    [Fact]
    public void MixedComparisonOperators_AreAlsoRejected() =>
        Codes("a <= b > c;").ShouldContain(Diagnostics.DiagnosticCodes.ChainedComparison);
}

public sealed class BlockTests : ParserTestBase
{
    [Fact]
    public void Block_WithTail_HasTail()
    {
        var block = ParseSingleExpression<BlockExpression>("{ def x = 1; x };");

        block.Statements.Length.ShouldBe(1);
        block.Tail.ShouldNotBeNull();
    }

    [Fact]
    public void Block_WithoutTail_HasNoTail()
    {
        var block = ParseSingleExpression<BlockExpression>("{ print(1); };");

        block.Statements.Length.ShouldBe(1);
        block.Tail.ShouldBeNull();
    }

    [Fact]
    public void Block_Empty()
    {
        var block = ParseSingleExpression<BlockExpression>("{};");

        block.Statements.ShouldBeEmpty();
        block.Tail.ShouldBeNull();
    }

    [Fact]
    public void Block_Nested() =>
        Should.NotThrow(() => Parse("{ { def x = 1; x } };"));

    [Fact]
    public void Block_MultipleStatements()
    {
        var block = ParseSingleExpression<BlockExpression>("{ def a = 1; def b = 2; a + b };");

        block.Statements.Length.ShouldBe(2);
        block.Tail.ShouldNotBeNull();
    }

    private static T ParseSingleExpression<T>(string source)
        where T : Expression
    {
        var file = Parse(source);
        var statement = file.Statements.ShouldHaveSingleItem().ShouldBeOfType<ExpressionStatement>();
        return statement.Expression.ShouldBeOfType<T>();
    }
}

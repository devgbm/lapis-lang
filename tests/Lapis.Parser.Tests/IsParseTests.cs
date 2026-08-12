using Lapis.Ast;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

/// <summary>
/// <c>e is Variante</c> e <c>e is Variante(v)</c> (plano 25, M16 — fecha Q23).
/// </summary>
public sealed class IsParseTests : ParserTestBase
{
    private static Expression SingleExpression(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>().Expression;

    [Fact]
    public void Unqualified_NoBinding()
    {
        var isExpr = SingleExpression("e is Some;").ShouldBeOfType<IsExpression>();

        isExpr.OwnerName.ShouldBeNull();
        isExpr.VariantName.ShouldBe("Some");
        isExpr.BindingName.ShouldBeNull();
    }

    [Fact]
    public void Qualified_NoBinding()
    {
        var isExpr = SingleExpression("e is Option.Some;").ShouldBeOfType<IsExpression>();

        isExpr.OwnerName.ShouldBe("Option");
        isExpr.VariantName.ShouldBe("Some");
    }

    private static Expression DefValue(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>().Value;

    [Fact]
    public void Unqualified_WithBinding()
    {
        // A ligação exige uma posição que dá escopo a ela (§25.3); fora dela o
        // parser aceita a sintaxe normalmente — quem reclama é o desugar
        // (LAP0730).
        var isExpr = DefValue("def b = e is Some(value);").ShouldBeOfType<IsExpression>();

        isExpr.VariantName.ShouldBe("Some");
        isExpr.BindingName.ShouldBe("value");
    }

    [Fact]
    public void QualifiedWithGenericOwner_WithBinding()
    {
        var isExpr = DefValue("def b = e is Result<Int, ?>.Ok(value);").ShouldBeOfType<IsExpression>();

        isExpr.OwnerName.ShouldBe("Result");
        isExpr.OwnerTypeArguments.Length.ShouldBe(2);
        isExpr.VariantName.ShouldBe("Ok");
        isExpr.BindingName.ShouldBe("value");
    }

    [Fact]
    public void InIfCondition_Parses() =>
        Codes("if e is Some(v) { print(v); }").ShouldBeEmpty();

    [Fact]
    public void InAndAlso_Parses() =>
        Codes("def b = e is Some(v) && v == 1;").ShouldBeEmpty();

    [Fact]
    public void PrecedenceBetween_AndAndEquality()
    {
        // `e is Some(v) && v == 1` deve parsear como `(e is Some(v)) && (v == 1)`.
        var and = DefValue("def b = e is Some(v) && v == 1;").ShouldBeOfType<BinaryExpression>();

        and.Operator.ShouldBe(BinaryOperator.AndAlso);
        and.Left.ShouldBeOfType<IsExpression>();
        and.Right.ShouldBeOfType<BinaryExpression>().Operator.ShouldBe(BinaryOperator.Equal);
    }

    [Fact]
    public void LooserThanEquality()
    {
        // `a == b is C` deve parsear como `(a == b) is C` — `is` é mais fraco
        // que `==` (§25.7).
        var isExpr = DefValue("def b = a == b is C;").ShouldBeOfType<IsExpression>();

        isExpr.Scrutinee.ShouldBeOfType<BinaryExpression>().Operator.ShouldBe(BinaryOperator.Equal);
    }

    [Fact]
    public void DoesNotChain_ReportsLap0114() =>
        Codes("def b = a is P is Q;").ShouldContain(DiagnosticCodes.ChainedComparison);
}

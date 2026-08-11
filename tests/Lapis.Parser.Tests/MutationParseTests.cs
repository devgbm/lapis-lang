using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

/// <summary><c>var</c> e reatribuição (Q25).</summary>
public sealed class MutationParseTests : ParserTestBase
{
    private static Statement Single(string source) => Parse(source).Statements.ShouldHaveSingleItem();

    [Fact]
    public void Var_IsADefStatementMarkedMutable() =>
        Single("var x = 1;").ShouldBeOfType<DefStatement>().IsMutable.ShouldBeTrue();

    [Fact]
    public void Def_IsNotMutable() =>
        Single("def x = 1;").ShouldBeOfType<DefStatement>().IsMutable.ShouldBeFalse();

    [Fact]
    public void Var_AcceptsAnnotation() =>
        Single("var x: Int = 1;").ShouldBeOfType<DefStatement>().Annotation.ShouldNotBeNull();

    [Fact]
    public void Assignment_IsParsed()
    {
        var assign = Single("x = 2;").ShouldBeOfType<AssignStatement>();

        assign.Name.ShouldBe("x");
        assign.Value.ShouldBeOfType<IntLiteral>().Value.ShouldBe(2);
    }

    [Fact]
    public void Assignment_RequiresSemicolon() =>
        Codes("x = 2").ShouldContain(DiagnosticCodes.ExpectedSemicolon);

    /// <summary>
    /// <c>==</c> é outro token, então comparação nunca é confundida com atribuição
    /// — e é por isso que a atribuição não precisa de sintaxe própria.
    /// </summary>
    [Fact]
    public void Comparison_IsNotAnAssignment() =>
        Single("x == 2;").ShouldBeOfType<ExpressionStatement>();

    /// <summary>
    /// Atribuição é statement, não expressão: <c>if (x = 1)</c> não existe, e a
    /// cauda de um bloco não pode ser uma atribuição.
    /// </summary>
    [Fact]
    public void Assignment_IsNotAnExpression() =>
        Codes("def x = { var a = 1; a = 2 };").ShouldContain(DiagnosticCodes.ExpectedSemicolon);

    [Fact]
    public void Var_RequiresIdentifier() =>
        Codes("var 1 = 2;").ShouldContain(DiagnosticCodes.ExpectedIdentifier);

    [Fact]
    public void Var_RequiresEquals() =>
        Codes("var x 1;").ShouldContain(DiagnosticCodes.ExpectedEquals);

    [Fact]
    public void Assignment_InsideBlock()
    {
        var block = Parse("def f = fn() Void { var x = 1; x = 2; };")
            .Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<FunctionExpression>()
            .Body.ShouldBeOfType<BlockExpression>();

        block.Statements[0].ShouldBeOfType<DefStatement>().IsMutable.ShouldBeTrue();
        block.Statements[1].ShouldBeOfType<AssignStatement>();
    }
}

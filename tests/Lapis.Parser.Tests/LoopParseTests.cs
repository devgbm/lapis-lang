using Lapis.Ast;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

/// <summary><c>loop</c>, <c>break</c> e <c>continue</c> (plano 26, M16 — Q32).</summary>
public sealed class LoopParseTests : ParserTestBase
{
    private static Expression SingleExpression(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>().Expression;

    [Fact]
    public void Loop_Unlabeled()
    {
        var loop = SingleExpression("loop { break; };").ShouldBeOfType<LoopExpression>();

        loop.Label.ShouldBeNull();
        loop.Body.Statements.ShouldHaveSingleItem();
    }

    [Fact]
    public void Loop_Labeled() =>
        SingleExpression("loop :fora { break; };").ShouldBeOfType<LoopExpression>().Label.ShouldBe("fora");

    [Fact]
    public void Break_Bare()
    {
        var jump = SingleExpression("break;").ShouldBeOfType<BreakExpression>();

        jump.Label.ShouldBeNull();
        jump.Value.ShouldBeNull();
    }

    [Fact]
    public void Break_WithValue()
    {
        var jump = SingleExpression("break 5;").ShouldBeOfType<BreakExpression>();

        jump.Label.ShouldBeNull();
        jump.Value.ShouldBeOfType<IntLiteral>().Value.ShouldBe(5);
    }

    [Fact]
    public void Break_WithLabel()
    {
        var jump = SingleExpression("break :fora;").ShouldBeOfType<BreakExpression>();

        jump.Label.ShouldBe("fora");
        jump.Value.ShouldBeNull();
    }

    [Fact]
    public void Break_WithLabelAndValue()
    {
        var jump = SingleExpression("break :fora, 5;").ShouldBeOfType<BreakExpression>();

        jump.Label.ShouldBe("fora");
        jump.Value.ShouldBeOfType<IntLiteral>().Value.ShouldBe(5);
    }

    /// <summary>Sem a vírgula, o rótulo e o valor seriam ambíguos de separar.</summary>
    [Fact]
    public void Break_LabelAndValue_RequiresComma() =>
        Codes("break :fora 5;").ShouldNotBeEmpty();

    [Fact]
    public void Continue_Bare() =>
        SingleExpression("continue;").ShouldBeOfType<ContinueExpression>().Label.ShouldBeNull();

    [Fact]
    public void Continue_WithLabel() =>
        SingleExpression("continue :fora;").ShouldBeOfType<ContinueExpression>().Label.ShouldBe("fora");

    [Fact]
    public void Break_InExpressionPosition_IsFine() =>
        Parse("def x = loop { break 5; };");

    // ------------------------------------------------- goto/label saíram

    [Fact]
    public void Goto_NoLongerParses() =>
        Codes("goto x;").ShouldNotBeEmpty();

    [Fact]
    public void Label_NoLongerParses() =>
        Codes("label x;").ShouldNotBeEmpty();

    /// <summary><c>goto</c> volta a valer como identificador comum.</summary>
    [Fact]
    public void Goto_IsNoLongerReserved() =>
        Parse("def goto = 1;\nprint(goto);");
}

/// <summary><c>if</c> sem chaves e o guard de dangling-else (plano 26 §26.9).</summary>
public sealed class IfBodyParseTests : ParserTestBase
{
    private static IfExpression SingleIf(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<IfExpression>();

    [Fact]
    public void BareThen_AcceptsPlainExpression()
    {
        var expression = SingleIf("if c break;");

        expression.Then.ShouldBeOfType<BreakExpression>();
    }

    [Fact]
    public void BareThen_RequiresSemicolon() =>
        Codes("if c break").ShouldContain(DiagnosticCodes.ExpectedSemicolon);

    [Fact]
    public void Block_DispensesSemicolon() =>
        Codes("if c { break; }").ShouldBeEmpty();

    [Fact]
    public void BareElse_AcceptsPlainExpression()
    {
        var expression = SingleIf("if c break; else continue;");

        expression.Else.ShouldBeOfType<ContinueExpression>();
    }

    /// <summary>Último ramo bloco: dispensa <c>;</c>, mesma regra de sempre (Q16).</summary>
    [Fact]
    public void BareThen_BlockElse_DispensesSemicolon() =>
        Codes("if c break; else { continue; }").ShouldBeEmpty();

    /// <summary><c>if</c> sem chaves não pode ter outro <c>if</c> como corpo direto.</summary>
    [Fact]
    public void BareThen_CannotBeBareIf() =>
        Codes("if a if b break;").ShouldContain(DiagnosticCodes.BareIfCannotHaveBareIfBody);

    /// <summary>Com chaves, aninhar continua livre — não há ambiguidade nenhuma.</summary>
    [Fact]
    public void BracedNestedIf_IsFine() =>
        Codes("if a { if b break; }").ShouldBeEmpty();

    /// <summary><c>else if</c> encadeia sem chaves — não é a mesma posição do dangling-else.</summary>
    [Fact]
    public void ElseIf_ChainsWithoutBraces()
    {
        var expression = SingleIf("if a break; else if b continue; else break;");

        var chained = expression.Else.ShouldBeOfType<IfExpression>();
        chained.Then.ShouldBeOfType<ContinueExpression>();
        chained.Else.ShouldBeOfType<BreakExpression>();
    }
}

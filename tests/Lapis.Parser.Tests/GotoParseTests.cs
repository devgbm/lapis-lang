using Lapis.Ast;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

/// <summary><c>goto</c> e <c>label</c> (plano 16 §16.3).</summary>
public sealed class GotoParseTests : ParserTestBase
{
    private static Statement Single(string source) => Parse(source).Statements.ShouldHaveSingleItem();

    [Fact]
    public void Goto_Unconditional()
    {
        var jump = Single("goto fim;").ShouldBeOfType<GotoStatement>();

        jump.Label.ShouldBe("fim");
        jump.Condition.ShouldBeNull();
    }

    /// <summary>
    /// O <c>if</c> aqui é o mesmo token do <c>if</c> expressão: o <c>goto</c> já
    /// determinou a produção, então não há ambiguidade a resolver.
    /// </summary>
    [Fact]
    public void Goto_Conditional()
    {
        var jump = Single("goto fim if x > 0;").ShouldBeOfType<GotoStatement>();

        jump.Label.ShouldBe("fim");
        jump.Condition.ShouldBeOfType<BinaryExpression>().Operator.ShouldBe(BinaryOperator.Greater);
    }

    [Fact]
    public void Label_Declaration() =>
        Single("label fim;").ShouldBeOfType<LabelStatement>().Label.ShouldBe("fim");

    [Fact]
    public void Goto_RequiresSemicolon() =>
        Codes("goto fim").ShouldContain(DiagnosticCodes.ExpectedSemicolon);

    [Fact]
    public void Goto_RequiresIdentifier() =>
        Codes("goto 1;").ShouldContain(DiagnosticCodes.ExpectedIdentifier);

    /// <summary>Um salto não é expressão: <c>def x = goto L;</c> não quer dizer nada.</summary>
    [Fact]
    public void Goto_IsNotAnExpression() =>
        Codes("def x = goto fim;").ShouldContain(DiagnosticCodes.ExpectedExpression);

    [Fact]
    public void Goto_InsideBlock()
    {
        var block = Parse("def f = fn() Void { goto fim; label fim; };")
            .Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<FunctionExpression>()
            .Body.ShouldBeOfType<BlockExpression>();

        block.Statements.Length.ShouldBe(2);
        block.Statements[0].ShouldBeOfType<GotoStatement>();
        block.Statements[1].ShouldBeOfType<LabelStatement>();
    }

    // ------------------------------------------- `label` é contextual

    /// <summary>
    /// Reservar <c>label</c> quebraria o exemplo da própria spec §13, que a usa
    /// como nome de campo. Estes três testes são a razão de ela ser contextual.
    /// </summary>
    [Fact]
    public void Label_IsStillValidAsFieldName() =>
        Parse("def T = type { label: Str; };");

    [Fact]
    public void Label_IsStillValidAsVariableName() =>
        Parse("def label = 1;\nprint(label);");

    [Fact]
    public void Label_AloneIsAnExpression() =>
        Parse("def label = 1;\nlabel;").Statements[1].ShouldBeOfType<ExpressionStatement>();

    /// <summary>
    /// <c>goto</c>, ao contrário, é reservada — é o que permite o diagnóstico
    /// preciso de <see cref="Goto_RequiresIdentifier"/>.
    /// </summary>
    [Fact]
    public void Goto_IsReserved() =>
        Codes("def goto = 1;").ShouldContain(DiagnosticCodes.ExpectedIdentifier);
}

using Lapis.Diagnostics;

namespace Lapis.Lexer.Tests;

/// <summary>
/// Aspas simples (Q35/Q38).
///
/// A lexer entrega o conteúdo <b>sem julgá-lo</b>: em expressão o token é um
/// literal de <c>Char</c>, e dentro do <c>match</c> de uma macro será uma
/// pseudo-palavra-chave, onde o conteúdo é um identificador inteiro. Quem separa
/// os dois é o parser, que sabe onde está — dar modos à lexer para saber o mesmo
/// custaria mais do que a checagem tardia.
/// </summary>
public sealed class CharLiteralTests : LexerTestBase
{
    [Fact]
    public void Char_Simple() => Single("'a'").StringValue.ShouldBe("a");

    [Fact]
    public void Char_IsItsOwnKind() => Single("'a'").Kind.ShouldBe(TokenKind.CharLiteral);

    /// <summary>Um ponto de código fora do plano básico chega inteiro.</summary>
    [Fact]
    public void Char_Astral() => Single("'𝕏'").StringValue.ShouldBe("𝕏");

    /// <summary>
    /// Cada aspa só se escapa dentro do literal que ela delimita: <c>'\''</c>
    /// precisa da barra, <c>'"'</c> não. É a mesma regra da string, espelhada.
    /// </summary>
    [Fact]
    public void Char_EscapedQuote() => Single("'\\''").StringValue.ShouldBe("'");

    [Fact]
    public void Char_DoubleQuoteNeedsNoEscape() => Single("'\"'").StringValue.ShouldBe("\"");

    [Fact]
    public void Char_Escapes_AreDecoded()
    {
        Single("'\\n'").StringValue.ShouldBe("\n");
        Single("'\\t'").StringValue.ShouldBe("\t");
        Single("'\\r'").StringValue.ShouldBe("\r");
        Single("'\\0'").StringValue.ShouldBe("\0");
        Single("'\\\\'").StringValue.ShouldBe("\\");
    }

    [Fact]
    public void Char_UnknownEscape_ReportsLap0003()
    {
        var (_, diagnostics) = Lex("'\\q'");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnknownEscapeSequence);
    }

    /// <summary>
    /// A barra na aspa <b>errada</b> é escape desconhecido, dos dois lados: é o
    /// que mantém as duas formas simétricas em vez de uma tolerar a outra.
    /// </summary>
    [Fact]
    public void Char_EscapedDoubleQuote_IsUnknown()
    {
        var (_, diagnostics) = Lex("'\\\"'");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnknownEscapeSequence);
    }

    [Fact]
    public void Str_EscapedSingleQuote_IsUnknown()
    {
        var (_, diagnostics) = Lex("\"\\'\"");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnknownEscapeSequence);
    }

    [Fact]
    public void Char_Unterminated_ReportsLap0007_AndStopsAtLineEnd()
    {
        var (tokens, diagnostics) = Lex("'ab\nresto");

        diagnostics.ShouldContain(d => d.Code == DiagnosticCodes.UnterminatedCharLiteral);
        tokens[0].Kind.ShouldBe(TokenKind.CharLiteral);
        tokens[1].Kind.ShouldBe(TokenKind.Identifier);
        tokens[1].Text.ShouldBe("resto");
    }

    /// <summary>
    /// A lexer aceita conteúdo de qualquer tamanho — é o que deixa a
    /// pseudo-palavra-chave da Q38 caber neste mesmo token sem nova sintaxe.
    /// </summary>
    [Fact]
    public void Char_MultipleCodepoints_LexesWithoutComplaint()
    {
        var (tokens, diagnostics) = Lex("'pseudopalavra'");

        diagnostics.ShouldBeEmpty();
        tokens[0].Kind.ShouldBe(TokenKind.CharLiteral);
        tokens[0].StringValue.ShouldBe("pseudopalavra");
    }

    [Fact]
    public void Char_Empty_LexesWithoutComplaint()
    {
        var (tokens, diagnostics) = Lex("''");

        diagnostics.ShouldBeEmpty();
        tokens[0].StringValue.ShouldBe(string.Empty);
    }
}

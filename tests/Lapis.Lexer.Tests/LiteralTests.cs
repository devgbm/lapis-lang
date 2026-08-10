using System.Globalization;
using Lapis.Diagnostics;

namespace Lapis.Lexer.Tests;

public sealed class LiteralTests : LexerTestBase
{
    // ------------------------------------------------------------- inteiros

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1", 1L)]
    [InlineData("42", 42L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void Int_IsParsed(string source, long expected) =>
        Single(source).IntegerValue.ShouldBe(expected);

    [Fact]
    public void Int_Overflow_ReportsLap0002_WithoutThrowing()
    {
        var (tokens, diagnostics) = Lex("9223372036854775808");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.IntegerLiteralOutOfRange);
        tokens[0].Kind.ShouldBe(TokenKind.IntegerLiteral);
        tokens[0].IntegerValue.ShouldBe(0);
    }

    /// <summary>A spec §6 chama <c>-10</c> de literal; o lexer produz dois tokens e o parser dobra.</summary>
    [Fact]
    public void NegativeInt_IsTwoTokens() =>
        Kinds("-10").ShouldBe([TokenKind.Minus, TokenKind.IntegerLiteral]);

    // --------------------------------------------------------------- floats

    [Theory]
    [InlineData("1.0", 1.0)]
    [InlineData("3.14", 3.14)]
    [InlineData("0.5", 0.5)]
    public void Float_IsParsed(string source, double expected) =>
        Single(source).FloatValue.ShouldBe(expected);

    /// <summary>
    /// <c>double.TryParse</c> devolve <c>true</c> com <c>Infinity</c> quando o
    /// literal estoura o Float. Aceitar isso seria surpresa dupla: o valor não é
    /// o que está escrito, e <c>Infinity</c> não é escrevível na linguagem — logo
    /// imprimi-lo produziria texto que não reparseia.
    /// </summary>
    [Fact]
    public void Float_Overflow_ReportsLap0006_WithoutBecomingInfinity()
    {
        var (tokens, diagnostics) = Lex("2" + new string('0', 308) + ".0");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.MalformedFloatLiteral);
        tokens[0].Kind.ShouldBe(TokenKind.FloatLiteral);
        tokens[0].FloatValue.ShouldBe(0);
    }

    /// <summary>No limite do intervalo ainda é um literal válido.</summary>
    [Fact]
    public void Float_AtMaxValue_IsAccepted()
    {
        var (tokens, diagnostics) = Lex("17976931348623157" + new string('0', 292) + ".0");

        diagnostics.ShouldBeEmpty();
        tokens[0].FloatValue.ShouldBe(double.MaxValue);
    }

    [Fact]
    public void NegativeFloat_IsTwoTokens() =>
        Kinds("-0.5").ShouldBe([TokenKind.Minus, TokenKind.FloatLiteral]);

    /// <summary>Sem dígito depois do ponto não é float — senão <c>1.foo</c> quebraria.</summary>
    [Fact]
    public void Float_RequiresDigitAfterDot() =>
        Kinds("1.foo").ShouldBe([TokenKind.IntegerLiteral, TokenKind.Dot, TokenKind.Identifier]);

    [Fact]
    public void Float_TrailingDot_IsIntegerThenDot() =>
        Kinds("1.").ShouldBe([TokenKind.IntegerLiteral, TokenKind.Dot]);

    /// <summary>
    /// A solução roda em <c>InvariantGlobalization</c>, então nem dá para instalar
    /// uma cultura pt-BR para testar — a própria exceção seria a prova. O que se
    /// verifica aqui é o comportamento: vírgula nunca é separador decimal.
    /// </summary>
    [Fact]
    public void Float_CommaIsNeverADecimalSeparator() =>
        Kinds("3,14").ShouldBe([TokenKind.IntegerLiteral, TokenKind.Comma, TokenKind.IntegerLiteral]);

    [Fact]
    public void Globalization_IsInvariant() =>
        CultureInfo.CurrentCulture.Name.ShouldBeEmpty();

    // -------------------------------------------------------------- strings

    [Fact]
    public void Str_Simple() => Single("\"hello world\"").StringValue.ShouldBe("hello world");

    [Fact]
    public void Str_Empty() => Single("\"\"").StringValue.ShouldBe(string.Empty);

    [Fact]
    public void Str_Escapes_AreDecoded() =>
        Single("""
               "a\nb\tc\"d\\e"
               """).StringValue.ShouldBe("a\nb\tc\"d\\e");

    [Fact]
    public void Str_NullEscape() => Single("\"a\\0b\"").StringValue.ShouldBe("a\0b");

    [Fact]
    public void Str_CarriageReturnEscape() => Single("\"a\\rb\"").StringValue.ShouldBe("a\rb");

    [Fact]
    public void Str_UnknownEscape_ReportsLap0003()
    {
        var (_, diagnostics) = Lex("\"a\\q\"");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnknownEscapeSequence);
    }

    [Fact]
    public void Str_Unterminated_ReportsLap0004_AndStopsAtLineEnd()
    {
        var (tokens, diagnostics) = Lex("\"abc\nresto");

        diagnostics.ShouldContain(d => d.Code == DiagnosticCodes.UnterminatedString);
        tokens[0].Kind.ShouldBe(TokenKind.StringLiteral);
        tokens[0].StringValue.ShouldBe("abc");
        tokens[1].Kind.ShouldBe(TokenKind.Identifier);
        tokens[1].Text.ShouldBe("resto");
    }

    [Fact]
    public void Str_UnterminatedAtEof_ReportsLap0004()
    {
        var (_, diagnostics) = Lex("\"abc");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnterminatedString);
    }

    // --------------------------------------------------------------- bool

    [Fact]
    public void Bool_Keywords() =>
        Kinds("true false").ShouldBe([TokenKind.TrueKeyword, TokenKind.FalseKeyword]);
}

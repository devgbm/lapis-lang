using System.Text;
using Lapis.Diagnostics;

namespace Lapis.Lexer.Tests;

public sealed class RobustnessTests : LexerTestBase
{
    [Fact]
    public void UnknownChar_ProducesBadAndProgresses()
    {
        var (tokens, diagnostics) = Lex("def x = # ;");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnexpectedCharacter);
        tokens.Select(t => t.Kind).ShouldContain(TokenKind.Bad);
        tokens[^1].Kind.ShouldBe(TokenKind.EndOfFile);
    }

    [Fact]
    public void MultipleUnknownChars_AllReported()
    {
        var (_, diagnostics) = Lex("~ # $");

        diagnostics.Length.ShouldBe(3);
        diagnostics.ShouldAllBe(d => d.Code == DiagnosticCodes.UnexpectedCharacter);
    }

    /// <summary>
    /// Um único <c>&amp;</c> ou <c>|</c> não é operador na 0.2. <c>@</c> saiu desta
    /// lista no M8: passou a ser o token que inicia uma invocação de macro.
    /// </summary>
    [Theory]
    [InlineData("&")]
    [InlineData("|")]
    [InlineData("~")]
    [InlineData("%")]
    [InlineData("^")]
    [InlineData("$")]
    [InlineData("#")]
    [InlineData("?")]
    public void UnsupportedSymbols_AreBad(string source)
    {
        var (tokens, diagnostics) = Lex(source);

        tokens[0].Kind.ShouldBe(TokenKind.Bad);
        diagnostics.ShouldHaveSingleItem();
    }

    /// <summary>E `@` agora é um token de verdade, não um caractere inesperado.</summary>
    [Fact]
    public void At_IsAToken()
    {
        var (tokens, diagnostics) = Lex("@");

        tokens[0].Kind.ShouldBe(TokenKind.At);
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\0")]
    [InlineData("\"")]
    [InlineData("/*")]
    [InlineData("\\\\\\")]
    [InlineData("999999999999999999999999")]
    [InlineData("é ü ñ")]
    [InlineData("def def def def")]
    [InlineData("...........")]
    public void Lexer_NeverThrows(string source) =>
        Should.NotThrow(() => Lex(source));

    [Fact]
    public void Lexer_AlwaysProgresses_OnArbitraryBytes()
    {
        var random = new Random(20260810);
        var builder = new StringBuilder();

        for (var i = 0; i < 5_000; i++)
        {
            builder.Append((char)random.Next(32, 127));
        }

        var source = builder.ToString();
        var (tokens, _) = Lex(source);

        // Cada token consome pelo menos um caractere, mais o EndOfFile.
        tokens.Length.ShouldBeLessThanOrEqualTo(source.Length + 1);
        tokens[^1].Kind.ShouldBe(TokenKind.EndOfFile);
    }

    [Fact]
    public void Lexer_IsDeterministic()
    {
        const string Source = "def add = fn(a: Int) Int { return a + 1; };";

        var first = Lex(Source).Tokens;
        var second = Lex(Source).Tokens;

        first.ShouldBe(second);
    }
}

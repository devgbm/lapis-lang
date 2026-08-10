using Lapis.Diagnostics;

namespace Lapis.Lexer.Tests;

public sealed class TriviaTests : LexerTestBase
{
    [Fact]
    public void LineComment_IsSkipped() =>
        Kinds("def x // nota").ShouldBe([TokenKind.DefKeyword, TokenKind.Identifier]);

    [Fact]
    public void LineComment_AtEofWithoutNewline_IsClean() => Lex("// só um comentário").Diagnostics.ShouldBeEmpty();

    [Fact]
    public void LineComment_EndsAtNewline() =>
        Kinds("// nota\ndef").ShouldBe([TokenKind.DefKeyword]);

    [Fact]
    public void BlockComment_IsSkipped() =>
        Kinds("/* nota */ def").ShouldBe([TokenKind.DefKeyword]);

    [Fact]
    public void BlockComment_Multiline_IsSkipped() =>
        Kinds("/* a\nb\nc */ def").ShouldBe([TokenKind.DefKeyword]);

    /// <summary>Comentários de bloco não aninham: fecham no primeiro <c>*/</c>.</summary>
    [Fact]
    public void BlockComment_DoesNotNest() =>
        Kinds("/* /* */ x").ShouldBe([TokenKind.Identifier]);

    [Fact]
    public void BlockComment_Unterminated_ReportsLap0005()
    {
        var (_, diagnostics) = Lex("/* sem fim");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnterminatedBlockComment);
    }

    [Fact]
    public void Newlines_ProduceNoTokens() =>
        Kinds("def\n\nx").ShouldBe([TokenKind.DefKeyword, TokenKind.Identifier]);

    [Fact]
    public void EmptyFile_ProducesOnlyEndOfFile()
    {
        var (tokens, diagnostics) = Lex(string.Empty);

        diagnostics.ShouldBeEmpty();
        tokens.Length.ShouldBe(1);
        tokens[0].Kind.ShouldBe(TokenKind.EndOfFile);
    }

    [Fact]
    public void OnlyComments_ProducesOnlyEndOfFile() =>
        Lex("// a\n/* b */").Tokens.Length.ShouldBe(1);
}

public sealed class SpanTests : LexerTestBase
{
    [Fact]
    public void Span_OffsetAndLength_AreExact()
    {
        //                     0123456789
        var tokens = LexClean("def x = 10;");

        tokens[3].Span.ShouldBe(new SourceSpan(8, 2));
        tokens[3].Text.ShouldBe("10");
    }

    [Fact]
    public void LineAndColumn_AreOneBased()
    {
        var source = SourceText.From("def x = 1;");
        var tokens = LexClean(source.Text);

        var position = source.GetPosition(tokens[0].Span.Start);
        position.Line.ShouldBe(1);
        position.Column.ShouldBe(1);
    }

    [Fact]
    public void Position_AfterLf_CountsNewLine()
    {
        var source = SourceText.From("def\nx");
        var tokens = LexClean(source.Text);

        var position = source.GetPosition(tokens[1].Span.Start);
        position.Line.ShouldBe(2);
        position.Column.ShouldBe(1);
    }

    [Fact]
    public void Position_AfterCrLf_CountsOneNewLine()
    {
        var source = SourceText.From("def\r\nx");
        var tokens = LexClean(source.Text);

        var position = source.GetPosition(tokens[1].Span.Start);
        position.Line.ShouldBe(2);
        position.Column.ShouldBe(1);
    }

    [Fact]
    public void EofToken_HasZeroLengthSpanAtEnd()
    {
        const string Source = "def x;";
        var (tokens, _) = Lex(Source);

        tokens[^1].Span.ShouldBe(new SourceSpan(Source.Length, 0));
    }

    /// <summary>Propriedade: o texto do span sempre bate com o lexema.</summary>
    [Theory]
    [InlineData("def add = fn(a: Int, b: Int) Int { return a + b; };")]
    [InlineData("if x <= 0 { return -x; } else { return x; }")]
    [InlineData("\"texto\" + 1.5 * 2 != true")]
    [InlineData("match r { Ok(v) => v, _ => 0 }")]
    public void EveryToken_SpanTextMatchesSource(string source)
    {
        var text = SourceText.From(source);
        var (tokens, _) = Lex(source);

        foreach (var token in tokens[..^1])
        {
            text.GetText(token.Span).ShouldBe(token.Text);
        }
    }
}

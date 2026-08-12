namespace Lapis.Lexer.Tests;

public sealed class TokenKindTests : LexerTestBase
{
    [Theory]
    [InlineData("def", TokenKind.DefKeyword)]
    [InlineData("fn", TokenKind.FnKeyword)]
    [InlineData("type", TokenKind.TypeKeyword)]
    [InlineData("enum", TokenKind.EnumKeyword)]
    [InlineData("return", TokenKind.ReturnKeyword)]
    [InlineData("true", TokenKind.TrueKeyword)]
    [InlineData("false", TokenKind.FalseKeyword)]
    [InlineData("if", TokenKind.IfKeyword)]
    [InlineData("else", TokenKind.ElseKeyword)]
    [InlineData("match", TokenKind.MatchKeyword)]
    [InlineData("throw", TokenKind.ThrowKeyword)]
    [InlineData("loop", TokenKind.LoopKeyword)]
    [InlineData("break", TokenKind.BreakKeyword)]
    [InlineData("continue", TokenKind.ContinueKeyword)]
    public void Keywords_AreRecognized(string source, TokenKind expected) =>
        Single(source).Kind.ShouldBe(expected);

    [Theory]
    [InlineData("define")]
    [InlineData("fnx")]
    [InlineData("returns")]
    [InlineData("type_")]
    [InlineData("_def")]
    [InlineData("ifx")]
    [InlineData("Int")]
    [InlineData("Result")]
    [InlineData("throws")]
    [InlineData("loops")]
    [InlineData("breakpoint")]
    [InlineData("continuation")]
    public void NearKeywords_AreIdentifiers(string source) =>
        Single(source).Kind.ShouldBe(TokenKind.Identifier);

    [Theory]
    [InlineData("(", TokenKind.OpenParen)]
    [InlineData(")", TokenKind.CloseParen)]
    [InlineData("{", TokenKind.OpenBrace)]
    [InlineData("}", TokenKind.CloseBrace)]
    [InlineData("[", TokenKind.OpenBracket)]
    [InlineData("]", TokenKind.CloseBracket)]
    public void Delimiters_AreRecognized(string source, TokenKind expected) =>
        Single(source).Kind.ShouldBe(expected);

    [Theory]
    [InlineData(":", TokenKind.Colon)]
    [InlineData(",", TokenKind.Comma)]
    [InlineData(";", TokenKind.Semicolon)]
    [InlineData("=", TokenKind.Equals)]
    [InlineData(".", TokenKind.Dot)]
    [InlineData("_", TokenKind.Underscore)]
    [InlineData("=>", TokenKind.FatArrow)]

    // O tamanho desconhecido de um span, `[Int;?]` (plano 24). É o único uso de
    // `?` na linguagem — não há operador ternário nem tipo opcional.
    [InlineData("?", TokenKind.Question)]
    public void Punctuation_IsRecognized(string source, TokenKind expected) =>
        Single(source).Kind.ShouldBe(expected);

    [Theory]
    [InlineData("+", TokenKind.Plus)]
    [InlineData("-", TokenKind.Minus)]
    [InlineData("*", TokenKind.Star)]
    [InlineData("/", TokenKind.Slash)]
    [InlineData("<", TokenKind.Less)]
    [InlineData(">", TokenKind.Greater)]
    [InlineData("!", TokenKind.Bang)]
    public void SingleCharOperators_AreRecognized(string source, TokenKind expected) =>
        Single(source).Kind.ShouldBe(expected);

    [Theory]
    [InlineData("==", TokenKind.EqualsEquals)]
    [InlineData("!=", TokenKind.BangEquals)]
    [InlineData("<=", TokenKind.LessEquals)]
    [InlineData(">=", TokenKind.GreaterEquals)]
    [InlineData("&&", TokenKind.AmpersandAmpersand)]
    [InlineData("||", TokenKind.PipePipe)]
    public void TwoCharOperators_AreRecognized(string source, TokenKind expected) =>
        Single(source).Kind.ShouldBe(expected);

    [Fact]
    public void MaximalMunch_EqualsEquals_IsOneToken() =>
        Kinds("==").ShouldBe([TokenKind.EqualsEquals]);

    [Fact]
    public void MaximalMunch_FatArrow_IsNotEqualsAndGreater() =>
        Kinds("=>").ShouldBe([TokenKind.FatArrow]);

    [Fact]
    public void MaximalMunch_LessEquals_IsNotLessAndEquals() =>
        Kinds("<=").ShouldBe([TokenKind.LessEquals]);

    [Fact]
    public void Assignment_FollowedByEquality_SplitsCorrectly() =>
        Kinds("= ==").ShouldBe([TokenKind.Equals, TokenKind.EqualsEquals]);

    /// <summary>
    /// Não existe token <c>&gt;&gt;</c>: `Box&lt;Box&lt;Int&gt;&gt;` fecha com dois
    /// <c>Greater</c> independentes (plano 03 §3.1).
    /// </summary>
    [Fact]
    public void NestedGenerics_CloseWithTwoGreaterTokens() =>
        Kinds("Box<Box<Int>>").ShouldBe([
            TokenKind.Identifier, TokenKind.Less,
            TokenKind.Identifier, TokenKind.Less,
            TokenKind.Identifier, TokenKind.Greater, TokenKind.Greater,
        ]);

    [Fact]
    public void UnderscorePrefixedName_IsIdentifierNotUnderscore() =>
        Single("_x").Kind.ShouldBe(TokenKind.Identifier);

    [Fact]
    public void EndOfFile_IsAlwaysLast()
    {
        var (tokens, _) = Lex("def x = 1;");

        tokens[^1].Kind.ShouldBe(TokenKind.EndOfFile);
    }
}

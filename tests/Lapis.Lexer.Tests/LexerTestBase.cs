using System.Collections.Immutable;
using Lapis.Diagnostics;

namespace Lapis.Lexer.Tests;

public abstract class LexerTestBase
{
    protected static (ImmutableArray<Token> Tokens, ImmutableArray<Diagnostic> Diagnostics) Lex(string source)
    {
        var diagnostics = new DiagnosticBag();
        var tokens = Lexer.Tokenize(SourceText.From(source), diagnostics);
        return (tokens, diagnostics.ToSortedArray());
    }

    /// <summary>Tokens sem o <c>EndOfFile</c> final.</summary>
    protected static ImmutableArray<Token> LexClean(string source)
    {
        var (tokens, diagnostics) = Lex(source);
        diagnostics.ShouldBeEmpty();
        return [.. tokens[..^1]];
    }

    protected static Token Single(string source)
    {
        var tokens = LexClean(source);
        tokens.Length.ShouldBe(1);
        return tokens[0];
    }

    protected static ImmutableArray<TokenKind> Kinds(string source) =>
        [.. LexClean(source).Select(t => t.Kind)];
}

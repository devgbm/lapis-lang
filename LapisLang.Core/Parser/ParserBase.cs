using System;
using System.Collections.Immutable;
using System.Linq;

namespace LapisLang.Core;

public abstract class ParserBase
{
    private readonly TokenStream _tokenStream;

    public ParserBase(TokenStream tokenStream)
    {
        Diagnostics = new();
        _tokenStream = tokenStream;
    }
    public DiagnosticsBag Diagnostics { get; }

    protected Token Peek(int offset) => _tokenStream.Peek(offset);
    protected Token NextToken() => _tokenStream.NextToken();
    protected Token Current => _tokenStream.Current;

    protected Token? MatchOptional(TokenKind kind)
    {
        if(Current.Kind == kind) return NextToken();
        return null;
    }
    protected bool Not(TokenKind kind)
    {
        return Current.Kind != kind;
    }
    protected bool Is(TokenKind kind)
    {
        return Current.Kind == kind;
    }
    protected Token Match(TokenKind kind)
    {
        if(Current.Kind == kind) return NextToken();

        Diagnostics.Report($"ERROR: Unexpected token <{Current.Kind}>, expected <{kind}>", Current.SourceSpan);
        NextToken();
        return new Token(kind, Current.SourceSpan);
    }

    protected ImmutableArray<T> MatchUntil<T>(TokenKind kind, Func<T> action)
    {
        var arr = ImmutableArray.CreateBuilder<T>();
        while(Current.Kind != kind && Current.Kind != TokenKind.EoFToken)
        {
            var matched = action();
            arr.Add(matched);
        }
        return arr.ToImmutableArray();
    }
    protected Token MatchAny(params TokenKind[] tokens)
    {
        if(tokens.Contains(Current.Kind)) return NextToken();
        Diagnostics.Report($"ERROR: Unexpected token <{Current.Kind}>, expected <{string.Join(" or ", tokens)}>", Current.SourceSpan);
        NextToken();
        return new Token(tokens[0], Current.SourceSpan);
    }
}

using System.Diagnostics;

namespace LapisLang.Core;

[DebuggerDisplay("<Token {String} {Value}>")]
public class Token 
{
    public Token(TokenKind kind, SourceSpan span)
    {
        Kind = kind;
        SourceSpan = span;
    }

    public TokenKind Kind { get; }

    public SourceSpan SourceSpan { get; }

    public string String => SourceSpan.AsText.ToString();

    public override string ToString()
    {
        return $"<{Kind}, {SourceSpan}>";
        
    }
}

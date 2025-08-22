using System.Diagnostics;

namespace LapisLang.Core;

[DebuggerDisplay("<{GetType().Name} {SourceSpan.AsText}>")]
public abstract class ExpressionSyntax : Syntax;


public class UnkownExpressionSyntax : ExpressionSyntax
{
    public UnkownExpressionSyntax(SourceSpan span)
    {
        SourceSpan = span;
    }
    public override SourceSpan SourceSpan { get; }
}
using System.Collections.Immutable;

namespace LapisLang.Core;
public abstract class BoundExpression : BoundSyntax
{
    public abstract TypeRune Type { get; }
}


public class BoundCallExpression : BoundExpression
{
    public BoundCallExpression(
        SourceSpan span,
        BoundExpression expression,
        ImmutableArray<BoundExpression> arguments
    )
    {
        Span = span;
        Expression = expression;
        Arguments = arguments;
    }

    public override TypeRune Type => throw new NotImplementedException();

    public override SourceSpan Span { get; }
    public BoundExpression Expression { get; }
    public ImmutableArray<BoundExpression> Arguments { get; }
}
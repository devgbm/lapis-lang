using System.Collections.Immutable;

namespace LapisLang.Core;

public class CallExpressionSyntax : ExpressionSyntax
{
    public CallExpressionSyntax(
        SourceSpan sourceSpan,
        ExpressionSyntax expression,
        ImmutableArray<ExpressionSyntax> parameters
    )
    {
        SourceSpan = sourceSpan;
        Expression = expression;
        Parameters = parameters;
    }

    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax Expression { get; }
    public ImmutableArray<ExpressionSyntax> Parameters { get; }
}
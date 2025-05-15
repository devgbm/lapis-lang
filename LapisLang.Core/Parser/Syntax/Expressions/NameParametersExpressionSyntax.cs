using System.Collections.Immutable;

namespace LapisLang.Core;

public class NameParametersExpressionSyntax : ExpressionSyntax
{
    public NameParametersExpressionSyntax(
        SourceSpan sourceSpan,
        ExpressionSyntax expression,
        ImmutableArray<ParameterExpression> parameters
    )
    {
        SourceSpan = sourceSpan;
        Expression = expression;
        Parameters = parameters;
    }

    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax Expression { get; }
    public ImmutableArray<ParameterExpression> Parameters { get; }
}
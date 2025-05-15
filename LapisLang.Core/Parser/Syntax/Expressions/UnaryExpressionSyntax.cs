using System.Linq.Expressions;

namespace LapisLang.Core;

public class UnaryExpressionSyntax : ExpressionSyntax
{
    public UnaryExpressionSyntax(
        SourceSpan sourceSpan,
        ExpressionSyntax expression,
        Token tokenOperator
    )
    {
        SourceSpan = sourceSpan;
        Expression = expression;
        TokenOperator = tokenOperator;
    }
    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax Expression { get; }
    public Token TokenOperator { get; }
}
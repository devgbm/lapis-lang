namespace LapisLang.Core;

public class ParenthesizedExpression : ExpressionSyntax
{
    public ParenthesizedExpression(
        SourceSpan sourceSpan,
        ExpressionSyntax expression
    )
    {
        SourceSpan = sourceSpan;
        Expression = expression;
    }
    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax Expression { get; }
}
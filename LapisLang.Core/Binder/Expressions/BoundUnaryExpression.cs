
namespace LapisLang.Core;

public class BoundUnaryExpression : BoundExpression
{
    public BoundUnaryExpression(
        SourceSpan span,
        BoundExpression expression,
        UnaryOperator unaryOperator,
        LapisType type
    )
    {
        Span = span;
        Expression = expression;
        UnaryOperator = unaryOperator;
        Type = type;
    }
    public override LapisType Type { get; }

    public override SourceSpan Span { get; }
    public BoundExpression Expression { get; }
    public UnaryOperator UnaryOperator { get; }
}
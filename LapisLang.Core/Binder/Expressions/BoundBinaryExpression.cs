
namespace LapisLang.Core;

public class BoundBinaryExpression : BoundExpression
{
    public BoundBinaryExpression(
        SourceSpan sourceSpan,
        BoundExpression left,
        BoundExpression right,
        BinaryOperator binaryOperator,
        TypeRune type
    )
    {
        Span = sourceSpan;
        Left = left;
        Right = right;
        BinaryOperator = binaryOperator;
        Type = type;
    }
    public override TypeRune Type { get; }
    public override SourceSpan Span { get; }
    public BoundExpression Left { get; }
    public BoundExpression Right { get; }
    public BinaryOperator BinaryOperator { get; }
}

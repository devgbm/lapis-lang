
namespace LapisLang.Core;
public abstract class BoundExpression : BoundSyntax
{
    public abstract LapisType Type { get; }
}

public enum BinaryOperator { Add, Sub, Mul, Div, Mod, Equality, Inequality, Less, Greather, LessEquals, GreatherEquals,
    LogicAnd,
    LogicOr,
    Unkown
}

public class BoundBinaryExpression : BoundExpression
{
    public BoundBinaryExpression(
        SourceSpan sourceSpan,
        BoundExpression left,
        BoundExpression right,
        BinaryOperator binaryOperator,
        LapisType type
    )
    {
        Span = sourceSpan;
        Left = left;
        Right = right;
        BinaryOperator = binaryOperator;
        Type = type;
    }
    public override LapisType Type { get; }
    public override SourceSpan Span { get; }
    public BoundExpression Left { get; }
    public BoundExpression Right { get; }
    public BinaryOperator BinaryOperator { get; }
}
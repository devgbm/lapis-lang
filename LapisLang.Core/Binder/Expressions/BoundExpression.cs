
namespace LapisLang.Core;
public abstract class BoundExpression : BoundSyntax
{
    public abstract LapisType Type { get; }
}


public class BoundLiteralExpression : BoundExpression
{
    public BoundLiteralExpression(
        SourceSpan span,
        LapisType type,
        object? value
    )
    {
        Span = span;
        Type = type;
        Value = value;
    }
    public override SourceSpan Span { get; }
    public override LapisType Type { get; }
    public object? Value { get; }
}


namespace LapisLang.Core;

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

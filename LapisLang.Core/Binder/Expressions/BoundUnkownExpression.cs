
namespace LapisLang.Core;

public class BoundUnkownExpression : BoundExpression
{
    public BoundUnkownExpression(SourceSpan span)
    {
        Span = span;
        Type = LangDefaults.Types.Unkown;
    }
    public override TypeRune Type { get; }

    public override SourceSpan Span { get; }
}
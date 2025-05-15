
namespace LapisLang.Core;

public class BoundNameExpression : BoundExpression
{
    public BoundNameExpression(
        SourceSpan sourceSpan,
        TypeRune type,
        string name
    )
    {
        Span = sourceSpan;
        Type = type;
        Name = name;
    }
    public override TypeRune Type { get; }
    public string Name { get; }
    public override SourceSpan Span { get; }

}
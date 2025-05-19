
namespace LapisLang.Core;

public class BoundNameExpression : BoundExpression
{
    public BoundNameExpression(
        SourceSpan sourceSpan,
        Rune rune,
        TypeRune type,
        string name
    )
    {
        Span = sourceSpan;
        Rune = rune;
        Type = type;
        Name = name;
    }
    public override TypeRune Type { get; }
    public string Name { get; }
    public override SourceSpan Span { get; }
    public Rune Rune { get; }
}

public class BoundMemberExpression : BoundExpression
{
    public BoundMemberExpression(
        SourceSpan span,
        TypeRune type,
        BoundExpression expression,
        Rune member
    )
    {
        Span = span;
        Type = type;
        Expression = expression;
        Member = member;
    }
    public override TypeRune Type { get; }
    public BoundExpression Expression { get; }
    public Rune Member { get; }
    public override SourceSpan Span { get; }
}
namespace LapisLang.Core;

public class BoundArgument : BoundSyntax
{
    public BoundArgument(SourceSpan span, string name, TypeRune type)
    {
        Span = span;
        Name = name;
        Type = type;
    }

    public override SourceSpan Span { get; }
    public string Name { get; }
    public TypeRune Type { get; }
}

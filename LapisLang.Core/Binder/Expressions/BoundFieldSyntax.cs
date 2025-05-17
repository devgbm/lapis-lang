namespace LapisLang.Core;

public class BoundFieldSyntax : BoundSyntax
{
    public BoundFieldSyntax(
        SourceSpan span,
        string name,
        TypeRune typeRune
    )
    {
        Span = span;
        Name = name;
        TypeRune = typeRune;
    }

    public override SourceSpan Span { get; }
    public string Name { get; }
    public TypeRune TypeRune { get; }
}
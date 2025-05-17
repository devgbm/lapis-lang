namespace LapisLang.Core;

public class BoundVariableDeclaration : BoundStatement
{
    public BoundVariableDeclaration(
        SourceSpan span,
        VariableRune rune
    )
    {
        Span = span;
        Rune = rune;
    }

    public override SourceSpan Span { get; }
    public VariableRune Rune { get; }
}
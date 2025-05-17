namespace LapisLang.Core;

public class BoundUnkownStatement : BoundStatement
{
    public BoundUnkownStatement(SourceSpan span)
    {
        Span = span;
    }

    public override SourceSpan Span { get; }
}

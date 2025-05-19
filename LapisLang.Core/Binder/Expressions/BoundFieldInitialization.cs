namespace LapisLang.Core;

public class BoundFieldInitialization : BoundSyntax
{
    public BoundFieldInitialization(
        SourceSpan span,
        string name,
        BoundExpression expression)
    {
        Span = span;
        Name = name;
        Expression = expression;
    }

    public override SourceSpan Span { get; }
    public string Name { get; }
    public BoundExpression Expression { get; }
}

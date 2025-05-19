namespace LapisLang.Core;

public class BoundReturnStatement : BoundStatement
{
    public BoundReturnStatement(SourceSpan span, BoundExpression expression)
    {
        Span = span;
        Expression = expression;
    }

    public override SourceSpan Span { get; }
    public BoundExpression Expression { get; }
}

namespace LapisLang.Core;

public abstract class BoundStatement : BoundSyntax;
public class BoundExpressionStatement : BoundStatement
{
    public BoundExpressionStatement(SourceSpan span, BoundExpression expression)
    {
        Span = span;
        Expression = expression;
    }

    public override SourceSpan Span { get; }
    public BoundExpression Expression { get; }
}
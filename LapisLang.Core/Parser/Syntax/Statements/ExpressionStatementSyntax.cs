namespace LapisLang.Core;

public class ExpressionStatementSyntax : StatementSyntax
{
    public ExpressionStatementSyntax(SourceSpan sourceSpan, ExpressionSyntax expression)
    {
        SourceSpan = sourceSpan;
        Expression = expression;
    }

    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax Expression { get; }
}

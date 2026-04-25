namespace LapisLang.Core;

public class ReturnStatementSyntax : StatementSyntax
{
    public ReturnStatementSyntax(SourceSpan sourceSpan, ExpressionSyntax? expression)
    {
        SourceSpan = sourceSpan;
        Expression = expression;
    }

    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax? Expression { get; }
}

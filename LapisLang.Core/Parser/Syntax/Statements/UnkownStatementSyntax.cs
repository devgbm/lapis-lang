namespace LapisLang.Core;

public class UnkownStatementSyntax : StatementSyntax
{
    public UnkownStatementSyntax(SourceSpan sourceSpan)
    {
        SourceSpan = sourceSpan;
    }

    public override SourceSpan SourceSpan { get; }
}

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
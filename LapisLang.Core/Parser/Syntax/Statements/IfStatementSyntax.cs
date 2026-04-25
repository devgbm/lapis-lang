namespace LapisLang.Core;

public class IfStatementSyntax : StatementSyntax
{
    public IfStatementSyntax(
        SourceSpan sourceSpan,
        ExpressionSyntax conditional,
        StatementSyntax statement,
        StatementSyntax? elseStatement)
    {
        Conditional = conditional;
        Statement = statement;
        ElseStatement = elseStatement;
        SourceSpan = sourceSpan;
    }

    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax Conditional { get; }
    public StatementSyntax Statement { get; }
    public StatementSyntax? ElseStatement { get; }
}

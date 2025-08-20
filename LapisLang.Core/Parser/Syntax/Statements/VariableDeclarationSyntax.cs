namespace LapisLang.Core;
public class DefineStatementSyntax : StatementSyntax
{
    public DefineStatementSyntax(
        SourceSpan sourceSpan,
        Token identifier,
        ExpressionSyntax expression
    )
    {
        SourceSpan = sourceSpan;
        Identifier = identifier;
        Expression = expression;
    }
    public override SourceSpan SourceSpan { get; }
    public Token Identifier { get; }
    public ExpressionSyntax Expression { get; }
}
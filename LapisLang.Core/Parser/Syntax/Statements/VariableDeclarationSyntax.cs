namespace LapisLang.Core;
public class VariableDeclarationSyntax : StatementSyntax
{
    public VariableDeclarationSyntax(
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
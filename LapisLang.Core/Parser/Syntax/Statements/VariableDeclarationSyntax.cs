namespace LapisLang.Core;
public class VariableDeclarationSyntax : StatementSyntax
{
    public VariableDeclarationSyntax(
        SourceSpan sourceSpan,
        Token identifier,
        TypeNameSyntax typeName,
        ExpressionSyntax expression
    )
    {
        SourceSpan = sourceSpan;
        Identifier = identifier;
        TypeName = typeName;
        Expression = expression;
    }
    public override SourceSpan SourceSpan { get; }
    public Token Identifier { get; }
    public TypeNameSyntax TypeName { get; }
    public ExpressionSyntax Expression { get; }
}
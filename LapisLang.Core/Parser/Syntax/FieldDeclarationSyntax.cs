namespace LapisLang.Core;

public class FieldDeclarationSyntax : Syntax
{
    public FieldDeclarationSyntax(
        SourceSpan sourceSpan,
        Token identifier,
        ExpressionSyntax typeName

    )
    {
        SourceSpan = sourceSpan;
        Identifier = identifier;
        Expression = typeName;
    }

    public override SourceSpan SourceSpan { get; }
    public Token Identifier { get; }
    public ExpressionSyntax Expression { get; }
}
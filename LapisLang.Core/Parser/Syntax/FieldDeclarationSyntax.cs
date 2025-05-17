namespace LapisLang.Core;

public class FieldDeclarationSyntax : Syntax
{
    public FieldDeclarationSyntax(
        SourceSpan sourceSpan,
        Token identifier,
        TypeNameSyntax typeName

    )
    {
        SourceSpan = sourceSpan;
        Identifier = identifier;
        TypeName = typeName;
    }

    public override SourceSpan SourceSpan { get; }
    public Token Identifier { get; }
    public TypeNameSyntax TypeName { get; }
}
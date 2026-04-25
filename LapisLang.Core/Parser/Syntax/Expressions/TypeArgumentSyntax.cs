namespace LapisLang.Core;

public class TypeArgumentSyntax : Syntax
{
    public TypeArgumentSyntax(
        TypeNameSyntax typeName,
        Token identifier,
        SourceSpan sourceSpan
    )
    {
        TypeName = typeName;
        Identifier = identifier;
        SourceSpan = sourceSpan;
    }

    public TypeNameSyntax TypeName { get; }
    public Token Identifier { get; }
    public override SourceSpan SourceSpan { get; }
}

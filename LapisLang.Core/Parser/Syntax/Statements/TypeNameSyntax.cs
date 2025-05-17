namespace LapisLang.Core;

public class TypeNameSyntax : Syntax
{
    public TypeNameSyntax(SourceSpan sourceSpan, Token identifier)
    {
        SourceSpan = sourceSpan;
        Identifier = identifier;
    }
    public override SourceSpan SourceSpan { get; }
    public Token Identifier { get; }
}

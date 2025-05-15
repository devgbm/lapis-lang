using System.Collections.Immutable;

namespace LapisLang.Core;

public abstract class Syntax
{
    public abstract SourceSpan SourceSpan { get; }
}


public class DeclarationTypeNameSyntax : Syntax
{
    public DeclarationTypeNameSyntax(SourceSpan sourceSpan, Token identifier)
    {
        SourceSpan = sourceSpan;
        Identifier = identifier;
    }
    public override SourceSpan SourceSpan { get; }
    public Token Identifier { get; }
    public string GetTypeName()
    {
        return Identifier.String;
    }
}


public class NamespaceNameSyntax : Syntax
{
    public NamespaceNameSyntax(
        SourceSpan sourceSpan,
        ImmutableArray<Token> identifiers)
    {
        SourceSpan = sourceSpan;
        Identifiers = identifiers;
    }
    public override SourceSpan SourceSpan { get; }
    public ImmutableArray<Token> Identifiers { get; }
}
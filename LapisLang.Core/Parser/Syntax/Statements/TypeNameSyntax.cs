using System.Collections.Immutable;

namespace LapisLang.Core;

public class TypeNameSyntax : Syntax
{
    public TypeNameSyntax(SourceSpan sourceSpan, Token identifier) : this(sourceSpan, identifier, [])
    {
    }
    public TypeNameSyntax(SourceSpan sourceSpan, Token identifier, ImmutableArray<TypeNameSyntax> typeArguments)
    {
        SourceSpan = sourceSpan;
        Identifier = identifier;
        TypeArguments = typeArguments;
    }
    public override SourceSpan SourceSpan { get; }
    public Token Identifier { get; }
    public ImmutableArray<TypeNameSyntax> TypeArguments { get; }
}

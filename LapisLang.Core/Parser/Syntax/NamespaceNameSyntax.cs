using System.Collections.Immutable;

namespace LapisLang.Core;

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
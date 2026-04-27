using System.Collections.Immutable;

namespace LapisLang.Core;

public class EnumVariantSyntax : Syntax
{
    public EnumVariantSyntax(SourceSpan sourceSpan, Token name, ImmutableArray<FieldDeclarationSyntax> fields)
    {
        SourceSpan = sourceSpan;
        Name = name;
        Fields = fields;
    }

    public override SourceSpan SourceSpan { get; }
    public Token Name { get; }
    public ImmutableArray<FieldDeclarationSyntax> Fields { get; }
}

using System.Collections.Immutable;

namespace LapisLang.Core;

public class TypeExpressionSyntax : ExpressionSyntax
{
    public TypeExpressionSyntax(
        SourceSpan sourceSpan,
        ImmutableArray<FieldDeclarationSyntax> fields
    )
    {
        SourceSpan = sourceSpan;
        Fields = fields;
    }

    public override SourceSpan SourceSpan { get; }
    public ImmutableArray<FieldDeclarationSyntax> Fields { get; }
}
using System.Collections.Immutable;

namespace LapisLang.Core;

public class TypeExpressionSyntax : ExpressionSyntax
{
    public TypeExpressionSyntax(
        SourceSpan sourceSpan,
        ImmutableArray<FieldDeclarationSyntax> fields,
        ImmutableArray<TypeArgumentSyntax> arguments
    )
    {
        SourceSpan = sourceSpan;
        Fields = fields;
        Arguments = arguments;
    }

    public override SourceSpan SourceSpan { get; }
    public ImmutableArray<FieldDeclarationSyntax> Fields { get; }
    public ImmutableArray<TypeArgumentSyntax> Arguments { get; }
}

using System.Collections.Immutable;

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
using System.Collections.Immutable;

namespace LapisLang.Core;

public class InstanceInitializationExpression : ExpressionSyntax
{
    public InstanceInitializationExpression(
        SourceSpan sourceSpan,
        TypeNameSyntax typeName,
        ImmutableArray<FieldInitilizationSyntax> initializers
    )
    {
        SourceSpan = sourceSpan;
        TypeName = typeName;
        Initializers = initializers;
    }

    public override SourceSpan SourceSpan { get; }
    public TypeNameSyntax TypeName { get; }
    public ImmutableArray<FieldInitilizationSyntax> Initializers { get; }
}
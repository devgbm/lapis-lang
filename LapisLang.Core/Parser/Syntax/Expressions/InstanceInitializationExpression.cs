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
        IsAnonimous = false;
        Initializers = initializers;
    }

    public InstanceInitializationExpression(
        SourceSpan sourceSpan,
        ImmutableArray<FieldInitilizationSyntax> initializers
    )
    {
        SourceSpan = sourceSpan;
        IsAnonimous = true;
        Initializers = initializers;
    }
    public override SourceSpan SourceSpan { get; }
    public bool IsAnonimous { get; }
    public TypeNameSyntax? TypeName { get; }
    public ImmutableArray<FieldInitilizationSyntax> Initializers { get; }
}
using System.Collections.Immutable;

namespace LapisLang.Core;

public class BoundInstanceInitializationExpression : BoundExpression
{
    public BoundInstanceInitializationExpression(
        SourceSpan span,
        TypeRune type,
        ImmutableArray<BoundFieldInitialization> initiaizations
    )
    {
        Span = span;
        Type = type;
        Initiaizations = initiaizations;
    }

    public override TypeRune Type { get; }
    public ImmutableArray<BoundFieldInitialization> Initiaizations { get; }
    public override SourceSpan Span { get; }
}
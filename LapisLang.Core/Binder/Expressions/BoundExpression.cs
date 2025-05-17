using System.Collections.Immutable;

namespace LapisLang.Core;
public abstract class BoundExpression : BoundSyntax
{
    public abstract TypeRune Type { get; }
}

public class BoundFieldInitialization : BoundSyntax
{
    public BoundFieldInitialization(
        SourceSpan span,
        string name,
        BoundExpression expression)
    {
        Span = span;
        Name = name;
        Expression = expression;
    }

    public override SourceSpan Span { get; }
    public string Name { get; }
    public BoundExpression Expression { get; }
}
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
    }

    public override TypeRune Type { get; }

    public override SourceSpan Span { get; }
}
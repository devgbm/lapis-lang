using System.Collections.Immutable;

namespace LapisLang.Core;

public class BoundScopeStatement : BoundStatement
{
    public BoundScopeStatement(
        SourceSpan span,
        ImmutableArray<BoundStatement> statements)
    {
        Span = span;
        Statements = statements;
    }

    public override SourceSpan Span { get; }
    public ImmutableArray<BoundStatement> Statements { get; }
}

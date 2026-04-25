using System.Collections.Immutable;

namespace LapisLang.Core;

public class ScopeStatementSyntax : StatementSyntax
{
    public ScopeStatementSyntax(SourceSpan sourceSpan, ImmutableArray<StatementSyntax> statements)
    {
        SourceSpan = sourceSpan;
        Statements = statements;
    }

    public override SourceSpan SourceSpan { get; }
    public ImmutableArray<StatementSyntax> Statements { get; }
}

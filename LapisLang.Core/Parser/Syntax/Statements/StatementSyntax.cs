using System.Collections.Immutable;
using LapisLang.Core;

namespace LapisLang.Core;

public abstract class StatementSyntax : Syntax;


public class ReturnStatementSyntax : StatementSyntax
{
    public ReturnStatementSyntax(SourceSpan sourceSpan, ExpressionSyntax? expression)
    {
        SourceSpan = sourceSpan;
        Expression = expression;
    }

    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax? Expression { get; }
}

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
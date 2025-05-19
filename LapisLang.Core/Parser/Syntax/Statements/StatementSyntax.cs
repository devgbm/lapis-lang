using System.Collections.Immutable;
using LapisLang.Core;

namespace LapisLang.Core;

public abstract class StatementSyntax : Syntax;


public class ReturnStatement : StatementSyntax
{
    public ReturnStatement(SourceSpan sourceSpan, ExpressionSyntax expression)
    {
        SourceSpan = sourceSpan;
        Expression = expression;
    }

    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax Expression { get; }
}

public class ScopeStatement : StatementSyntax
{
    public ScopeStatement(SourceSpan sourceSpan, ImmutableArray<StatementSyntax> statements)
    {
        SourceSpan = sourceSpan;
        Statements = statements;
    }

    public override SourceSpan SourceSpan { get; }
    public ImmutableArray<StatementSyntax> Statements { get; }
}




using System.Collections.Immutable;

namespace LapisLang.Core;

public abstract class StatementSymbol : Symbol
{
    public static StatementSymbol Unknown = new UnkownStatementSymbol();
}

public class ReturnStatementSymbol : StatementSymbol
{
    public ReturnStatementSymbol(ExpressionSymbol expression)
    {
        Expression = expression;
    }

    public ExpressionSymbol Expression { get; }
}
public class ScopeStatementSymbol : StatementSymbol
{
    public ScopeStatementSymbol(
        ImmutableArray<StatementSymbol> statements,
        TypeSymbol? returnType
    )
    {
        Statements = statements;
        ReturnType = returnType;
    }

    public ImmutableArray<StatementSymbol> Statements { get; }
    public TypeSymbol? ReturnType { get; }
}
public class VariableDeclarationSymbol : StatementSymbol
{
    public VariableDeclarationSymbol(
        string name,
        ExpressionSymbol symbol,
        TypeSymbol type
    )
    {
        Name = name;
        Symbol = symbol;
        Type = type;
    }

    public string Name { get; }
    public ExpressionSymbol Symbol { get; }
    public TypeSymbol Type { get; }
}
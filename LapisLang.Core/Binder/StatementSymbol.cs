

using System.Collections.Immutable;

namespace LapisLang.Core;

public abstract class StatementSymbol : Symbol
{
    public static StatementSymbol UnkownStatement = new UnkownStatement();
}

public class UnkownStatement : StatementSymbol;

public class VoidStatement : StatementSymbol
{
    public static VoidStatement Instance = new();
    private VoidStatement(){}
}

public class IfSymbol : StatementSymbol
{
    public IfSymbol(ExprSymbol expression, StatementSymbol statements, StatementSymbol? elseStatements)
    {
        ConditionExpression = expression;
        Statements = statements;
        ElseStatements = elseStatements;
    }

    public ExprSymbol ConditionExpression { get; }
    public StatementSymbol Statements { get; }
    public StatementSymbol? ElseStatements { get; }
}
public class ReturnSymbol : StatementSymbol
{
    public ReturnSymbol(
        ExprSymbol expression
    )
    {
        Expression = expression;
    }

    public ExprSymbol Expression { get; }
}
public class DefineSymbol : StatementSymbol
{
    public DefineSymbol(string name, ExprSymbol exprSymbol)
    {
        Name = name;
        Expression = exprSymbol;
    }

    public string Name { get; }
    public ExprSymbol Expression { get; }
}

public class ScopeSymbol : StatementSymbol
{
    public ScopeSymbol(ImmutableArray<StatementSymbol> statementSymbols, TypeSymbol returnType)
    {
        StatementSymbols = statementSymbols;
        ReturnType = returnType;
    }

    public ImmutableArray<StatementSymbol> StatementSymbols { get; }
    public TypeSymbol ReturnType { get; }
}
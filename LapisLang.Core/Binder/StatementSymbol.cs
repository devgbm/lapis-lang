

using System.Collections.Immutable;

namespace LapisLang.Core;

public abstract class StatementSymbol : Symbol
{
    public static StatementSymbol UnkownStatement = new UnkownStatement();
}
public class UnkownStatement : StatementSymbol;


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
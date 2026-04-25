using System.Collections.Immutable;

namespace LapisLang.Core;

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

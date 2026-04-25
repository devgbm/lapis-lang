using System.Collections.Immutable;

namespace LapisLang.Core;

public class FuncSymbol : ExprSymbol
{
    public FuncSymbol(
        StatementSymbol statement,
        ImmutableArray<ParameterSymbol> parameters,
        ExprSymbol ReturnType,
        FuncTypeSymbol type,
        bool isComptimeFn,
        bool isInstanceMethod = false,
        BindFlag flags = BindFlag.None
    )
    {
        Statement = statement;
        Parameters = parameters;
        this.ReturnType = ReturnType;
        IsComptimeFn = isComptimeFn;
        IsInstanceMethod = isInstanceMethod;
        Type = type;
        Flags = flags;
    }

    public override TypeSymbol Type { get; }

    public StatementSymbol Statement { get; }
    public ImmutableArray<ParameterSymbol> Parameters { get; }
    public ExprSymbol ReturnType { get; }
    public bool IsComptimeFn { get; }
    public bool IsInstanceMethod { get; }
}

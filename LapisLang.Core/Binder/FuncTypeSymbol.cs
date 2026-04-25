using System.Collections.Immutable;

namespace LapisLang.Core;

public class FuncTypeSymbol : TypeSymbol
{
    public FuncTypeSymbol(
        ImmutableArray<ParameterSymbol> arguments,
        TypeSymbol returnType,
        BindFlag bindFlags = BindFlag.None) : base($"({string.Join(", ", arguments.Select(e => e.Expression))}):{returnType}")
    {
        Flags = bindFlags;
        Parameters = arguments;
        ReturnType = returnType;
    }

    public ImmutableArray<ParameterSymbol> Parameters { get; }
    public TypeSymbol ReturnType { get; }
    public override TypeSymbol Type => LangDefaults.Types.Function;

    public override bool IsEquivalent(ExprSymbol symbol)
    {
        return symbol is FuncTypeSymbol s;
    }
}

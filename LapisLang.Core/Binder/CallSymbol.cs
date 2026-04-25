using System.Collections.Immutable;

namespace LapisLang.Core;

public class CallSymbol : ExprSymbol
{
    public CallSymbol(
        ExprSymbol callableExpression,
        ImmutableArray<ArgumentSymbol> arguments,
        TypeSymbol returnType,
        BindFlag flags = BindFlag.None
    )
    {
        CallableExpression = callableExpression;
        Arguments = arguments;
        Type = returnType;
        Flags = flags;
    }

    public override TypeSymbol Type { get; }

    public ExprSymbol CallableExpression { get; }
    public ImmutableArray<ArgumentSymbol> Arguments { get; }
}

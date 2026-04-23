

using System.Collections.Immutable;

namespace LapisLang.Core;

public class InstanceSymbol : ExprSymbol
{
    public InstanceSymbol(
        ImmutableDictionary<string, ExprSymbol> atributes,
        TypeSymbol type
    )
    {
        Atributes = atributes;
        Type = type;
    }
    public override TypeSymbol Type { get; }
    public ImmutableDictionary<string, ExprSymbol> Atributes { get; }
}

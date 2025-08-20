

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
        IsConstant = atributes.Values.All(e => e.IsConstant);
        Type = type;

    }
    public override bool IsConstant { get; }
    public override TypeSymbol Type { get; }
    public ImmutableDictionary<string, ExprSymbol> Atributes { get; }
} 

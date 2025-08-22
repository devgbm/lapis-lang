

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
        IsCompileTime = atributes.Values.All(e => e.IsCompileTime);
        Type = type;

    }
    public override bool IsCompileTime { get; }
    public override TypeSymbol Type { get; }
    public ImmutableDictionary<string, ExprSymbol> Atributes { get; }
} 

using System.Collections.Immutable;

namespace LapisLang.Core;

public class EnumTypeSymbol : TypeSymbol
{
    public EnumTypeSymbol(ImmutableArray<EnumVariantInfo> variants, string? debugName) : base(debugName)
    {
        Variants = variants;
    }

    public ImmutableArray<EnumVariantInfo> Variants { get; }

    public override bool IsEquivalent(ExprSymbol symbol) => ReferenceEquals(this, symbol);
}

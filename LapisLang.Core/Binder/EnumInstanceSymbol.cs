using System.Collections.Immutable;

namespace LapisLang.Core;

public class EnumInstanceSymbol : ExprSymbol
{
    public EnumInstanceSymbol(
        EnumTypeSymbol enumType,
        string variantName,
        ImmutableDictionary<string, ExprSymbol> fields)
    {
        EnumType = enumType;
        VariantName = variantName;
        Fields = fields;
    }

    public EnumTypeSymbol EnumType { get; }
    public string VariantName { get; }
    public ImmutableDictionary<string, ExprSymbol> Fields { get; }
    public override TypeSymbol Type => EnumType;
}

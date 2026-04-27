namespace LapisLang.Core;

public class EnumVariantConstructorSymbol : ExprSymbol
{
    public EnumVariantConstructorSymbol(EnumTypeSymbol enumType, EnumVariantInfo variant, FuncTypeSymbol funcType)
    {
        EnumType = enumType;
        Variant = variant;
        _type = funcType;
    }

    public EnumTypeSymbol EnumType { get; }
    public EnumVariantInfo Variant { get; }
    private readonly FuncTypeSymbol _type;
    public override TypeSymbol Type => _type;
}

namespace LapisLang.Core;

public class DecimalSymbol : ExprSymbol
{
    public DecimalSymbol(decimal value, BindFlag flags = BindFlag.None)
    {
        Value = value;
        Flags = flags;
    }
    public decimal Value { get; }
    public override TypeSymbol Type => LangDefaults.Types.Decimal;
}

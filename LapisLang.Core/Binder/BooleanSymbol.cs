namespace LapisLang.Core;

public class BooleanSymbol : ExprSymbol
{
    public BooleanSymbol(bool value, BindFlag flags = BindFlag.None)
    {
        Value = value;
        Flags = flags;
    }
    public bool Value { get; }
    public override TypeSymbol Type => LangDefaults.Types.Boolean;
}

namespace LapisLang.Core;

public class StringSymbol : ExprSymbol
{
    public StringSymbol(string value, BindFlag flags = BindFlag.None)
    {
        Value = value;
        Flags = flags;
    }
    public string Value { get; }
    public override TypeSymbol Type => LangDefaults.Types.String;
}

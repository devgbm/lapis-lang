namespace LapisLang.Core;

public class IntegerSymbol : ExprSymbol
{
    public IntegerSymbol(long value, BindFlag flags = BindFlag.None)
    {
        Value = value;
        Flags = flags;
    }
    public long Value { get; }
    public override TypeSymbol Type => LangDefaults.Types.Integer;
}

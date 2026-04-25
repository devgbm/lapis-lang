namespace LapisLang.Core;

public class VoidSymbol : ExprSymbol
{
    public static VoidSymbol Instance = new();
    private VoidSymbol() { }
    public override TypeSymbol Type => LangDefaults.Types.Void;
}

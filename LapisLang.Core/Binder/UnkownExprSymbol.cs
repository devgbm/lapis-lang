namespace LapisLang.Core;

public class UnkownExprSymbol : ExprSymbol
{
    public override TypeSymbol Type => LangDefaults.Types.Unkown;
}

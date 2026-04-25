namespace LapisLang.Core;

public abstract class ExprSymbol : Symbol
{
    public static ExprSymbol Unkown = new UnkownExprSymbol();
    public abstract TypeSymbol Type { get; }
    public BindFlag Flags { get; protected set; }
}





namespace LapisLang.Core;

public abstract class ExpressionSymbol : Symbol
{
    public static ExpressionSymbol Unknown = new UnkwonExpressionSymbol();
    public ExpressionSymbol(TypeSymbol type)
    {
        Type = type;
    }

    public TypeSymbol Type { get; }
}

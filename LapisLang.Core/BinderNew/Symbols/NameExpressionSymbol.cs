



namespace LapisLang.Core;

public class NameExpressionSymbol : ExpressionSymbol
{
    public NameExpressionSymbol(
        Symbol symbol,
        TypeSymbol type
    ) : base(type)
    {
        Symbol = symbol;
    }

    public Symbol Symbol { get; }
}

namespace LapisLang.Core;

public class ReturnSymbol : StatementSymbol
{
    public ReturnSymbol(ExprSymbol expression)
    {
        Expression = expression;
    }

    public ExprSymbol Expression { get; }
}

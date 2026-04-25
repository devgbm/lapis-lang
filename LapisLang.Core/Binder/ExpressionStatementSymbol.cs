namespace LapisLang.Core;

public class ExpressionStatementSymbol : StatementSymbol
{
    public ExpressionStatementSymbol(ExprSymbol expression)
    {
        Expression = expression;
    }

    public ExprSymbol Expression { get; }
}

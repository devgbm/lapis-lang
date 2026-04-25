namespace LapisLang.Core;

public class AssingSymbol : StatementSymbol
{
    public AssingSymbol(string name, ExprSymbol expression)
    {
        Name = name;
        Expression = expression;
    }

    public string Name { get; }
    public ExprSymbol Expression { get; }
}

namespace LapisLang.Core;

public class VarSymbol : StatementSymbol
{
    public VarSymbol(string name, ExprSymbol expression)
    {
        Name = name;
        Expression = expression;
    }

    public string Name { get; }
    public ExprSymbol Expression { get; }
}

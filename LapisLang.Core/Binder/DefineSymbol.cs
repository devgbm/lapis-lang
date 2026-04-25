namespace LapisLang.Core;

public class DefineSymbol : StatementSymbol
{
    public DefineSymbol(string name, ExprSymbol exprSymbol)
    {
        Name = name;
        Expression = exprSymbol;
    }

    public string Name { get; }
    public ExprSymbol Expression { get; }
}

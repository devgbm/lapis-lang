namespace LapisLang.Core;

public class ArgumentSymbol : Symbol
{
    public ArgumentSymbol(
        string name,
        ExprSymbol expression
    )
    {
        Name = name;
        Expression = expression;
    }

    public string Name { get; }
    public ExprSymbol Expression { get; }
}

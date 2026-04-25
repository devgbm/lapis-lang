namespace LapisLang.Core;

public class ParameterSymbol : Symbol
{
    public ParameterSymbol(
        string name,
        ExprSymbol expression,
        bool isComptime
    )
    {
        Name = name;
        Expression = expression;
        IsComptime = isComptime;
    }

    public string Name { get; }
    public ExprSymbol Expression { get; }
    public bool IsComptime { get; }
}

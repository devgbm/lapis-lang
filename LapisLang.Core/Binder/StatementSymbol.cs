

namespace LapisLang.Core;

public abstract class StatementSymbol : Symbol
{
    public static StatementSymbol UnkownStatement = new UnkownStatement();
}
public class UnkownStatement : StatementSymbol;

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
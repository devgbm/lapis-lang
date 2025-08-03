



namespace LapisLang.Core;

public abstract class StatementSymbol : Symbol
{
    public static StatementSymbol Unknown = new UnkownStatementSymbol();
}

public class VariableDeclarationSymbol : StatementSymbol
{
    public VariableDeclarationSymbol(
        string name,
        ExpressionSymbol symbol,
        TypeSymbol type
    )
    {
        Name = name;
        Symbol = symbol;
        Type = type;
    }

    public string Name { get; }
    public ExpressionSymbol Symbol { get; }
    public TypeSymbol Type { get; }
}
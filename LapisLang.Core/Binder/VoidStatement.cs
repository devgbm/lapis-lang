namespace LapisLang.Core;

public class VoidStatement : StatementSymbol
{
    public static VoidStatement Instance = new();
    private VoidStatement() { }
}

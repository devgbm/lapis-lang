



namespace LapisLang.Core;


public class BinaryOperatorSymbol : Symbol
{
    public BinaryOperatorSymbol(BinaryOperatorKind kind, TypeSymbol with, TypeSymbol result)
    {
        Kind = kind;
        With = with;
        Result = result;
    }
    public BinaryOperatorKind Kind { get; set; }
    public TypeSymbol With { get; set; }
    public TypeSymbol Result { get; set; }

    public static BinaryOperatorSymbol Unknown = new BinaryOperatorSymbol(BinaryOperatorKind.Unknown ,DefaultSymbols.Types.Unkown, DefaultSymbols.Types.Unkown);

}

public class TypeSymbol : ScopeSymbol
{
    public TypeSymbol(string typeName)
    {
        TypeName = typeName;
    }
    public string TypeName { get; }

    internal BinaryOperatorSymbol GetOperatorFor(TokenKind kind)
    {
        if (!GetSymbol(kind, out var symbol))
        {
            return BinaryOperatorSymbol.Unknown;
        }
        return symbol as BinaryOperatorSymbol ?? BinaryOperatorSymbol.Unknown;
    }
}

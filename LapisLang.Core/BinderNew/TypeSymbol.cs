




namespace LapisLang.Core;


public enum UnaryOperatorKind { Unknown = -1, Identity, Inverse, Negation };
public class UnaryOperatorSymbol : Symbol
{
    public UnaryOperatorSymbol(UnaryOperatorKind kind, TypeSymbol result)
    {
        Kind = kind;
        Result = result;
    }
    public UnaryOperatorKind Kind { get; set; }
    public TypeSymbol Result { get; set; }

    public static UnaryOperatorSymbol Unknown = new UnaryOperatorSymbol(UnaryOperatorKind.Unknown, DefaultSymbols.Types.Unkown);

}
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

    internal BinaryOperatorSymbol GetBinaryOperatorFor(TokenKind kind)
    {
        if (!GetSymbol(kind, out var symbol))
        {
            return BinaryOperatorSymbol.Unknown;
        }
        return symbol as BinaryOperatorSymbol ?? BinaryOperatorSymbol.Unknown;
    }

    internal UnaryOperatorSymbol GetUnaryOperatorFor(TokenKind kind)
    {
        if (!GetSymbol(kind, out var symbol))
        {
            return UnaryOperatorSymbol.Unknown;
        }
        return symbol as UnaryOperatorSymbol ?? UnaryOperatorSymbol.Unknown;
    }
}

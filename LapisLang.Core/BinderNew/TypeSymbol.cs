




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
    private static Dictionary<TokenKind, string> _uops = new()
    {
        [TokenKind.Minus] = "@uop_-",
        [TokenKind.Plus] = "@uop_+",
        [TokenKind.Bang] = "@uop_!",
        [TokenKind.NotKeyword] = "@uop_not"
    };

    private static Dictionary<TokenKind, string> _bops = new()
    {
        [TokenKind.Plus] = "@bop_+",
        [TokenKind.Minus] = "@bop_-",
        [TokenKind.Slash] = "@bop_/",
        [TokenKind.Star] = "@bop_*",
        [TokenKind.Percent] = "@bop_%",
        [TokenKind.Minus] = "@bop_-",
        [TokenKind.Minus] = "@bop_-",

        [TokenKind.DoubleEquals] = "@bop_eq",
        [TokenKind.BangEquals] = "@bop_neq",
        [TokenKind.LeftArrow] = "@bop_lt",
        [TokenKind.LeftArrowEquals] = "@bop_lteq",
        [TokenKind.RightArrow] = "@bop_gt",
        [TokenKind.RightArrowEquals] = "@bop_gteq",

        [TokenKind.AndKeyword] = "@bop_and",
        [TokenKind.OrKeyword] = "@bop_or",
    };

    public TypeSymbol(string typeName)
    {
        TypeName = typeName;
    }
    public string TypeName { get; }

    internal BinaryOperatorSymbol GetBinaryOperatorFor(TokenKind kind)
    {
        var mapped = _bops[kind];
        if (!GetSymbol(mapped, out var symbol))
        {
            return BinaryOperatorSymbol.Unknown;
        }
        return symbol as BinaryOperatorSymbol ?? BinaryOperatorSymbol.Unknown;
    }

    internal UnaryOperatorSymbol GetUnaryOperatorFor(TokenKind kind)
    {
        var mapped = _uops[kind];
        if (!GetSymbol(mapped, out var symbol))
        {
            return UnaryOperatorSymbol.Unknown;
        }
        return symbol as UnaryOperatorSymbol ?? UnaryOperatorSymbol.Unknown;
    }
}

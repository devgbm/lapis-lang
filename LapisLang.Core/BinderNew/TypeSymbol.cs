





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
public class FieldSymbol : Symbol
{
    public FieldSymbol(TypeSymbol owner, string name, TypeSymbol type)
    {
        Owner = owner;
        Name = name;
        Type = type;
    }

    public TypeSymbol Owner { get; }
    public string Name { get; }
    public TypeSymbol Type { get; }
}
public class TypeSymbol : ExpressionSymbol, IScopeSymbol
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

    private ScopeSymbol _scope = new ScopeSymbol();

    public TypeSymbol(string typeName) : base(DefaultSymbols.Types.Type)
    {
        TypeName = typeName;
    }
    public string TypeName { get; set; }

    public bool DefineSymbol(object? key, Symbol value)
    {
        return _scope.DefineSymbol(key, value);
    }

    public bool GetSymbol(object? key, out Symbol? symbol)
    {
        return _scope.GetSymbol(key, out symbol);
    }

    public IEnumerable<Symbol> GetSymbols()
    {
        return _scope.GetSymbols();
    }

    public void SetSymbol(object? key, Symbol value)
    {
        _scope.SetSymbol(key, value);
    }

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

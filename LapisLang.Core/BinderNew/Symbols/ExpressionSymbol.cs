



namespace LapisLang.Core;

public abstract class ExpressionSymbol : Symbol
{
    public static ExpressionSymbol Unknown = new UnkwonExpressionSymbol();
    public ExpressionSymbol(TypeSymbol type)
    {
        Type = type;
    }

    public TypeSymbol Type { get; }
}


public class MemberExpressionSymbol : ExpressionSymbol
{
    public MemberExpressionSymbol(
        ExpressionSymbol expression,
        FieldSymbol fieldSymbol) : base(fieldSymbol.Type is FieldSymbolType fst ? fst.TypeSymbol : (fieldSymbol.Type as FieldSymbolArgument).TypeSymbolArgument.Type)
    {
        Expression = expression;
        FieldSymbol = fieldSymbol;
    }

    public ExpressionSymbol Expression { get; }
    public FieldSymbol FieldSymbol { get; }
}

public class InstanceInitializeSymbol : ExpressionSymbol
{
    public InstanceInitializeSymbol(
        TypeSymbol type,
        Dictionary<FieldSymbol, ExpressionSymbol> initializers
    ) : base(type)
    {
        Initializers = initializers;
    }

    public Dictionary<FieldSymbol, ExpressionSymbol> Initializers { get; }
}




using System.Collections.Immutable;

namespace LapisLang.Core;

public class VoidExpresisonSymbol : ExpressionSymbol
{
    public VoidExpresisonSymbol() : base(DefaultSymbols.Types.Void)
    {
    }
}
public abstract class ExpressionSymbol : Symbol
{
    public static ExpressionSymbol Unknown = new UnkwonExpressionSymbol();
    public static ExpressionSymbol Void = new VoidExpresisonSymbol();

    public ExpressionSymbol(TypeSymbol type)
    {
        Type = type;
    }

    public TypeSymbol Type { get; }
}
public record CallArgument(string Name, ExpressionSymbol Expression);
public class CallSymbol : ExpressionSymbol
{
    public CallSymbol(
        FuncSymbol function,
        ImmutableArray<CallArgument> arguments 
    ) : base(function.ReturnType)
    {
        Function = function;
        Arguments = arguments;
    }

    public FuncSymbol Function { get; }
    public ImmutableArray<CallArgument> Arguments { get; }
}
public class MemberExpressionSymbol : ExpressionSymbol
{
    public MemberExpressionSymbol(
        ExpressionSymbol expression,
        FieldSymbol fieldSymbol) : base(fieldSymbol.Expression is TypeSymbol ts? ts : fieldSymbol.Expression.Type)
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
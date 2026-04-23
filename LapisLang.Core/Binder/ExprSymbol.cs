

using System.Collections.Immutable;

namespace LapisLang.Core;

public abstract class ExprSymbol : Symbol
{
    public static ExprSymbol Unkown = new UnkownExprSymbol();
    public abstract TypeSymbol Type { get; }
}
public class CallSymbol : ExprSymbol
{
    public CallSymbol(
        ExprSymbol callableExpression,
        ImmutableArray<ArgumentSymbol> arguments,
        TypeSymbol returnType
    )
    {
        CallableExpression = callableExpression;
        Arguments = arguments;
        Type = returnType;
    }

    public override TypeSymbol Type { get; }

    public ExprSymbol CallableExpression { get; }
    public ImmutableArray<ArgumentSymbol> Arguments { get; }
}
public class ArgumentSymbol : Symbol
{
    public ArgumentSymbol(
        string name,
        ExprSymbol expression
    )
    {
        Name = name;
        Expression = expression;
    }

    public string Name { get; }
    public ExprSymbol Expression { get; }
}
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


public class NativeFuncSymbol : ExprSymbol
{
    public NativeFuncSymbol(
        FuncTypeSymbol type,
        Delegate @delegate,
        bool isInstanceMethod = false
    )
    {
        FuncType = type;
        Delegate = @delegate;
        IsInstanceMethod = isInstanceMethod;
    }

    public override TypeSymbol Type { get => FuncType; }
    public FuncTypeSymbol FuncType { get; }
    public Delegate Delegate { get; }
    public bool IsInstanceMethod { get; }
}
public class FuncSymbol : ExprSymbol
{
    public FuncSymbol(
        StatementSymbol statement,
        ImmutableArray<ParameterSymbol> parameters,
        ExprSymbol ReturnType,
        FuncTypeSymbol type,
        bool isComptimeFn,
        bool isInstanceMethod = false
    )
    {
        Statement = statement;
        Parameters = parameters;
        this.ReturnType = ReturnType;
        IsComptimeFn = isComptimeFn;
        IsInstanceMethod = isInstanceMethod;
        Type = type;
    }

    public override TypeSymbol Type { get; }

    public StatementSymbol Statement { get; }
    public ImmutableArray<ParameterSymbol> Parameters { get; }
    public ExprSymbol ReturnType { get; }
    public bool IsComptimeFn { get; }
    public bool IsInstanceMethod { get; }
}

public class MemberSymbol : ExprSymbol
{
    public MemberSymbol(
        ExprSymbol expression,
        TypeSymbol type,
        string name
    )
    {
        Expression = expression;
        Name = name;
        Type = type;
    }

    public override TypeSymbol Type { get; }

    public ExprSymbol Expression { get; }
    public string Name { get; }
}
public class NameSymbol : ExprSymbol
{
    public NameSymbol(string name, TypeSymbol type)
    {
        Name = name;
        Type = type;
    }

    public override TypeSymbol Type { get; }
    public string Name { get; }
}
public class UnkownExprSymbol : ExprSymbol
{
    public override TypeSymbol Type => LangDefaults.Types.Unkown;
}

public class IntegerSymbol : ExprSymbol
{
    public IntegerSymbol(long value)
    {
        Value = value;
    }
    public long Value { get; }
    public override TypeSymbol Type => LangDefaults.Types.Integer;
}

public class BooleanSymbol : ExprSymbol
{
    public BooleanSymbol(bool value)
    {
        Value = value;
    }
    public bool Value { get; }
    public override TypeSymbol Type => LangDefaults.Types.Boolean;
}

public class StringSymbol : ExprSymbol
{
    public StringSymbol(string value)
    {
        Value = value;
    }
    public string Value { get; }
    public override TypeSymbol Type => LangDefaults.Types.String;
}

public class DecimalSymbol : ExprSymbol
{
    public DecimalSymbol(decimal value)
    {
        Value = value;
    }
    public decimal Value { get; }
    public override TypeSymbol Type => LangDefaults.Types.Decimal;
}

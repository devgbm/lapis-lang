

using System.Collections.Immutable;

namespace LapisLang.Core;

public abstract class ExprSymbol : Symbol
{
    public static ExprSymbol Unkown = new UnkownExprSymbol();
    public abstract bool IsCompileTime { get; }
    public abstract TypeSymbol Type { get; }
}
public class CallSymbol : ExprSymbol
{
    public CallSymbol(
        FuncSymbol function,
        ImmutableArray<ArgumentSymbol> arguments
    )
    {
        Function = function;
        Arguments = arguments;
        IsCompileTime = function.IsCompileTime;
        Type = function.ReturnType as TypeSymbol ?? LangDefaults.Types.Unkown;
    }
    public override bool IsCompileTime { get; }

    public override TypeSymbol Type { get; }

    public FuncSymbol Function { get; }
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

public class FuncSymbol : ExprSymbol
{
    public FuncSymbol(
        StatementSymbol statement,
        ImmutableArray<ParameterSymbol> parameters,
        ExprSymbol ReturnType,
        bool isComptimeFn
    )
    {
        Statement = statement;
        Parameters = parameters;
        this.ReturnType = ReturnType;
        IsComptimeFn = isComptimeFn;
    }
    public override bool IsCompileTime => true;

    public override TypeSymbol Type => LangDefaults.Types.Function;

    public StatementSymbol Statement { get; }
    public ImmutableArray<ParameterSymbol> Parameters { get; }
    public ExprSymbol ReturnType { get; }
    public bool IsComptimeFn { get; }
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
        IsCompileTime = expression.IsCompileTime;
        Name = name;
        Type = type;
    }
    public override bool IsCompileTime { get; }

    public override TypeSymbol Type { get; }

    public ExprSymbol Expression { get; }
    public string Name { get; }
}
public class NameSymbol : ExprSymbol
{
    public NameSymbol(string name, TypeSymbol type, bool isConstant)
    {
        Name = name;
        IsCompileTime = isConstant;
        Type = type;
    }
    public override bool IsCompileTime { get; }

    public override TypeSymbol Type { get; }
    public string Name { get; }
}
public class UnkownExprSymbol : ExprSymbol
{
    public override bool IsCompileTime => false;

    public override TypeSymbol Type => LangDefaults.Types.Unkown;
}

public class IntegerSymbol : ExprSymbol
{
    public IntegerSymbol(long value)
    {
        Value = value;
    }
    public long Value { get; }
    public override bool IsCompileTime => true;
    public override TypeSymbol Type => LangDefaults.Types.Integer;
}

public class BooleanSymbol : ExprSymbol
{
    public BooleanSymbol(bool value)
    {
        Value = value;
    }
    public bool Value { get; }
    public override bool IsCompileTime => true;
    public override TypeSymbol Type => LangDefaults.Types.Boolean;
}

public class StringSymbol : ExprSymbol
{
    public StringSymbol(string value)
    {
        Value = value;
    }
    public string Value { get; }
    public override bool IsCompileTime => true;
    public override TypeSymbol Type => LangDefaults.Types.String;
}

public class DecimalSymbol : ExprSymbol
{
    public DecimalSymbol(decimal value)
    {
        Value = value;
    }
    public decimal Value { get; }
    public override bool IsCompileTime => true;
    public override TypeSymbol Type => LangDefaults.Types.Decimal;
}


namespace LapisLang.Core;

public abstract class ExprSymbol : Symbol
{
    public static ExprSymbol Unkown = new UnkownExprSymbol();
    public abstract bool IsConstant { get; }
    public abstract TypeSymbol Type { get; }
}

public class NameSymbol : ExprSymbol
{
    public NameSymbol(string name, TypeSymbol type, ExprSymbol? contantSymbol = null)
    {
        Name = name;
        ContantSymbol = contantSymbol;
        IsConstant = contantSymbol is not null;
        Type = type;
    }
    public override bool IsConstant { get; }

    public override TypeSymbol Type { get; }

    public string Name { get; }
    public ExprSymbol? ContantSymbol { get; }
}
public class UnkownExprSymbol : ExprSymbol
{
    public override bool IsConstant => false;

    public override TypeSymbol Type => LangDefaults.Types.Unkown;
}

public class IntegerSymbol : ExprSymbol
{
    public IntegerSymbol(long value)
    {
        Value = value;
    }
    public long Value { get; }
    public override bool IsConstant => true;
    public override TypeSymbol Type => LangDefaults.Types.Integer;
}

public class BooleanSymbol : ExprSymbol
{
    public BooleanSymbol(bool value)
    {
        Value = value;
    }
    public bool Value { get; }
    public override bool IsConstant => true;
    public override TypeSymbol Type => LangDefaults.Types.Boolean;
}

public class StringSymbol : ExprSymbol
{
    public StringSymbol(string value)
    {
        Value = value;
    }
    public string Value { get; }
    public override bool IsConstant => true;
    public override TypeSymbol Type => LangDefaults.Types.String;
}

public class DecimalSymbol : ExprSymbol
{
    public DecimalSymbol(decimal value)
    {
        Value = value;
    }
    public decimal Value { get; }
    public override bool IsConstant => true;
    public override TypeSymbol Type => LangDefaults.Types.Decimal;
}
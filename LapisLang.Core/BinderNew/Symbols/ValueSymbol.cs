



namespace LapisLang.Core;
public class ValueSymbol : ExpressionSymbol
{
    public ValueSymbol(object? value, TypeSymbol type) : base(type)
    {
        Value = value;
    }

    public object? Value { get; }
}





namespace LapisLang.Core;

public class ValueSymbol : ExpressionSymbol
{
    public ValueSymbol(object? value, TypeSymbol type) : base(type)
    {
        Value = value;
    }

    public object? Value { get; }
}

public class NameSymbol : ExpressionSymbol
{
    public NameSymbol(string name, TypeSymbol type) : base(type)
    {
        Name = name;
    }

    public string Name { get; }
}

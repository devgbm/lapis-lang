namespace LapisLang.Core;

public class FieldSymbol : Symbol
{
    public FieldSymbol(string name, TypeSymbol type, bool isConstant)
    {
        Name = name;
        Type = type;
        IsConstant = isConstant;
    }

    public string Name { get; }
    public TypeSymbol Type { get; }
    public bool IsConstant { get; }

    public bool IsEquivalent(FieldSymbol? fieldSymbol)
    {
        return fieldSymbol is not null && fieldSymbol.Name == Name && fieldSymbol.Type.IsEquivalent(Type);
    }
}

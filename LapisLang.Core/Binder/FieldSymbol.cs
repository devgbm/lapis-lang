namespace LapisLang.Core;

public class FieldSymbol : Symbol
{
    public FieldSymbol(string name, ExprSymbol typeExpression)
    {
        Name = name;
        TypeExpression = typeExpression;
        Type = typeExpression as TypeSymbol ?? LangDefaults.Types.Unkown;
    }

    public string Name { get; }
    public ExprSymbol TypeExpression { get; }
    public TypeSymbol Type { get; }

    public bool IsEquivalent(FieldSymbol? fieldSymbol)
    {
        return fieldSymbol is not null && fieldSymbol.Name == Name && fieldSymbol.Type.IsEquivalent(Type);
    }
}

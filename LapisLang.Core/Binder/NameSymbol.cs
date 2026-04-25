namespace LapisLang.Core;

public class NameSymbol : ExprSymbol
{
    public NameSymbol(string name, TypeSymbol type, BindFlag flags = BindFlag.None)
    {
        Name = name;
        Type = type;
        Flags = flags;
    }

    public override TypeSymbol Type { get; }
    public string Name { get; }
}

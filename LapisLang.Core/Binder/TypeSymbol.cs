namespace LapisLang.Core;

public abstract class TypeSymbol : ExprSymbol
{
    private Dictionary<string, ExprSymbol> _members = new();
    public TypeSymbol()
    { }
    public TypeSymbol(string? debugName)
    {
        DebugName = debugName;
    }

    public ExprSymbol GetMember(string name)
    {
        return _members.GetValueOrDefault(name, ExprSymbol.Unkown);
    }

    public bool DefineMember(string name, ExprSymbol symbol)
    {
        if (_members.ContainsKey(name))
        {
            return false;
        }

        _members.Add(name, symbol);
        return true;
    }

    public string? DebugName { get; set; }
    public override TypeSymbol Type { get => LangDefaults.Types.Type; }
    public abstract bool IsEquivalent(ExprSymbol symbol);
}

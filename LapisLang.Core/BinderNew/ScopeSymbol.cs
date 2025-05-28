



namespace LapisLang.Core;

public class ScopeSymbol : Symbol
{
    private Dictionary<object?, Symbol> _scope = new();

    public bool DefineSymbol(object? key, Symbol value)
    {
        if (_scope.ContainsKey(key)) return false;
        _scope[key] = value;
        return true;
    }

    public bool GetSymbol(object? key, out Symbol? symbol)
    {
        if (!_scope.ContainsKey(key))
        {
            symbol = null;
            return false;
        }
        symbol = _scope[key];
        return true;
    }
}

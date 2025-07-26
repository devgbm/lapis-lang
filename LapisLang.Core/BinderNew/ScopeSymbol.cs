



namespace LapisLang.Core;


public interface IScopeSymbol
{
    bool DefineSymbol(object? key, Symbol value);

    bool GetSymbol(object? key, out Symbol? symbol);

    void SetSymbol(object? key, Symbol value);
}

public class ScopeSymbol : Symbol, IScopeSymbol
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

    public void SetSymbol(object? key, Symbol value)
    {
        _scope[key] = value;
    }
}

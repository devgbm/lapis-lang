



namespace LapisLang.Core;


public interface IScopeSymbol
{
    public IEnumerable<Symbol> GetSymbols();

    bool DefineSymbol(object? key, Symbol value);

    bool GetSymbol(object? key, out Symbol? symbol);

    void SetSymbol(object? key, Symbol value);
}

public class ScopeSymbol : Symbol, IScopeSymbol
{
    private Dictionary<object?, Symbol> _scope = new();
    private ScopeSymbol? _parent;
    public TypeSymbol? ExpectedReturn { get; set; }

    public IEnumerable<Symbol> GetSymbols() => _scope.Values;

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
            if (_parent is not null)
            {
                return _parent.GetSymbol(key, out symbol);
            }
            else
            {
                symbol = null;
                return false;
            }
        }
        symbol = _scope[key];
        return true;
    }

    public void SetSymbol(object? key, Symbol value)
    {
        _scope[key] = value;
    }


    public ScopeSymbol Derive()
    {
        var derived = new ScopeSymbol();
        derived._parent = this;
        return derived;
    }
}

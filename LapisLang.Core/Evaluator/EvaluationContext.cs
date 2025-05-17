


namespace LapisLang.Core;

public class EvaluationContext
{
    private Dictionary<VariableRune, object?> _variables = new();
    public EvaluationContext(NamespaceRune namespaceRune)
    {
        NamespaceRune = namespaceRune;
    }

    public NamespaceRune NamespaceRune { get; }

    public Rune ResolveName(string name)
    {
        var resolved = NamespaceRune.Query(name);
        return resolved;
    }

    internal void DeclareVariable(VariableRune rune, object? value)
    {
        NamespaceRune.Add(rune);
        _variables.Add(rune, value);
    }
}

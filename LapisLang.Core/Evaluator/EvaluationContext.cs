

namespace LapisLang.Core;

public class EvaluationContext
{
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
}



using System.Diagnostics.CodeAnalysis;

namespace LapisLang.Core;

public class BindingContext
{
    public BindingContext(NamespaceRune? namespaceRune = null)
    {
        NamespaceRune = namespaceRune ?? LangDefaults.RootNamespace();
    }

    public NamespaceRune NamespaceRune { get; }

    public bool TryResolveName(string name, [NotNullWhen(true)] out Rune? rune)
    {
        rune = NamespaceRune.Query(name);
        return rune is not null;
    }
}

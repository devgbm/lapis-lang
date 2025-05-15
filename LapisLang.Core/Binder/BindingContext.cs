

namespace LapisLang.Core;

public class BindingContext
{
    public BindingContext(NamespaceRune? namespaceRune = null)
    {
        NamespaceRune = namespaceRune ?? LangDefaults.RootNamespace();
    }

    public NamespaceRune NamespaceRune { get; }
}

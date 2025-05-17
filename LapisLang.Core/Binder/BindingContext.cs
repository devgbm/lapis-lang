

using System.Diagnostics.CodeAnalysis;

namespace LapisLang.Core;

public class BindingContext
{
    private Rune _scope;
    public BindingContext(NamespaceRune? namespaceRune = null)
    {
        NamespaceRune = namespaceRune ?? LangDefaults.RootNamespace();
        _scope = NamespaceRune;
    }

    public NamespaceRune NamespaceRune { get; }

    public bool TryResolveName(string name, [NotNullWhen(true)] out Rune? rune)
    {
        rune = NamespaceRune.Query(name);
        return rune is not null;
    }

    internal TypeRune ResolveTypename(TypeNameSyntax typeName)
    {
        var name = typeName.Identifier.String;
        var foundRune = NamespaceRune.Query(name) as TypeRune;
        return foundRune ?? LangDefaults.Types.Unkown;
    }

    internal bool TryDeclareVariable(VariableRune rune)
    {
        if (rune.Expression is BoundTypeExpression bte)
        {
            var typeRune = PromoteToType(rune.Name, bte);
            return _scope.Add(typeRune);
        }
        else
        {
            return _scope.Add(rune);
        }
    }

    private TypeRune PromoteToType(string name, BoundTypeExpression bte)
    {
        var typeRune = new TypeRune(name);
        foreach (var field in bte.BoundFieldSyntaxes)
        {
            var fieldRune = new FieldRune(field.Name, field.TypeRune);
            typeRune.Add(fieldRune);
        }
        return typeRune;
    }
}

using System.Diagnostics.CodeAnalysis;
using Lapis.Ast.Typed;
using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker;

public enum BindingKind
{
    Value,
    Parameter,
    Native,
}

public sealed record BindingInfo(BindingId Id, string Name, LapisType Type, SourceSpan Span, BindingKind Kind)
{
    /// <summary>
    /// O valor deste binding, quando conhecido em tempo de compilação — é o que
    /// permite passá-lo como argumento const genérico (Q18).
    ///
    /// Só um `def` ligado a um literal ou a uma função literal tem constante. Um
    /// parâmetro nunca tem, mesmo o de um `fn&lt;N: Int&gt;`: seu valor chega na
    /// instanciação, não na declaração.
    /// </summary>
    public GenericArgument? Constant { get; init; }
}

/// <summary>
/// Escopo léxico do type checker.
///
/// Sombrear em escopo interno é permitido (bindings são imutáveis, então não é
/// mutação); redefinir no mesmo escopo é <c>LAP0202</c>.
/// </summary>
public sealed class Scope(Scope? parent)
{
    private readonly Dictionary<string, BindingInfo> _bindings = new(StringComparer.Ordinal);

    public Scope? Parent { get; } = parent;

    public static Scope Root() => new(null);

    public Scope Child() => new(this);

    public bool TryLookup(string name, [NotNullWhen(true)] out BindingInfo? binding)
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            if (scope._bindings.TryGetValue(name, out binding))
            {
                return true;
            }
        }

        binding = null;
        return false;
    }

    public bool TryLookupLocal(string name, [NotNullWhen(true)] out BindingInfo? binding) =>
        _bindings.TryGetValue(name, out binding);

    public void Declare(BindingInfo binding) => _bindings[binding.Name] = binding;

    /// <summary>Nomes visíveis, para a sugestão "você quis dizer" dos diagnósticos.</summary>
    public IEnumerable<string> VisibleNames()
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            foreach (var name in scope._bindings.Keys)
            {
                yield return name;
            }
        }
    }
}

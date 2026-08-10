using System.Diagnostics.CodeAnalysis;

namespace Lapis.Runtime;

/// <summary>
/// Ambiente léxico (spec §31): mapa de nomes encadeado a um pai.
///
/// Imutável e persistente: <see cref="Extend"/> devolve um novo ambiente. Com
/// bindings imutáveis (spec §8) isso é ao mesmo tempo correto e barato, e é o que
/// permite que closures capturem o ambiente sem risco de vê-lo mudar depois.
/// </summary>
public sealed class Environment
{
    /// <summary>
    /// Mapas pequenos dominam (parâmetros de função, poucos <c>def</c> por escopo),
    /// então uma busca linear num array bate um dicionário. Sem otimizar além disso
    /// antes de medir.
    /// </summary>
    private readonly (string Name, Value Value)[] _bindings;

    private Environment(Environment? parent, (string, Value)[] bindings)
    {
        Parent = parent;
        _bindings = bindings;
    }

    public static readonly Environment Empty = new(null, []);

    public Environment? Parent { get; }

    public int LocalCount => _bindings.Length;

    public bool TryLookup(string name, [NotNullWhen(true)] out Value? value)
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            // De trás para frente: o binding mais recente do mesmo nome vence.
            for (var i = scope._bindings.Length - 1; i >= 0; i--)
            {
                if (string.Equals(scope._bindings[i].Name, name, StringComparison.Ordinal))
                {
                    value = scope._bindings[i].Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    public Environment Extend(string name, Value value) => new(this, [(name, value)]);

    public Environment ExtendAll(IReadOnlyList<(string Name, Value Value)> bindings)
    {
        if (bindings.Count == 0)
        {
            return this;
        }

        var array = new (string, Value)[bindings.Count];

        for (var i = 0; i < bindings.Count; i++)
        {
            array[i] = (bindings[i].Name, bindings[i].Value);
        }

        return new Environment(this, array);
    }
}

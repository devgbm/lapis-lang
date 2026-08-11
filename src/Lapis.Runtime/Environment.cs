using System.Diagnostics.CodeAnalysis;
using Lapis.Diagnostics;

namespace Lapis.Runtime;

/// <summary>
/// Ambiente léxico (spec §31): mapa de nomes encadeado a um pai.
///
/// Persistente na estrutura: <see cref="Extend"/> devolve um novo ambiente, e
/// nenhum ambiente já criado ganha ou perde nomes. É o que permite a uma closure
/// capturar o ambiente sem risco de vê-lo mudar de forma.
///
/// O <b>conteúdo</b> de um slot marcado como mutável muda, e é assim que um
/// <c>var</c> avança entre as voltas de um laço (Q25). Isso não quebra a captura
/// porque um <c>var</c> não atravessa fronteira de função — o checker recusa
/// (<c>LAP0207</c>), então nenhuma closure enxerga um slot mutável.
/// </summary>
public sealed class Environment
{
    /// <summary>
    /// Mapas pequenos dominam (parâmetros de função, poucos <c>def</c> por escopo),
    /// então uma busca linear num array bate um dicionário. Sem otimizar além disso
    /// antes de medir.
    /// </summary>
    private readonly (string Name, Value Value, bool IsMutable)[] _bindings;

    private Environment(Environment? parent, (string, Value, bool)[] bindings)
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

    public Environment Extend(string name, Value value, bool isMutable = false) =>
        new(this, [(name, value, isMutable)]);

    public Environment ExtendAll(IReadOnlyList<(string Name, Value Value)> bindings)
    {
        if (bindings.Count == 0)
        {
            return this;
        }

        var array = new (string, Value, bool)[bindings.Count];

        for (var i = 0; i < bindings.Count; i++)
        {
            array[i] = (bindings[i].Name, bindings[i].Value, false);
        }

        return new Environment(this, array);
    }

    /// <summary>
    /// Troca o valor do slot mutável mais recente com este nome.
    ///
    /// Devolve <c>false</c> quando não há slot nenhum; atribuir a um slot
    /// imutável é bug do checker (<c>LAP0206</c> deveria ter barrado), e por isso
    /// é exceção interna, não diagnóstico.
    /// </summary>
    public bool TryAssign(string name, Value value)
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            for (var i = scope._bindings.Length - 1; i >= 0; i--)
            {
                if (!string.Equals(scope._bindings[i].Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!scope._bindings[i].IsMutable)
                {
                    throw new InternalCompilerException(
                        $"atribuição a binding imutável '{name}' chegou ao evaluator");
                }

                scope._bindings[i].Value = value;
                return true;
            }
        }

        return false;
    }
}

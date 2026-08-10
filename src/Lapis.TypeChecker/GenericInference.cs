using System.Collections.Immutable;
using Lapis.Ast.Types;

namespace Lapis.TypeChecker;

/// <summary>
/// Inferência de argumentos genéricos por casamento de 1ª ordem (Q7).
///
/// É o mínimo necessário para que <c>print(result)</c> da spec §34 funcione com
/// <c>print: fn&lt;T&gt;(value: T) Void</c>, e é previsível como a spec §47 pede:
/// nada de unificação global, apenas casamento posicional entre os tipos dos
/// parâmetros formais e os dos argumentos reais.
///
/// Em M4 esta mesma máquina passa a servir aos generics escritos pelo usuário.
/// </summary>
public static class GenericInference
{
    public sealed record Result(
        bool Success,
        ImmutableArray<LapisType> TypeArguments,
        FunctionType Instantiated,
        string? UninferredParameter);

    public static Result Infer(FunctionType signature, ImmutableArray<LapisType> argumentTypes)
    {
        if (!signature.IsGeneric)
        {
            return new Result(true, [], signature, null);
        }

        var bindings = new Dictionary<string, LapisType>(StringComparer.Ordinal);
        var count = Math.Min(signature.Parameters.Length, argumentTypes.Length);

        for (var i = 0; i < count; i++)
        {
            Match(signature.Parameters[i], argumentTypes[i], bindings);
        }

        foreach (var parameter in signature.TypeParameters)
        {
            if (!bindings.ContainsKey(parameter.Name))
            {
                return new Result(false, [], signature, parameter.Name);
            }
        }

        var arguments = signature.TypeParameters
            .Select(p => bindings[p.Name])
            .ToImmutableArray();

        return new Result(true, arguments, (FunctionType)Substitute(signature, bindings), null);
    }

    /// <summary>
    /// Casa um tipo formal contra um real, coletando bindings. Conflitos não são
    /// reportados aqui: o primeiro casamento vence e a incompatibilidade aparece
    /// depois, como erro de argumento (LAP0222), com um span melhor.
    /// </summary>
    private static void Match(LapisType formal, LapisType actual, Dictionary<string, LapisType> bindings)
    {
        switch (formal)
        {
            case TypeParameterType parameter:
                if (actual is not NeverType and not ErrorType)
                {
                    bindings.TryAdd(parameter.Name, actual);
                }

                break;

            case ArrayType array when actual is ArrayType actualArray:
                Match(array.Element, actualArray.Element, bindings);
                break;

            case FunctionType function when actual is FunctionType actualFunction:
                var count = Math.Min(function.Parameters.Length, actualFunction.Parameters.Length);

                for (var i = 0; i < count; i++)
                {
                    Match(function.Parameters[i], actualFunction.Parameters[i], bindings);
                }

                Match(function.Return, actualFunction.Return, bindings);
                break;
        }
    }

    /// <summary>Aplica os bindings, produzindo o tipo instanciado.</summary>
    public static LapisType Substitute(LapisType type, IReadOnlyDictionary<string, LapisType> bindings) => type switch
    {
        TypeParameterType parameter => bindings.TryGetValue(parameter.Name, out var bound) ? bound : parameter,
        ArrayType array => new ArrayType(Substitute(array.Element, bindings)),
        FunctionType function => new FunctionType(
            [.. function.Parameters.Select(p => Substitute(p, bindings))],
            Substitute(function.Return, bindings),
            []),
        _ => type,
    };
}

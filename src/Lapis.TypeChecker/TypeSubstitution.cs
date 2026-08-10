using Lapis.Ast.Types;

namespace Lapis.TypeChecker;

/// <summary>
/// Substituição de parâmetros de tipo por argumentos.
///
/// Não há inferência: argumentos genéricos são sempre explícitos (Q7). Esta é a
/// única máquina de generics do checker, usada ao instanciar uma assinatura
/// genérica com os argumentos que o programa escreveu.
/// </summary>
public static class TypeSubstitution
{
    public static LapisType Apply(LapisType type, IReadOnlyDictionary<string, LapisType> bindings) => type switch
    {
        TypeParameterType parameter => bindings.TryGetValue(parameter.Name, out var bound) ? bound : parameter,
        ArrayType array => new ArrayType(Apply(array.Element, bindings)),
        FunctionType function => new FunctionType(
            [.. function.Parameters.Select(p => Apply(p, bindings))],
            Apply(function.Return, bindings),
            []),
        _ => type,
    };
}

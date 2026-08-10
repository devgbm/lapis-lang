using System.Collections.Immutable;
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

        // Os parâmetros da própria função sombreiam os de fora: `fn<T>` aninhado
        // dentro de outro `fn<T>` liga o seu, e substituir aqui trocaria o nome
        // errado. Uma assinatura genérica atravessa a substituição intacta.
        FunctionType { IsGeneric: true } => type,

        FunctionType function => new FunctionType(
            [.. function.Parameters.Select(p => Apply(p, bindings))],
            Apply(function.Return, bindings),
            []),

        // `Result<T, IndexError>` com `T` ligado vira `Result<Int, IndexError>`.
        // Sem este caso, um parâmetro de tipo dentro de um tipo genérico
        // sobreviveria à instanciação e nada casaria com ele.
        NamedType named => new NamedType(named.Definition, Apply(named.Arguments, bindings)),

        MetaType meta => new MetaType(meta.Definition, Apply(meta.Arguments, bindings)),

        _ => type,
    };

    private static ImmutableArray<GenericArgument> Apply(
        ImmutableArray<GenericArgument> arguments,
        IReadOnlyDictionary<string, LapisType> bindings) =>
        arguments.IsDefaultOrEmpty
            ? arguments
            : [.. arguments.Select(a => a is TypeArgument t ? new TypeArgument(Apply(t.Type, bindings)) : a)];
}

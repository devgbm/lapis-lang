using System.Collections.Immutable;
using Lapis.Ast.Types;

namespace Lapis.TypeChecker;

/// <summary>
/// Substituição de parâmetros genéricos pelos argumentos correspondentes.
///
/// Não há inferência: argumentos genéricos são sempre explícitos (Q7). Esta é a
/// única máquina de generics do checker, usada ao instanciar uma declaração
/// genérica com os argumentos que o programa escreveu.
///
/// As ligações são indexadas pelo <b>nome do parâmetro</b> e cobrem os dois
/// tipos: um parâmetro de tipo (<c>T</c>) é substituído onde aparece como tipo,
/// e um parâmetro const (<c>N</c>) onde aparece como argumento genérico — é isso
/// que fecha as constantes simbólicas de Q18.
/// </summary>
public static class TypeSubstitution
{
    public static LapisType Apply(LapisType type, IReadOnlyDictionary<string, GenericArgument> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        return type switch
        {
            TypeParameterType parameter =>
                bindings.TryGetValue(parameter.Name, out var bound) && bound is TypeArgument argument
                    ? argument.Type
                    : parameter,

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
            // Sem este caso, um parâmetro dentro de um tipo genérico sobreviveria à
            // instanciação e nada casaria com ele.
            NamedType named => new NamedType(named.Definition, Apply(named.Arguments, bindings)),

            MetaType meta => new MetaType(meta.Definition, Apply(meta.Arguments, bindings)),

            _ => type,
        };
    }

    public static ImmutableArray<GenericArgument> Apply(
        ImmutableArray<GenericArgument> arguments,
        IReadOnlyDictionary<string, GenericArgument> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        return arguments.IsDefaultOrEmpty ? arguments : [.. arguments.Select(a => Apply(a, bindings))];
    }

    private static GenericArgument Apply(
        GenericArgument argument,
        IReadOnlyDictionary<string, GenericArgument> bindings) => argument switch
        {
            TypeArgument a => new TypeArgument(Apply(a.Type, bindings)),

            // A constante simbólica de Q18 se fecha aqui: `FixedArray<Int, N>`
            // dentro de `fn<N: Int>` vira `FixedArray<Int, 3>` quando a assinatura
            // é instanciada com `<3>`.
            ConstParameterArgument a when bindings.TryGetValue(a.Name, out var bound) => bound,

            _ => argument,
        };
}

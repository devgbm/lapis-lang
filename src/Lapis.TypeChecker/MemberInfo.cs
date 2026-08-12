using System.Collections.Immutable;
using Lapis.Ast.Typed;
using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker;

/// <summary>
/// Um membro declarado por <c>def T.m = e;</c> (plano 21 §21.5).
///
/// <paramref name="SyntheticName"/> é o nome com que o valor vive na Core
/// (<c>User#hello</c>): o desugar já emitiu o <c>Let</c>, então o evaluator não
/// precisa de caminho especial — basta ler o nome.
///
/// <paramref name="Kind"/> separa valor de método estático, e vai separar método
/// de instância no plano 22. A separação existe porque <c>User.hello</c> e
/// <c>user.hello()</c> não podem ser dois caminhos para a mesma coisa.
/// </summary>
/// <paramref name="OwnerPattern"/> é o alcance da declaração (plano 23 §23.4):
/// vazio para um dono não genérico, e um argumento por parâmetro do tipo quando
/// há — <see cref="WildcardArgument"/> onde a declaração escreveu <c>?</c>. É por
/// ele que a resolução filtra os candidatos, e é o que faz o mesmo nome poder ter
/// mais de uma declaração.
public sealed record MemberInfo(
    string Name,
    MemberAccessKind Kind,
    LapisType Type,
    string SyntheticName,
    SourceSpan Span,
    ImmutableArray<GenericArgument> OwnerPattern)
{
    /// <summary>
    /// O padrão aceita estes argumentos? Posição a posição: curinga aceita
    /// qualquer coisa, o resto exige igualdade (§23.3).
    /// </summary>
    public bool Accepts(ImmutableArray<GenericArgument> arguments) =>
        Covers(OwnerPattern, arguments);

    /// <summary>
    /// Existe algum tipo que casa com os dois padrões? É a pergunta da
    /// sobreposição (§23.5), e com curinga ela é a mesma comparação posicional —
    /// só que sem direção: <c>?</c> de qualquer lado casa.
    /// </summary>
    public bool Overlaps(MemberInfo other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (OwnerPattern.Length != other.OwnerPattern.Length)
        {
            return false;
        }

        for (var i = 0; i < OwnerPattern.Length; i++)
        {
            if (OwnerPattern[i] is not WildcardArgument
                && other.OwnerPattern[i] is not WildcardArgument
                && OwnerPattern[i] != other.OwnerPattern[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Como o dono aparece numa mensagem: <c>Result</c> quando não há padrão,
    /// <c>Result&lt;Int, ?&gt;</c> quando há.
    /// </summary>
    public static string OwnerToDisplayString(
        TypeDefinition definition, ImmutableArray<GenericArgument> pattern)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return pattern.IsDefaultOrEmpty
            ? definition.Name
            : $"{definition.Name}<{string.Join(", ", pattern.Select(a => a.ToDisplayString()))}>";
    }

    private static bool Covers(
        ImmutableArray<GenericArgument> pattern, ImmutableArray<GenericArgument> arguments)
    {
        if (pattern.Length != arguments.Length)
        {
            return false;
        }

        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] is not WildcardArgument && pattern[i] != arguments[i])
            {
                return false;
            }
        }

        return true;
    }
}

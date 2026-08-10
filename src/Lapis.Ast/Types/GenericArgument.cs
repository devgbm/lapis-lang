using System.Collections.Immutable;

namespace Lapis.Ast.Types;

/// <summary>
/// Um parâmetro genérico de <b>declaração</b> (Q1): <c>T</c> é um parâmetro de
/// tipo, <c>N: Int</c> é um parâmetro const.
///
/// A distinção declaração/uso é o que a spec §13 deixava implícita: a declaração
/// <b>nomeia</b> cada parâmetro, o uso fornece um <see cref="GenericArgument"/>
/// para cada um.
/// </summary>
public sealed record GenericParameter(string Name, LapisType? ConstType)
{
    public static GenericParameter OfType(string name) => new(name, null);

    public bool IsConst => ConstType is not null;

    /// <summary>Este parâmetro visto como tipo, para substituição.</summary>
    public TypeParameterType AsType => new(Name);

    public string ToDisplayString() => IsConst ? $"{Name}: {ConstType!.ToDisplayString()}" : Name;

    public override string ToString() => ToDisplayString();
}

/// <summary>
/// Um argumento genérico de <b>uso</b>: um tipo (<c>Int</c>) ou um valor constante
/// (<c>3</c>, <c>"a"</c>, <c>fn() Int { return 1; }</c>) — spec §13.
///
/// Argumentos entram na identidade nominal de <see cref="NamedType"/>, então
/// <c>FixedArray&lt;Int, 3&gt;</c> e <c>FixedArray&lt;Int, 4&gt;</c> são tipos
/// distintos.
/// </summary>
public abstract record GenericArgument
{
    public static ImmutableArray<GenericArgument> OfTypes(IEnumerable<LapisType> types) =>
        [.. types.Select(t => (GenericArgument)new TypeArgument(t))];

    public abstract string ToDisplayString();

    public sealed override string ToString() => ToDisplayString();
}

public sealed record TypeArgument(LapisType Type) : GenericArgument
{
    public override string ToDisplayString() => Type.ToDisplayString();
}

public sealed record ConstArgument(ConstantValue Value) : GenericArgument
{
    public override string ToDisplayString() => Value.ToDisplayString();
}

/// <summary>
/// Uma função literal usada como argumento genérico — o último item do exemplo da
/// spec §13.
///
/// A identidade é <b>estrutural</b>, dada pelo código-fonte normalizado da função
/// em <see cref="Shape"/>: duas funções escritas igual produzem o mesmo argumento,
/// e portanto o mesmo tipo. Sem isso o tipo seria impossível de escrever duas
/// vezes, e uma anotação como <c>SomeType&lt;fn() Int { return 1; }&gt;</c> nunca
/// casaria com o valor correspondente.
/// </summary>
public sealed record ConstFunctionArgument(string Shape, FunctionType Signature) : GenericArgument
{
    public override string ToDisplayString() => Shape;
}

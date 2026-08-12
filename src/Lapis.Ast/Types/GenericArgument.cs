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
/// <c>?</c> — o curinga do dono de um membro (Q27, plano 23):
/// <c>def Result&lt;?, ?&gt;.isOk</c>.
///
/// <b>Não é um parâmetro.</b> Ele não liga nome nenhum, e é isso que dissolve a
/// tensão com a Q7: não há o que unificar, e não há argumento a transportar para o
/// corpo do membro. O preço é declarado — sem nome, o corpo não consegue escrever
/// o tipo do argumento do dono.
///
/// É o mesmo <c>?</c> de <c>[Int;?]</c>, com a mesma leitura ("não se diz") e a
/// mesma regra de atribuibilidade, numa direção só: <c>Result&lt;Int, Error&gt;</c>
/// cabe em <c>Result&lt;?, ?&gt;</c>, e não o contrário. Esquecer o que se sabia é
/// seguro; afirmar o que não se sabe, não.
///
/// Só existe em posição de <b>dono de membro</b>. Como tipo de valor seria um
/// <c>Any</c> estrutural pela porta dos fundos, e é <c>LAP0721</c>.
/// </summary>
public sealed record WildcardArgument : GenericArgument
{
    public static readonly WildcardArgument Instance = new();

    public override string ToDisplayString() => "?";
}

/// <summary>
/// Um parâmetro const repassado adiante: <c>N</c> dentro de
/// <c>fn&lt;N: Int&gt;</c> usado como argumento de outro genérico (Q18).
///
/// É uma constante <b>simbólica</b>: constante por construção — parâmetros const
/// só recebem valores conhecidos em tempo de compilação — mas de valor ainda
/// desconhecido, porque quem o fixa é a instanciação de fora. A substituição o
/// fecha quando ela acontece; até lá, <c>FixedArray&lt;Int, N&gt;</c> é um tipo
/// legítimo, distinto de <c>FixedArray&lt;Int, 3&gt;</c>.
///
/// É o análogo, no mundo dos valores, do que <see cref="TypeParameterType"/> é no
/// mundo dos tipos — e, como ele, tem identidade por nome.
/// </summary>
public sealed record ConstParameterArgument(string Name, LapisType Type) : GenericArgument
{
    public override string ToDisplayString() => Name;
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

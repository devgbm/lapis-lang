using System.Collections.Immutable;

namespace Lapis.Ast.Types;

public enum PrimitiveKind
{
    Int,
    Float,
    Bool,
    Str,
    Void,
}

/// <summary>
/// Modelo semântico de tipos. Vive em <c>Lapis.Ast</c> — e não no type checker —
/// porque runtime e partial evaluator precisam falar de tipos sem depender da
/// checagem (plano 02 §2.5).
///
/// Todos os tipos são <c>record</c>: comparação de tipos é igualdade estrutural.
/// </summary>
public abstract record LapisType
{
    public abstract string ToDisplayString();

    public sealed override string ToString() => ToDisplayString();
}

public sealed record PrimitiveType(PrimitiveKind Kind) : LapisType
{
    public static readonly PrimitiveType Int = new(PrimitiveKind.Int);
    public static readonly PrimitiveType Float = new(PrimitiveKind.Float);
    public static readonly PrimitiveType Bool = new(PrimitiveKind.Bool);
    public static readonly PrimitiveType Str = new(PrimitiveKind.Str);
    public static readonly PrimitiveType Void = new(PrimitiveKind.Void);

    public bool IsNumeric => Kind is PrimitiveKind.Int or PrimitiveKind.Float;

    public override string ToDisplayString() => Kind.ToString();
}

/// <summary>
/// Tipo bottom, interno e não escrevível em código-fonte. É o tipo de
/// <c>return e</c>, e a única relação de subtipagem da linguagem é
/// <c>Never &lt;: T</c> para todo <c>T</c> (plano 02 §2.5, Q13).
/// </summary>
public sealed record NeverType : LapisType
{
    public static readonly NeverType Instance = new();

    public override string ToDisplayString() => "Never";
}

/// <summary>Absorve operações para evitar cascata de diagnósticos.</summary>
public sealed record ErrorType : LapisType
{
    public static readonly ErrorType Instance = new();

    public override string ToDisplayString() => "<erro>";
}

/// <summary>
/// Tipo top, interno e não escrevível em código-fonte. Existe apenas para tipar
/// o parâmetro de primitivas que aceitam qualquer valor — hoje só <c>print</c>.
///
/// Nenhuma sintaxe produz <c>Any</c> e nenhum valor tem <c>Any</c> como tipo, então
/// ele não é uma brecha no sistema de tipos: só aparece na assinatura de um
/// nativo. Ver Q7 no apêndice C.
/// </summary>
public sealed record AnyType : LapisType
{
    public static readonly AnyType Instance = new();

    public override string ToDisplayString() => "Any";
}

public sealed record ArrayType(LapisType Element) : LapisType
{
    public override string ToDisplayString() => $"{Element.ToDisplayString()}[]";
}

public sealed record TypeParameterType(string Name) : LapisType
{
    public override string ToDisplayString() => Name;
}

/// <summary>
/// Instância de um tipo definido pelo usuário: <c>Result&lt;Int, IndexError&gt;</c>,
/// <c>Color</c>.
///
/// A identidade é <b>nominal</b>: dois <c>enum { A }</c> escritos separadamente são
/// tipos distintos, porque <see cref="TypeDefinition"/> é comparado por
/// referência.
/// </summary>
public sealed record NamedType(TypeDefinition Definition, ImmutableArray<GenericArgument> Arguments) : LapisType
{
    public override string ToDisplayString() =>
        Arguments.IsDefaultOrEmpty
            ? Definition.Name
            : $"{Definition.Name}<{string.Join(", ", Arguments.Select(a => a.ToDisplayString()))}>";

    public bool Equals(NamedType? other) =>
        other is not null
        && ReferenceEquals(Definition, other.Definition)
        && Arguments.SequenceEqual(other.Arguments);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Definition.Id);

        foreach (var argument in Arguments)
        {
            hash.Add(argument);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// O tipo de uma expressão que <b>é</b> um tipo. Em <c>def Color = enum { Red };</c>,
/// <c>Color</c> tem tipo <c>MetaType(NamedType(Color))</c>.
///
/// Existe porque tipos e enums são expressões de primeira classe (spec §14, §15).
///
/// <see cref="Arguments"/> guarda os argumentos genéricos já aplicados: o tipo de
/// <c>Result&lt;Int, IndexError&gt;</c> em posição de expressão, do qual
/// <c>.Ok</c> extrai um construtor já instanciado.
/// </summary>
public sealed record MetaType(TypeDefinition Definition, ImmutableArray<GenericArgument> Arguments) : LapisType
{
    public MetaType(TypeDefinition definition)
        : this(definition, [])
    {
    }

    public override string ToDisplayString() =>
        Arguments.IsDefaultOrEmpty
            ? $"<tipo {Definition.Name}>"
            : $"<tipo {Definition.Name}<{string.Join(", ", Arguments.Select(a => a.ToDisplayString()))}>>";

    public bool Equals(MetaType? other) =>
        other is not null
        && ReferenceEquals(Definition, other.Definition)
        && Arguments.SequenceEqual(other.Arguments);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Definition.Id);

        foreach (var argument in Arguments)
        {
            hash.Add(argument);
        }

        return hash.ToHashCode();
    }
}

public sealed record FunctionType(
    ImmutableArray<LapisType> Parameters,
    LapisType Return,
    ImmutableArray<GenericParameter> TypeParameters) : LapisType
{
    public static FunctionType Of(IEnumerable<LapisType> parameters, LapisType returnType) =>
        new([.. parameters], returnType, []);

    public bool IsGeneric => !TypeParameters.IsDefaultOrEmpty;

    public override string ToDisplayString()
    {
        var generics = IsGeneric
            ? "<" + string.Join(", ", TypeParameters.Select(p => p.ToDisplayString())) + ">"
            : string.Empty;

        return $"fn{generics}({string.Join(", ", Parameters.Select(p => p.ToDisplayString()))}) {Return.ToDisplayString()}";
    }

    public bool Equals(FunctionType? other) =>
        other is not null
        && Parameters.SequenceEqual(other.Parameters)
        && Return == other.Return
        && TypeParameters.SequenceEqual(other.TypeParameters);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Return);

        foreach (var parameter in Parameters)
        {
            hash.Add(parameter);
        }

        foreach (var typeParameter in TypeParameters)
        {
            hash.Add(typeParameter);
        }

        return hash.ToHashCode();
    }
}

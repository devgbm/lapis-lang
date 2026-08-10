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

public sealed record ArrayType(LapisType Element) : LapisType
{
    public override string ToDisplayString() => $"{Element.ToDisplayString()}[]";
}

public sealed record TypeParameterType(string Name) : LapisType
{
    public override string ToDisplayString() => Name;
}

public sealed record FunctionType(
    ImmutableArray<LapisType> Parameters,
    LapisType Return,
    ImmutableArray<TypeParameterType> TypeParameters) : LapisType
{
    public static FunctionType Of(IEnumerable<LapisType> parameters, LapisType returnType) =>
        new([.. parameters], returnType, []);

    public bool IsGeneric => !TypeParameters.IsDefaultOrEmpty;

    public override string ToDisplayString()
    {
        var generics = IsGeneric
            ? "<" + string.Join(", ", TypeParameters.Select(p => p.Name)) + ">"
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

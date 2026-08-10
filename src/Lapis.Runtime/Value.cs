using System.Collections.Immutable;
using Lapis.Ast.Core;
using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.Runtime;

/// <summary>
/// Valores de execução (spec §27).
///
/// Todos imutáveis — não há mutação na v0.2 (spec §8) — o que dispensa cópias
/// defensivas e permite reusar valores entre evaluator e partial evaluator.
/// </summary>
public abstract record Value
{
    public abstract LapisType Type { get; }
}

public sealed record IntValue(long Value) : Value
{
    public override LapisType Type => PrimitiveType.Int;
}

public sealed record FloatValue(double Value) : Value
{
    public override LapisType Type => PrimitiveType.Float;
}

public sealed record BoolValue(bool Value) : Value
{
    public static readonly BoolValue True = new(true);
    public static readonly BoolValue False = new(false);

    public static BoolValue Of(bool value) => value ? True : False;

    public override LapisType Type => PrimitiveType.Bool;
}

public sealed record StrValue(string Value) : Value
{
    public override LapisType Type => PrimitiveType.Str;
}

public sealed record VoidValue : Value
{
    public static readonly VoidValue Instance = new();

    public override LapisType Type => PrimitiveType.Void;
}

/// <summary>
/// Guarda o tipo do elemento porque um array vazio precisa saber o que carrega —
/// para formatação e para o partial evaluator.
/// </summary>
public sealed record ArrayValue(ImmutableArray<Value> Elements, LapisType ElementType) : Value
{
    public override LapisType Type => new ArrayType(ElementType);

    public bool Equals(ArrayValue? other) =>
        other is not null && ElementType == other.ElementType && Elements.SequenceEqual(other.Elements);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ElementType);

        foreach (var element in Elements)
        {
            hash.Add(element);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Uma variante de enum construída: <c>Result.Ok(20)</c>.</summary>
public sealed record EnumValue(
    TypeDefinition Definition,
    int VariantIndex,
    ImmutableArray<Value> Payload,
    ImmutableArray<LapisType> TypeArguments) : Value
{
    public VariantInfo Variant => Definition.Variants[VariantIndex];

    public override LapisType Type => new NamedType(Definition, TypeArguments);

    public bool Equals(EnumValue? other) =>
        other is not null
        && ReferenceEquals(Definition, other.Definition)
        && VariantIndex == other.VariantIndex
        && Payload.SequenceEqual(other.Payload);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Definition.Id);
        hash.Add(VariantIndex);

        foreach (var value in Payload)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Uma instância de <c>type</c>: o que <c>.User { id: 1 }</c> produz.</summary>
public sealed record StructValue(
    TypeDefinition Definition,
    ImmutableArray<Value> Fields,
    ImmutableArray<LapisType> TypeArguments) : Value
{
    public override LapisType Type => new NamedType(Definition, TypeArguments);

    public bool Equals(StructValue? other) =>
        other is not null
        && ReferenceEquals(Definition, other.Definition)
        && Fields.SequenceEqual(other.Fields);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Definition.Id);

        foreach (var field in Fields)
        {
            hash.Add(field);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Um tipo usado como valor: o que <c>def Color = enum { ... };</c> liga.</summary>
public sealed record TypeValue(TypeDefinition Definition) : Value
{
    public override LapisType Type => new MetaType(Definition);

    public bool Equals(TypeValue? other) => other is not null && ReferenceEquals(Definition, other.Definition);

    public override int GetHashCode() => Definition.Id;
}

/// <summary>
/// Construtor de variante parcialmente aplicado: o valor de <c>Result.Ok</c> antes
/// de receber a carga. Chamá-lo produz um <see cref="EnumValue"/>.
/// </summary>
public sealed record VariantConstructorValue(
    TypeDefinition Definition,
    int VariantIndex,
    FunctionType Signature,
    ImmutableArray<LapisType> TypeArguments) : Value
{
    public override LapisType Type => Signature;

    public bool Equals(VariantConstructorValue? other) =>
        other is not null
        && ReferenceEquals(Definition, other.Definition)
        && VariantIndex == other.VariantIndex;

    public override int GetHashCode() => HashCode.Combine(Definition.Id, VariantIndex);
}

/// <summary>
/// Uma closure guarda parâmetros, corpo e o ambiente capturado (spec §32).
///
/// <see cref="Equals(ClosureValue)"/> lança de propósito: o type checker proíbe
/// comparar funções (LAP0281), então uma chamada aqui só pode ser bug nosso.
/// </summary>
public sealed record ClosureValue(CoreLambda Lambda, Environment Captured, FunctionType Signature) : Value
{
    public override LapisType Type => Signature;

    public bool Equals(ClosureValue? other) =>
        throw new InternalCompilerException("closures não são comparáveis; o checker deveria ter rejeitado");

    public override int GetHashCode() =>
        throw new InternalCompilerException("closures não são comparáveis; o checker deveria ter rejeitado");
}

/// <summary>
/// Função implementada em C#. Existe apenas para o que não dá para escrever em
/// LapisLang: efeito de I/O e acesso à representação (plano 09 §9.2).
/// </summary>
public sealed record NativeFunctionValue(
    string Name,
    FunctionType Signature,
    Func<ImmutableArray<Value>, RuntimeContext, Value> Implementation) : Value
{
    public override LapisType Type => Signature;

    public bool Equals(NativeFunctionValue? other) => other is not null && Name == other.Name;

    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
}

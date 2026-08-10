using System.Collections.Immutable;
using Lapis.Diagnostics;

namespace Lapis.Ast.Types;

public enum TypeDefinitionKind
{
    Struct,
    Enum,
}

public sealed record VariantInfo(string Name, ImmutableArray<LapisType> Payload, SourceSpan Span);

public sealed record FieldInfo(string Name, LapisType Type, SourceSpan Span);

/// <summary>
/// Um <c>type</c> ou <c>enum</c> declarado pelo programa.
///
/// Tem identidade de referência de propósito: dois <c>enum { A }</c> escritos
/// separadamente são tipos distintos, mesmo tendo a mesma forma. O
/// <see cref="Id"/> existe apenas para hashing estável.
/// </summary>
public sealed class TypeDefinition
{
    private static int _nextId;

    public TypeDefinition(
        string name,
        TypeDefinitionKind kind,
        ImmutableArray<TypeParameterType> typeParameters,
        SourceSpan span)
    {
        Id = Interlocked.Increment(ref _nextId);
        Name = name;
        Kind = kind;
        TypeParameters = typeParameters;
        Span = span;
    }

    public int Id { get; }

    /// <summary>
    /// Nome do binding que recebeu a definição. Um <c>type</c> não tem nome próprio
    /// (spec §14); este é preenchido pelo checker a partir do <c>def</c> que o
    /// envolve, e serve só para diagnósticos e formatação.
    /// </summary>
    public string Name { get; set; }

    public TypeDefinitionKind Kind { get; }

    public ImmutableArray<TypeParameterType> TypeParameters { get; }

    public SourceSpan Span { get; }

    /// <summary>Campos, quando <see cref="Kind"/> é <c>Struct</c>.</summary>
    public ImmutableArray<FieldInfo> Fields { get; set; } = [];

    /// <summary>Variantes, quando <see cref="Kind"/> é <c>Enum</c>.</summary>
    public ImmutableArray<VariantInfo> Variants { get; set; } = [];

    public bool IsGeneric => !TypeParameters.IsDefaultOrEmpty;

    public int IndexOfVariant(string name)
    {
        for (var i = 0; i < Variants.Length; i++)
        {
            if (string.Equals(Variants[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    public int IndexOfField(string name)
    {
        for (var i = 0; i < Fields.Length; i++)
        {
            if (string.Equals(Fields[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    public override string ToString() => Name;
}

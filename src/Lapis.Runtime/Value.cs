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

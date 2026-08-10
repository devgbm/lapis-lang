using System.Globalization;
using Lapis.Ast.Types;

namespace Lapis.Ast;

/// <summary>
/// Valor conhecido em tempo de compilação: literais da Core AST, argumentos
/// const-genéricos e o ambiente estático do partial evaluator.
///
/// Puro dado — sem dependência do runtime, para que <c>Lapis.Ast</c> continue
/// não conhecendo valores de execução (spec §36).
/// </summary>
public abstract record ConstantValue
{
    public abstract LapisType Type { get; }

    public abstract string ToDisplayString();

    public sealed override string ToString() => ToDisplayString();
}

public sealed record ConstInt(long Value) : ConstantValue
{
    public override LapisType Type => PrimitiveType.Int;

    public override string ToDisplayString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record ConstFloat(double Value) : ConstantValue
{
    public override LapisType Type => PrimitiveType.Float;

    /// <summary>
    /// Sempre com ponto decimal: <c>1.0</c> nunca imprime como <c>1</c>, senão
    /// a saída de <c>lapis desugar</c> deixaria de ser re-parseável como Float.
    /// </summary>
    public override string ToDisplayString()
    {
        var text = Value.ToString("R", CultureInfo.InvariantCulture);

        return text.Contains('.') || text.Contains('E') || text.Contains("Inf") || text.Contains("NaN")
            ? text
            : text + ".0";
    }
}

public sealed record ConstBool(bool Value) : ConstantValue
{
    public static readonly ConstBool True = new(true);
    public static readonly ConstBool False = new(false);

    public override LapisType Type => PrimitiveType.Bool;

    public override string ToDisplayString() => Value ? "true" : "false";
}

public sealed record ConstStr(string Value) : ConstantValue
{
    public override LapisType Type => PrimitiveType.Str;

    public override string ToDisplayString() => "\"" + Escape(Value) + "\"";

    internal static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal)
        .Replace("\0", "\\0", StringComparison.Ordinal);
}

public sealed record ConstUnit : ConstantValue
{
    public static readonly ConstUnit Instance = new();

    public override LapisType Type => PrimitiveType.Void;

    public override string ToDisplayString() => "()";
}

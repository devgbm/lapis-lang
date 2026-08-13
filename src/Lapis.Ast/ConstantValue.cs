using System.Globalization;
using System.Text;
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
    /// Sempre com ponto decimal e <b>nunca</b> em notação científica: a gramática
    /// de float da 0.2 é <c>dígitos "." dígitos</c> (spec §6) e não tem expoente,
    /// então <c>1E+20</c> seria uma saída que a própria linguagem não reparseia —
    /// e o round-trip do <c>CoreSourcePrinter</c> é requisito, não conveniência
    /// (plano 02 §2.8).
    /// </summary>
    public override string ToDisplayString()
    {
        // "R" dá a forma mais curta que reconstrói o valor exatamente; expandir o
        // expoente depois é manipulação de texto e preserva essa exatidão.
        var text = Value.ToString("R", CultureInfo.InvariantCulture);

        if (text.Contains("Inf", StringComparison.Ordinal) || text.Contains("NaN", StringComparison.Ordinal))
        {
            return text;
        }

        if (text.Contains('E', StringComparison.Ordinal))
        {
            return Expand(text);
        }

        return text.Contains('.', StringComparison.Ordinal) ? text : text + ".0";
    }

    /// <summary><c>1E+20</c> → <c>100000000000000000000.0</c>.</summary>
    private static string Expand(string scientific)
    {
        var marker = scientific.IndexOf('E', StringComparison.Ordinal);
        var exponent = int.Parse(scientific[(marker + 1)..], CultureInfo.InvariantCulture);
        var mantissa = scientific[..marker];

        var negative = mantissa.StartsWith('-');

        if (negative)
        {
            mantissa = mantissa[1..];
        }

        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        var digits = dot < 0 ? mantissa : mantissa.Remove(dot, 1);

        // Onde o ponto cai depois de aplicar o expoente.
        var point = (dot < 0 ? mantissa.Length : dot) + exponent;

        var expanded = point <= 0
            ? "0." + new string('0', -point) + digits
            : point >= digits.Length
                ? digits + new string('0', point - digits.Length) + ".0"
                : digits[..point] + "." + digits[point..];

        return negative ? "-" + expanded : expanded;
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

/// <summary>
/// Literal de caractere: <c>'a'</c> (Q35).
///
/// Um <see cref="Rune"/>, e não um <c>char</c> de C#: a unidade da linguagem é o
/// ponto de código, e um <c>char</c> não comporta os que estão fora do plano
/// básico.
/// </summary>
public sealed record ConstChar(Rune Value) : ConstantValue
{
    /// <summary>
    /// O texto entre aspas simples é um <c>Char</c>? Vive aqui porque a lexer
    /// entrega o conteúdo sem julgá-lo (Q35/Q38), e são dois os interessados: o
    /// parser, que reporta <c>LAP0117</c> em posição de expressão, e o matcher
    /// de macro, para quem "não é um caractere" significa apenas que o padrão
    /// não casa.
    /// </summary>
    public static bool TrySingleCodepoint(string text, out Rune rune)
    {
        var runes = text.EnumerateRunes();

        if (runes.MoveNext())
        {
            rune = runes.Current;

            if (!runes.MoveNext())
            {
                return true;
            }
        }

        rune = default;
        return false;
    }

    public override LapisType Type => PrimitiveType.Char;

    /// <summary>
    /// As mesmas regras de <see cref="ConstStr"/> com a aspa trocada: é <c>'</c>
    /// que leva barra aqui, e <c>"</c> que dispensa — cada aspa só se escapa
    /// dentro do literal que ela delimita. O round-trip do
    /// <c>CoreSourcePrinter</c> depende de isto reparsear.
    /// </summary>
    public override string ToDisplayString() => "'" + Value.Value switch
    {
        '\\' => "\\\\",
        '\'' => "\\'",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        '\0' => "\\0",
        _ => Value.ToString(),
    } + "'";
}

public sealed record ConstUnit : ConstantValue
{
    public static readonly ConstUnit Instance = new();

    public override LapisType Type => PrimitiveType.Void;

    public override string ToDisplayString() => "()";
}

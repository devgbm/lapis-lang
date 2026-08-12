namespace Lapis.Ast;

public enum UnaryOperator
{
    Negate,
    Not,
}

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,

    Equal,
    NotEqual,
    Less,
    Greater,
    LessOrEqual,
    GreaterOrEqual,

    /// <summary>Extensão Q4. Desaparece no desugar, virando <c>If</c>.</summary>
    AndAlso,

    /// <summary>Extensão Q4. Desaparece no desugar, virando <c>If</c>.</summary>
    OrElse,
}

public static class OperatorExtensions
{
    public static string Symbol(this UnaryOperator op) => op switch
    {
        UnaryOperator.Negate => "-",
        UnaryOperator.Not => "!",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    public static string Symbol(this BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.Less => "<",
        BinaryOperator.Greater => ">",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.GreaterOrEqual => ">=",
        BinaryOperator.AndAlso => "&&",
        BinaryOperator.OrElse => "||",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    public static bool IsComparison(this BinaryOperator op) =>
        op is BinaryOperator.Less or BinaryOperator.Greater
           or BinaryOperator.LessOrEqual or BinaryOperator.GreaterOrEqual;

    public static bool IsEquality(this BinaryOperator op) =>
        op is BinaryOperator.Equal or BinaryOperator.NotEqual;

    /// <summary>
    /// Precedência do plano 04 §4.2, com o degrau do <c>is</c> (plano 25 §25.7)
    /// aberto entre <c>&amp;&amp;</c> e <c>==</c>/<c>!=</c> — <c>is</c> não é um
    /// <see cref="BinaryOperator"/> (produz <c>IsExpression</c>, não
    /// <c>BinaryExpression</c>), então o valor 3 não aparece neste enum; é o
    /// <c>Parser</c> que o usa (<c>IsPrecedence</c>) para intercalar corretamente.
    /// Maior liga mais forte.
    /// </summary>
    public static int Precedence(this BinaryOperator op) => op switch
    {
        BinaryOperator.OrElse => 1,
        BinaryOperator.AndAlso => 2,
        // 3 = 'is' (fora deste enum; ver Parser.IsPrecedence)
        BinaryOperator.Equal or BinaryOperator.NotEqual => 4,
        BinaryOperator.Less or BinaryOperator.Greater
            or BinaryOperator.LessOrEqual or BinaryOperator.GreaterOrEqual => 5,
        BinaryOperator.Add or BinaryOperator.Subtract => 6,
        BinaryOperator.Multiply or BinaryOperator.Divide => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };
}

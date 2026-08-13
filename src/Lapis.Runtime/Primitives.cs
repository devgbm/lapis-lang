using System.Text;
using Lapis.Ast;
using Lapis.Diagnostics;

namespace Lapis.Runtime;

/// <summary>Resultado de um acesso a array, antes de virar um <c>Result</c> da linguagem.</summary>
public readonly record struct IndexOutcome(Value? Value, bool IsInBounds)
{
    public static IndexOutcome InBounds(Value value) => new(value, true);

    public static readonly IndexOutcome OutOfBounds = new(null, false);
}

/// <summary>
/// As operações fundamentais do runtime (spec §29).
///
/// Todas assumem que o type checker já validou os operandos: um tipo inesperado
/// é <see cref="InternalCompilerException"/>, nunca diagnóstico.
///
/// Todas são <b>totais</b>: nenhuma operação aritmética falha (Q9), o que mantém
/// o tipo de <c>/</c> simples e torna toda a aritmética dobrável pelo partial
/// evaluator sem análise de efeito.
/// </summary>
public static class Primitives
{
    public static Value Apply(BinaryOperator op, Value left, Value right) => op switch
    {
        BinaryOperator.Add => Add(left, right),
        BinaryOperator.Subtract => Subtract(left, right),
        BinaryOperator.Multiply => Multiply(left, right),
        BinaryOperator.Divide => Divide(left, right),
        BinaryOperator.Equal => BoolValue.Of(StructuralEquals(left, right)),
        BinaryOperator.NotEqual => BoolValue.Of(!StructuralEquals(left, right)),
        _ when op.IsComparison() => Compare(op, left, right),
        _ => throw new InternalCompilerException($"operador binário inesperado no runtime: {op}"),
    };

    public static Value Add(Value left, Value right) => (left, right) switch
    {
        (IntValue a, IntValue b) => new IntValue(unchecked(a.Value + b.Value)),
        (FloatValue a, FloatValue b) => new FloatValue(a.Value + b.Value),
        (StrValue a, StrValue b) => new StrValue(a.Value + b.Value),
        _ => throw Mismatch("+", left, right),
    };

    public static Value Subtract(Value left, Value right) => (left, right) switch
    {
        (IntValue a, IntValue b) => new IntValue(unchecked(a.Value - b.Value)),
        (FloatValue a, FloatValue b) => new FloatValue(a.Value - b.Value),
        _ => throw Mismatch("-", left, right),
    };

    public static Value Multiply(Value left, Value right) => (left, right) switch
    {
        (IntValue a, IntValue b) => new IntValue(unchecked(a.Value * b.Value)),
        (FloatValue a, FloatValue b) => new FloatValue(a.Value * b.Value),
        _ => throw Mismatch("*", left, right),
    };

    /// <summary>
    /// Divisão inteira por zero produz o maior <c>Int</c> (Q9), qualquer que seja o
    /// sinal do dividendo — regra única, sem casos especiais a memorizar. Com isso
    /// <c>/</c> é total e seu tipo continua sendo <c>Int</c>, sem contaminar toda
    /// expressão aritmética com <c>Result</c>.
    ///
    /// <c>Float</c> segue IEEE 754 e produz infinito ou NaN.
    /// </summary>
    public static Value Divide(Value left, Value right) => (left, right) switch
    {
        (IntValue, IntValue { Value: 0 }) => new IntValue(long.MaxValue),

        // `MinValue / -1` é a única divisão inteira que estoura: o quociente não
        // cabe em Int64 e o hardware trapeia, mesmo em `unchecked`. Envolve como o
        // resto da aritmética (`-MinValue == MinValue` em complemento de dois).
        (IntValue { Value: long.MinValue }, IntValue { Value: -1 }) => new IntValue(long.MinValue),

        (IntValue a, IntValue b) => new IntValue(a.Value / b.Value),
        (FloatValue a, FloatValue b) => new FloatValue(a.Value / b.Value),
        _ => throw Mismatch("/", left, right),
    };

    public static Value Negate(Value value) => value switch
    {
        IntValue v => new IntValue(unchecked(-v.Value)),
        FloatValue v => new FloatValue(-v.Value),
        _ => throw new InternalCompilerException($"'-' não se aplica a {value.Type.ToDisplayString()}"),
    };

    public static Value Not(Value value) => value switch
    {
        BoolValue v => BoolValue.Of(!v.Value),
        _ => throw new InternalCompilerException($"'!' não se aplica a {value.Type.ToDisplayString()}"),
    };

    public static BoolValue Compare(BinaryOperator op, Value left, Value right)
    {
        var order = (left, right) switch
        {
            (IntValue a, IntValue b) => a.Value.CompareTo(b.Value),
            (FloatValue a, FloatValue b) => a.Value.CompareTo(b.Value),

            // Comparação ordinal, não linguística: não pode depender de cultura.
            (StrValue a, StrValue b) => string.CompareOrdinal(a.Value, b.Value),

            _ => throw Mismatch(op.Symbol(), left, right),
        };

        return BoolValue.Of(op switch
        {
            BinaryOperator.Less => order < 0,
            BinaryOperator.Greater => order > 0,
            BinaryOperator.LessOrEqual => order <= 0,
            BinaryOperator.GreaterOrEqual => order >= 0,
            _ => throw new InternalCompilerException($"operador de comparação inesperado: {op}"),
        });
    }

    /// <summary>
    /// Igualdade estrutural. Closures nunca chegam aqui: o checker rejeita
    /// comparar funções (LAP0281).
    /// </summary>
    public static bool StructuralEquals(Value left, Value right)
    {
        if (left is ClosureValue || right is ClosureValue)
        {
            throw new InternalCompilerException("closures não são comparáveis; o checker deveria ter rejeitado");
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Acesso a array com checagem de limites (spec §41). O runtime não conhece
    /// <c>Result</c>: quem converte este resultado em <c>Result.Ok</c> /
    /// <c>Result.Err</c> é o evaluator, usando as definições do prelude (spec §16).
    /// </summary>
    public static IndexOutcome SpanGet(SpanValue array, long index) =>
        index >= 0 && index < array.Elements.Length
            ? IndexOutcome.InBounds(array.Elements[(int)index])
            : IndexOutcome.OutOfBounds;

    /// <summary>
    /// Quantos pontos de código o texto tem (Q35/A2a).
    ///
    /// Linear por construção: a <c>string</c> de C# é UTF-16 e um ponto de
    /// código fora do plano básico ocupa duas unidades, então não há como
    /// responder em O(1) sem mudar a representação — que é justamente o que A2b
    /// faria, e o que A2a decidiu não fazer agora.
    ///
    /// Vive aqui, e não no evaluator, porque o partial evaluator dobra
    /// <c>s.length</c> com a mesma contagem: se as duas divergissem, o residual
    /// deixaria de significar o mesmo que o original.
    /// </summary>
    public static int StrLength(string text)
    {
        var count = 0;

        foreach (var _ in text.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// O ponto de código na posição dada, ou <c>null</c> fora dos limites — o
    /// <c>null</c> é o que o evaluator vira <c>Option.None</c>.
    /// </summary>
    public static Rune? StrAt(string text, long position)
    {
        if (position < 0)
        {
            return null;
        }

        var seen = 0L;

        foreach (var rune in text.EnumerateRunes())
        {
            if (seen == position)
            {
                return rune;
            }

            seen++;
        }

        return null;
    }

    private static InternalCompilerException Mismatch(string op, Value left, Value right) =>
        new($"'{op}' não se aplica a {left.Type.ToDisplayString()} e {right.Type.ToDisplayString()}");
}

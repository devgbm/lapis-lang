using Lapis.Ast;
using Lapis.Diagnostics;

namespace Lapis.Runtime;

/// <summary>Resultado de uma operação aritmética que pode falhar.</summary>
public readonly record struct ArithmeticOutcome(Value? Value, bool DivisionByZero)
{
    public static ArithmeticOutcome Ok(Value value) => new(value, false);

    public static readonly ArithmeticOutcome DividedByZero = new(null, true);

    public Value Unwrap() => Value ?? throw new InternalCompilerException("operação aritmética falhou");
}

/// <summary>
/// As operações fundamentais do runtime (spec §29).
///
/// Todas assumem que o type checker já validou os operandos: um tipo inesperado
/// é <see cref="InternalCompilerException"/>, nunca diagnóstico.
/// </summary>
public static class Primitives
{
    public static ArithmeticOutcome Apply(BinaryOperator op, Value left, Value right) => op switch
    {
        BinaryOperator.Add => ArithmeticOutcome.Ok(Add(left, right)),
        BinaryOperator.Subtract => ArithmeticOutcome.Ok(Subtract(left, right)),
        BinaryOperator.Multiply => ArithmeticOutcome.Ok(Multiply(left, right)),
        BinaryOperator.Divide => Divide(left, right),
        BinaryOperator.Equal => ArithmeticOutcome.Ok(BoolValue.Of(StructuralEquals(left, right))),
        BinaryOperator.NotEqual => ArithmeticOutcome.Ok(BoolValue.Of(!StructuralEquals(left, right))),
        _ when op.IsComparison() => ArithmeticOutcome.Ok(Compare(op, left, right)),
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
    /// Divisão inteira por zero aborta a execução (Q9); <c>Float</c> segue IEEE 754
    /// e produz infinito ou NaN sem abortar.
    /// </summary>
    public static ArithmeticOutcome Divide(Value left, Value right) => (left, right) switch
    {
        (IntValue, IntValue { Value: 0 }) => ArithmeticOutcome.DividedByZero,
        (IntValue a, IntValue b) => ArithmeticOutcome.Ok(new IntValue(unchecked(a.Value / b.Value))),
        (FloatValue a, FloatValue b) => ArithmeticOutcome.Ok(new FloatValue(a.Value / b.Value)),
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

    private static InternalCompilerException Mismatch(string op, Value left, Value right) =>
        new($"'{op}' não se aplica a {left.Type.ToDisplayString()} e {right.Type.ToDisplayString()}");
}

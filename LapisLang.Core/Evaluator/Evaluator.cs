

namespace LapisLang.Core;

public record EvaluationResult(object? value, DiagnosticsBag Diagnostics);
public class Evaluator
{
    public DiagnosticsBag Diagnostics { get; }

    public Evaluator()
    {
        Diagnostics = new DiagnosticsBag();
    }
    public object? Evaluate(BoundSyntax syntax)
    {
        switch (syntax)
        {
            case BoundExpression be: return EvaluateExpression(be);
            default: return null;
        }
    }

    public object? EvaluateExpression(BoundExpression expression)
    {
        switch (expression)
        {
            case BoundLiteralExpression ble:
                return EvaluateLiteral(ble);

            case BoundBinaryExpression bbe:
                return EvaluateBinaryExpression(bbe);

            case BoundUnaryExpression bue:
                return EvaluateUnaryExpression(bue);
            default: return null;
        }
    }

    private object? EvaluateUnaryExpression(BoundUnaryExpression bue)
    {
        var expression = Evaluate(bue.Expression);
        Func<object?, object?> oper = bue.UnaryOperator switch {
            UnaryOperator.Identity => (object? obj) => obj,
            UnaryOperator.Negation => (object? obj) => !(bool)obj,
            UnaryOperator.Inverse => (object? obj) => -(long)obj,
            UnaryOperator.Unkown => throw new Exception("unable to evaluate unary expression")
        };

        return oper(expression);
    }

    private object? EvaluateBinaryExpression(BoundBinaryExpression bbe)
    {
        var left = Evaluate(bbe.Left);
        var right = Evaluate(bbe.Right);
        Func<object?, object?, object?> oper = bbe.BinaryOperator switch
        {
            BinaryOperator.Add => (object? left, object? right) => (long)left + (long)right,
            BinaryOperator.Sub => (object? left, object? right) => (long)left - (long)right,
            BinaryOperator.Mul => (object? left, object? right) => (long)left * (long)right,
            BinaryOperator.Div => (object? left, object? right) => (long)left / (long)right,
            BinaryOperator.Mod => (object? left, object? right) => (long)left % (long)right,

            BinaryOperator.LogicAnd => (object? left, object? right) => (bool)left && (bool)right,
            BinaryOperator.LogicOr => (object? left, object? right) => (bool)left || (bool)right,

            BinaryOperator.Equality => (object? left, object? right) => (long)left == (long)right,
            BinaryOperator.Inequality => (object? left, object? right) => (long)left != (long)right,
            BinaryOperator.Greather => (object? left, object? right) => (long)left > (long)right,
            BinaryOperator.Less => (object? left, object? right) => (long)left < (long)right,
            BinaryOperator.GreatherEquals => (object? left, object? right) => (long)left >= (long)right,
            BinaryOperator.LessEquals => (object? left, object? right) => (long)left <= (long)right,

            _ => throw new Exception("unable to evaluate expression")
        };

        return oper(left, right);

    }

    private object? EvaluateLiteral(BoundLiteralExpression ble)
    {
        return ble.Value;
    }
}
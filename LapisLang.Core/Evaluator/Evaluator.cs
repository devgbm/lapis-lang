

namespace LapisLang.Core;
public class Evaluator
{
    public DiagnosticsBag Diagnostics { get; }

    public Evaluator()
    {
        Diagnostics = new DiagnosticsBag();
    }
    public object? Evaluate(BoundSyntax syntax, EvaluationContext context)
    {
        switch (syntax)
        {
            case BoundExpression be: return EvaluateExpression(be, context);
            default: return null;
        }
    }

    public object? EvaluateExpression(BoundExpression expression, EvaluationContext context)
    {
        switch (expression)
        {
            case BoundLiteralExpression ble:
                return EvaluateLiteral(ble, context);

            case BoundBinaryExpression bbe:
                return EvaluateBinaryExpression(bbe, context);

            case BoundUnaryExpression bue:
                return EvaluateUnaryExpression(bue, context);
            
            case BoundNameExpression bne:
                return EvaluateNameExpression(bne, context);
            default: return null;
        }
    }

    private object? EvaluateNameExpression(BoundNameExpression bne, EvaluationContext context)
    {
        if (bne.Type.Kind == RuneKind.Type)
        {
            var rune = context.ResolveName(bne.Name);
            return new RuneReference(rune);
        }
        throw new NotImplementedException();
    }

    private object? EvaluateUnaryExpression(BoundUnaryExpression bue, EvaluationContext context)
    {
        var expression = Evaluate(bue.Expression, context);
        Func<object?, object?> oper = bue.UnaryOperator switch {
            UnaryOperator.Identity => (object? obj) => obj,
            UnaryOperator.Negation => (object? obj) => !(bool)obj!,
            UnaryOperator.Inverse => (object? obj) => -(long)obj!,
            _=> throw new Exception("unable to evaluate unary expression")
        };

        return oper(expression);
    }

    private object? EvaluateBinaryExpression(BoundBinaryExpression bbe, EvaluationContext context)
    {
        var left = Evaluate(bbe.Left, context);
        var right = Evaluate(bbe.Right, context);
        Func<object?, object?, object?> oper = bbe.BinaryOperator switch
        {
            BinaryOperator.Add => (object? left, object? right) => (long)left! + (long)right!,
            BinaryOperator.Sub => (object? left, object? right) => (long)left! - (long)right!,
            BinaryOperator.Mul => (object? left, object? right) => (long)left! * (long)right!,
            BinaryOperator.Div => (object? left, object? right) => (long)left! / (long)right!,
            BinaryOperator.Mod => (object? left, object? right) => (long)left! % (long)right!,

            BinaryOperator.LogicAnd => (object? left, object? right) => (bool)left! && (bool)right!,
            BinaryOperator.LogicOr => (object? left, object? right) => (bool)left! || (bool)right!,

            BinaryOperator.Equality => (object? left, object? right) => (long)left! == (long)right!,
            BinaryOperator.Inequality => (object? left, object? right) => (long)left! != (long)right!,
            BinaryOperator.Greather => (object? left, object? right) => (long)left! > (long)right!,
            BinaryOperator.Less => (object? left, object? right) => (long)left! < (long)right!,
            BinaryOperator.GreatherEquals => (object? left, object? right) => (long)left! >= (long)right!,
            BinaryOperator.LessEquals => (object? left, object? right) => (long)left! <= (long)right!,

            _ => throw new Exception("unable to evaluate expression")
        };

        return oper(left, right);

    }

    private object? EvaluateLiteral(BoundLiteralExpression ble, EvaluationContext context)
    {
        return ble.Value;
    }
}
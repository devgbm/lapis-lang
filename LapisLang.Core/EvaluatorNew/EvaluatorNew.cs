

namespace LapisLang.Core;


public class EvaluatorNew
{
    private readonly DiagnosticsBag _diagnostics = new();
    public EvaluationResult Evaluate(Symbol symbol, ScopeSymbol scope)
    {
        object? value = null;
        switch (symbol)
        {
            case ExpressionSymbol es:
                value = EvaluateExpressionSymbol(es, scope);
                break;
        }

        return new EvaluationResult(value, _diagnostics);
    }

    private object? EvaluateExpressionSymbol(ExpressionSymbol es, ScopeSymbol scope)
    {
        switch (es)
        {
            case ValueSymbol vs: return EvaluateValueSymbol(vs);
            case BinaryExpressionSymbol bss: return EvaluateBinarySymbol(bss, scope);
        }
        return null;
    }

    private object? EvaluateBinarySymbol(BinaryExpressionSymbol bss, ScopeSymbol scope)
    {
        var left = EvaluateExpressionSymbol(bss.Left, scope);
        var right = EvaluateExpressionSymbol(bss.Right, scope);

        Func<object?, object?, object?> op;
        if (bss.Left.Type == DefaultSymbols.Types.Integer)
        {
            op = bss.BinaryOp switch
            {
                BinaryOperatorKind.Add => (object? left, object? right) => (long)left + (long)right,
                BinaryOperatorKind.Sub => (object? left, object? right) => (long)left - (long)right,
                BinaryOperatorKind.Mul => (object? left, object? right) => (long)left * (long)right,
                BinaryOperatorKind.Div => (object? left, object? right) => (long)left / (long)right,
                BinaryOperatorKind.Mod => (object? left, object? right) => (long)left % (long)right,
                BinaryOperatorKind.Equality => (object? left, object? right) => (long)left == (long)right,
                BinaryOperatorKind.Inequality => (object? left, object? right) => (long)left != (long)right,
                BinaryOperatorKind.GreatherThan => (object? left, object? right) => (long)left > (long)right,
                BinaryOperatorKind.GreatherOrEqual => (object? left, object? right) => (long)left >= (long)right,
                BinaryOperatorKind.LessThan => (object? left, object? right) => (long)left < (long)right,
                BinaryOperatorKind.LessOrEqual => (object? left, object? right) => (long)left <= (long)right,
                _ => (object? left, object? right) => 0
            };
        }
        else
        {
            op = bss.BinaryOp switch
            {
                BinaryOperatorKind.LogicOr => (object? left, object? right) => (bool)left || (bool)right,
                BinaryOperatorKind.LogicAnd => (object? left, object? right) => (bool)left && (bool)right,
                BinaryOperatorKind.Equality => (object? left, object? right) => (bool)left == (bool)right,
                BinaryOperatorKind.Inequality => (object? left, object? right) => (bool)left != (bool)right,
                _ => (object? left, object? right) => 0
            };
        }


        return op(left, right);
    }

    private object? EvaluateValueSymbol(ValueSymbol vs)
    {
        return vs.Value;
    }
}
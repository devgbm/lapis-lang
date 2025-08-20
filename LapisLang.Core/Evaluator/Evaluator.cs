



namespace LapisLang.Core;


public class LapisEvaluator
{
    private static LapisEvaluator? _instnce;
    public static LapisEvaluator Instance
    {
        get
        {
            if (_instnce is null) _instnce = new();
            return _instnce;
        }
    }
    private LapisEvaluator()
    {
    }

    public Symbol Evaluate(Symbol symbol, EvaluationScope? scope = null)
    {
        var evaluationScope = scope ?? EvaluationScope.CreateScope();
        switch (symbol)
        {
            case ExprSymbol es: return EvaluateExpression(es, evaluationScope);
            case StatementSymbol ss: return EvaluateStatement(ss, evaluationScope);
            default: return Symbol.Unkown;
        }
    }

    private Symbol EvaluateStatement(StatementSymbol syntax, EvaluationScope evaluationScope)
    {
        switch (syntax)
        {
            case DefineSymbol ds: return EvaluateDefineSymbol(ds, evaluationScope);
            default:
                throw new Exception("Unkown or unsuported statement.");
        }
        
    }

    private Symbol EvaluateDefineSymbol(DefineSymbol ds, EvaluationScope evaluationScope)
    {
        if (ds.Expression.IsConstant) return VoidSymbol.Instance;

        return VoidSymbol.Instance;
    }

    private ExprSymbol EvaluateExpression(ExprSymbol expr, EvaluationScope scope)
    {
        switch (expr)
        {
            case IntegerSymbol:
            case BooleanSymbol:
            case DecimalSymbol:
            case StringSymbol:
            case TypeSymbol:
            case InstanceSymbol:
                return expr;

            case NameSymbol ns:
                return EvaluateNameSymbol(ns, scope);
            case BinaryExprSymbol bes:
                return EvaluateBinaryExpr(bes, scope);

            default: return ExprSymbol.Unkown;
        }
    }

    private ExprSymbol EvaluateNameSymbol(NameSymbol ns, EvaluationScope scope)
    {
        scope.BoundScope.TryGetSymbol(ns.Name, out var symbol);
        return symbol as ExprSymbol ?? ExprSymbol.Unkown;
    }

    private ExprSymbol EvaluateBinaryExpr(BinaryExprSymbol bes, EvaluationScope scope)
    {
        var left = EvaluateExpression(bes.Left, scope);
        var right = EvaluateExpression(bes.Right, scope);

        var operatorFn = scope.GetOperatorFn(left.Type, right.Type, bes.BinaryOperator);
        return operatorFn(left, right);
    }
}
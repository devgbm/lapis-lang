
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
        }
        return null;
    }

    private object? EvaluateValueSymbol(ValueSymbol vs)
    {
        return vs.Value;
    }
}
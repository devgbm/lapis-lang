
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
            default: return null;
        }
    }

    private object? EvaluateLiteral(BoundLiteralExpression ble)
    {
        return ble.Value;
    }
}
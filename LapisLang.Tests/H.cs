using LapisLang.Core;

namespace LapisLang.Tests;

internal static class H
{
    public static ExprSymbol Eval(string code, EvaluationScope? scope = null)
    {
        var result = LapisInterpreter.Evaluate(code, scope);
        Assert.False(result.Diagnostics.HasErrors, $"Unexpected errors for: {code}");
        return Assert.IsAssignableFrom<ExprSymbol>(result.result);
    }

    public static T EvalAs<T>(string code, EvaluationScope? scope = null) where T : ExprSymbol
        => Assert.IsType<T>(Eval(code, scope));

    public static bool HasErrors(string code, EvaluationScope? scope = null)
    {
        try { return LapisInterpreter.Evaluate(code, scope).Diagnostics.HasErrors; }
        catch { return true; }
    }
}

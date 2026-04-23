namespace LapisLang.Core;


public record EvaluationResult(Symbol result, DiagnosticsBag Diagnostics);

public class LapisInterpreter
{
    public static EvaluationResult Evaluate(string code, EvaluationScope? scope = null)
    {
        var lexer = new Lexer(code);
        var tokenStream = lexer.TokenStream();

        var parser = new LapisParser(tokenStream);
        var syntaxResult = parser.Parse();
        if (parser.Diagnostics.HasErrors)
        {
            return new EvaluationResult(Symbol.Unkown, parser.Diagnostics);
        }

        var binder = new Binder();
        var evaluationScope = scope ?? new EvaluationScope();
        var boundSyntax = binder.Bind(syntaxResult, evaluationScope.BoundScope);

        if (binder.Diagnostics.HasErrors)
        {
            return new EvaluationResult(Symbol.Unkown, binder.Diagnostics);
        }

        var evaluator = LapisEvaluator.Instance.Evaluate(boundSyntax, evaluationScope);
        return new EvaluationResult(evaluator, new DiagnosticsBag());
    }

    public static EvaluationResult EvaluateScript(string code, EvaluationScope? scope = null)
    {
        var lexer = new Lexer(code);
        var tokenStream = lexer.TokenStream();

        var parser = new LapisParser(tokenStream);
        var syntaxResult = parser.ParseProgram();
        if (parser.Diagnostics.HasErrors)
        {
            return new EvaluationResult(Symbol.Unkown, parser.Diagnostics);
        }

        var binder = new Binder();
        var evaluationScope = scope ?? new EvaluationScope();
        var boundSyntax = binder.Bind(syntaxResult, evaluationScope.BoundScope);

        if (binder.Diagnostics.HasErrors)
        {
            return new EvaluationResult(Symbol.Unkown, binder.Diagnostics);
        }

        var evaluator = LapisEvaluator.Instance.Evaluate(boundSyntax, evaluationScope);
        return new EvaluationResult(evaluator, new DiagnosticsBag());
    }
}
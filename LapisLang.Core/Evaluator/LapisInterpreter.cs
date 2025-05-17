namespace LapisLang.Core;


public class LapisInterpreter
{
    public static EvaluationResult Evaluate(string source, EvaluationContext? context = null)
    {
        var evaluationContext = context ?? new EvaluationContext(LangDefaults.RootNamespace());
        var lexer = new Lexer(source);
        var stream = lexer.TokenStream();
        var parser = new StatementParser(stream);
        var expression = parser.ParseStatement();
        var binder = new Binder();
        var bindingContext = new BindingContext(evaluationContext.NamespaceRune);
        var bound = binder.Bind(expression, bindingContext);
        var evaluator = new Evaluator();
        var result = evaluator.Evaluate(bound.BoundSyntax, evaluationContext);
        return new EvaluationResult(result, evaluator.Diagnostics);
    }
}
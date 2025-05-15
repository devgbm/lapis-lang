namespace LapisLang.Core;


public class LapisInterpreter
{
    public static EvaluationResult Evaluate(string source)
    {
        var lexer = new Lexer(source);
        var stream = lexer.TokenStream();
        var parser = new ExpressionParser(stream);
        var expression = parser.ParseExpression();
        var binder = new Binder();
        var bindingContext = new BindingContext();
        var bound = binder.Bind(expression, bindingContext);
        var evaluator = new Evaluator();
        var result = evaluator.Evaluate(bound.BoundSyntax);
        return new EvaluationResult(result, evaluator.Diagnostics);
    }
}
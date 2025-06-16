namespace LapisLang.Core;


public class LapisInterpreter
{
    public static EvaluationResult Evaluate(string source, ScopeSymbol? scope = null)
    {
        var lexer = new Lexer(source);
        var stream = lexer.TokenStream();
        var parser = new LapisParser(stream);
        var syntax = parser.Parse();
        var binder = new BinderNew();
        var bindScope = scope ?? DefaultSymbols.CreateDefaultScope();
        var symbol = binder.Bind(syntax, bindScope);
        var evaluator = new EvaluatorNew();
        return evaluator.Evaluate(symbol, bindScope);
    }
}
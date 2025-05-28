



namespace LapisLang.Core;

public class BinderNew
{
    public DiagnosticsBag Diagnostics { get; }

    public BinderNew()
    {
        Diagnostics = new DiagnosticsBag();
    }
    public Symbol Bind(Syntax syntax, ScopeSymbol? bindingContext = null)
    {
        var context = bindingContext ?? CreateDefaultScope();
        if(typeof(ExpressionSyntax).IsAssignableFrom(syntax.GetType()))
        switch (syntax)
        {
            case ExpressionSyntax es:
                return BindExpression(es, context);
        }
        return Symbol.Unkown;
    }

    public static ScopeSymbol CreateDefaultScope()
    {
        var root = new ScopeSymbol();

        return root;
    }

    private ExpressionSymbol BindExpression(ExpressionSyntax es, ScopeSymbol context)
    {
        switch (es)
        {
            case LiteralExpressionSyntax les: return BindLiteralExpression(les, context);
            case BinaryExpressionSyntax bes: return BindBinaryExpression(bes, context);
            case UnaryExpressionSyntax ues: return BindUnaryExpression(ues, context);
            default: return ExpressionSymbol.Unknown;
        }
    }

    private ExpressionSymbol BindUnaryExpression(UnaryExpressionSyntax ues, ScopeSymbol context)
    {
        var operand = BindExpression(ues.Expression, context);
        var oper = operand.Type.GetUnaryOperatorFor(ues.TokenOperator.Kind);

        return new UnaryExpressionSymbol(operand, oper.Kind, oper.Result);
    }

    private BinaryExpressionSymbol BindBinaryExpression(BinaryExpressionSyntax bes, ScopeSymbol context)
    {
        var left = BindExpression(bes.Left, context);
        var right = BindExpression(bes.Right, context);
        var oper = left.Type.GetBinaryOperatorFor(bes.OperatorToken.Kind);

        if (oper.With != right.Type)
        {
            Diagnostics.Report("Operator type mismatch", bes.OperatorToken);
        }

        return new BinaryExpressionSymbol(left, right, oper.Kind, oper.Result);
    }

    private ExpressionSymbol BindLiteralExpression(LiteralExpressionSyntax es, ScopeSymbol context)
    {
        switch (es.LiteralType)
        {
            case LiteralType.Integer:
            {
                var value = long.Parse(es.SourceSpan.AsText);
                return new ValueSymbol(value, DefaultSymbols.Types.Integer);
            }
            case LiteralType.String:
            {
                var value = new String(es.SourceSpan.AsText);
                return new ValueSymbol(value, DefaultSymbols.Types.String);
            }
            case LiteralType.Decimal:
            {
                var value = decimal.Parse(es.SourceSpan.AsText);
                return new ValueSymbol(value, DefaultSymbols.Types.Decimal);
            }
            case LiteralType.Boolean:
            {
                var value = bool.Parse(es.SourceSpan.AsText);
                return new ValueSymbol(value, DefaultSymbols.Types.Boolean);
            }
            default: return ExpressionSymbol.Unknown;
        }
    }
}

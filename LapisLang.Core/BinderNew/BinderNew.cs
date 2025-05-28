



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
        if(typeof(ExpressionSyntax).IsAssignableFrom(syntax.GetType())) return BindExpression(((ExpressionSyntax)syntax), context);
        if(typeof(StatementSyntax).IsAssignableFrom(syntax.GetType())) return BindStatement(((StatementSyntax)syntax), context);

        return Symbol.Unkown;
    }

    private StatementSymbol BindStatement(StatementSyntax ss, ScopeSymbol context)
    {
        switch (ss)
        {
            case VariableDeclarationSyntax vds: return BindVariableDeclaration(vds, context);
            default: return StatementSymbol.Unknown;
        }
    }

    private StatementSymbol BindVariableDeclaration(VariableDeclarationSyntax vds, ScopeSymbol context)
    {
        var expression = BindExpression(vds.Expression, context);
        if (!context.GetSymbol(vds.TypeName.Identifier.String, out var type))
        {
            Diagnostics.Report("type could not be found", vds.TypeName);
            type = DefaultSymbols.Types.Unkown;
        }

        if (type is not TypeSymbol ts)
        {
            Diagnostics.Report("symbol is not a type", vds.TypeName);
            ts = DefaultSymbols.Types.Unkown;
        }

        var name = vds.Identifier.String;
        context.DefineSymbol(name, expression);

        return new VariableDeclarationSymbol(name, expression, ts);
    }

    public static ScopeSymbol CreateDefaultScope()
    {
        var root = new ScopeSymbol();

        root.DefineSymbol("@integer", DefaultSymbols.Types.Integer);
        root.DefineSymbol("@boolean", DefaultSymbols.Types.Boolean);
        root.DefineSymbol("@decimal", DefaultSymbols.Types.Decimal);
        root.DefineSymbol("@string", DefaultSymbols.Types.String);
        root.DefineSymbol("@type", DefaultSymbols.Types.Type);
        root.DefineSymbol("@scope", DefaultSymbols.Types.Scope);
        root.DefineSymbol("@unkown", DefaultSymbols.Types.Unkown);
        root.DefineSymbol("@", root);

        return root;
    }

    private ExpressionSymbol BindExpression(ExpressionSyntax es, ScopeSymbol context)
    {
        switch (es)
        {
            case LiteralExpressionSyntax les: return BindLiteralExpression(les, context);
            case BinaryExpressionSyntax bes: return BindBinaryExpression(bes, context);
            case UnaryExpressionSyntax ues: return BindUnaryExpression(ues, context);
            case ParenthesizedExpression pe: return BindExpression(pe.Expression, context);
            case NameExpressionSyntax ne: return BindNameExpression(ne, context);
            default: return ExpressionSymbol.Unknown;
        }
    }

    private ExpressionSymbol BindNameExpression(NameExpressionSyntax ne, ScopeSymbol context)
    {
        if (!context.GetSymbol(ne.Name.String, out var symbol))
        {
            Diagnostics.Report("unkown symbol", ne);
            return ExpressionSymbol.Unknown;
        }

        var type = symbol switch
        {
            ExpressionSymbol es => es.Type,
            TypeSymbol => DefaultSymbols.Types.Type,
            ScopeSymbol => DefaultSymbols.Types.Scope,
            _ => DefaultSymbols.Types.Unkown
        };

        return new NameExpressionSymbol(symbol, type);
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

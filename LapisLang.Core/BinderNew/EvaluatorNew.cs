



namespace LapisLang.Core;


public class LapisEvaluator
{

}

public class BindingContextNew
{

}

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
            // case BinaryExpressionSyntax bes: return BindBinaryExpression(bes, context);
            default: return ExpressionSymbol.Unknown;
        }
    }

    // private BinaryExpressionSymbol BindBinaryExpression(BinaryExpressionSyntax bes, ScopeSymbol context)
    // {
    //     var left = BindExpression(bes, context);
    //     var right = BindExpression(bes, context);
    //     var oper = context.GetBinaryOpFor(left.Type, right.Type, bes.OperatorToken.Kind);

    //     return new BinaryExpressionSymbol(left, right, oper.,);
    // }

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


public class DefaultSymbols
{
    public static class Types
    {
        public static TypeSymbol Integer = new TypeSymbol("integer");
        public static TypeSymbol Decimal = new TypeSymbol("decimal");
        public static TypeSymbol Boolean = new TypeSymbol("boolean");
        public static TypeSymbol String = new TypeSymbol("string");

        public static TypeSymbol Unkown = new TypeSymbol("unkown");

    }
}


    public class BinaryExpressionSymbol : ExpressionSymbol
    {
        public BinaryExpressionSymbol(
            ExpressionSymbol left,
            ExpressionSymbol right,
            BinaryOperatorKind binaryOp,
            TypeSymbol type
        ) : base(type)
        {

        }
    }

    public enum BinaryOperatorKind { Add, Sub, Mul, Div, Mod }
    public abstract class Symbol
    {
        public static Symbol Unkown = new UnkwonSymbol();
    }

    public abstract class ExpressionSymbol : Symbol
    {
        public static ExpressionSymbol Unknown = new UnkwonExpressionSymbol();
        public ExpressionSymbol(TypeSymbol type)
        {
            Type = type;
        }

        public TypeSymbol Type { get; }
    }
    public class UnkwonSymbol : Symbol;
    public class UnkwonExpressionSymbol : ExpressionSymbol
    {
        public UnkwonExpressionSymbol() : base(DefaultSymbols.Types.Unkown)
        {
        }
    }

    public class TypeSymbol : Symbol
    {
        public TypeSymbol(string typeName)
        {
            TypeName = typeName;
        }

        public string TypeName { get; }
    }
    public class ValueSymbol : ExpressionSymbol
    {
        public ValueSymbol(object? value, TypeSymbol type) : base(type)
        {
        Value = value;
    }

    public object? Value { get; }
}

    public class ScopeSymbol : Symbol
    {
        internal object GetBinaryOpFor(TypeSymbol left, TypeSymbol right, TokenKind kind)
        {
            throw new NotImplementedException();
        }
    }

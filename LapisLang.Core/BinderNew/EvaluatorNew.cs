



namespace LapisLang.Core;


public class LapisEvaluator
{

}

public class BindingContextNew
{

}


public class DefaultSymbols
{
    public static class Types
    {
        public static TypeSymbol Boolean = CreateBool();
        public static TypeSymbol Integer = CreateInteger();
        public static TypeSymbol Decimal = new TypeSymbol("decimal");
        public static TypeSymbol String = new TypeSymbol("string");
        public static TypeSymbol Type = new TypeSymbol("type");
        public static TypeSymbol Scope = new TypeSymbol("scope");
        public static TypeSymbol Unkown = new TypeSymbol("unkown");


        private static TypeSymbol CreateInteger()
        {
            var type = new TypeSymbol("integer");

            type.DefineSymbol("@bop_+", new BinaryOperatorSymbol(BinaryOperatorKind.Add, type, type));
            type.DefineSymbol("@bop_-", new BinaryOperatorSymbol(BinaryOperatorKind.Sub, type, type));
            type.DefineSymbol("@bop_/", new BinaryOperatorSymbol(BinaryOperatorKind.Div, type, type));
            type.DefineSymbol("@bop_*", new BinaryOperatorSymbol(BinaryOperatorKind.Mul, type, type));
            type.DefineSymbol("@bop_%", new BinaryOperatorSymbol(BinaryOperatorKind.Mod, type, type));

            type.DefineSymbol("@bop_eq", new BinaryOperatorSymbol(BinaryOperatorKind.Equality, type, Boolean));
            type.DefineSymbol("@bop_neq", new BinaryOperatorSymbol(BinaryOperatorKind.Inequality, type, Boolean));
            type.DefineSymbol("@bop_gt", new BinaryOperatorSymbol(BinaryOperatorKind.GreatherThan, type, Boolean));
            type.DefineSymbol("@bop_gteq", new BinaryOperatorSymbol(BinaryOperatorKind.GreatherOrEqual, type, Boolean));
            type.DefineSymbol("@bop_lt", new BinaryOperatorSymbol(BinaryOperatorKind.LessThan, type, Boolean));
            type.DefineSymbol("@bop_lteq", new BinaryOperatorSymbol(BinaryOperatorKind.LessOrEqual, type, Boolean));


            type.DefineSymbol("@uop_-", new UnaryOperatorSymbol(UnaryOperatorKind.Inverse, type));
            type.DefineSymbol("@uop_+", new UnaryOperatorSymbol(UnaryOperatorKind.Identity, type));

            return type;
        }

        private static TypeSymbol CreateBool()
        {
            var type = new TypeSymbol("boolean");

            type.DefineSymbol("@bop_and", new BinaryOperatorSymbol(BinaryOperatorKind.LogicAnd, type, type));
            type.DefineSymbol("@bop_or", new BinaryOperatorSymbol(BinaryOperatorKind.LogicOr, type, type));
            type.DefineSymbol("@bop_eq", new BinaryOperatorSymbol(BinaryOperatorKind.Equality, type, type));
            type.DefineSymbol("@bop_neq", new BinaryOperatorSymbol(BinaryOperatorKind.Inequality, type, type));

            type.DefineSymbol("@uop_not", new UnaryOperatorSymbol(UnaryOperatorKind.Negation, type));

            return type;
        }
    }
}


public class NameExpressionSymbol : ExpressionSymbol
{
    public NameExpressionSymbol(
        Symbol symbol,
        TypeSymbol type
    ) : base(type)
    {
        Symbol = symbol;
    }

    public Symbol Symbol { get; }
}
public class UnaryExpressionSymbol : ExpressionSymbol
{
    public UnaryExpressionSymbol(
        ExpressionSymbol expression,
        UnaryOperatorKind unaryOp,
        TypeSymbol type
    ) : base(type)
    {
        Expression = expression;
        UnaryOp = unaryOp;
    }

    public ExpressionSymbol Expression { get; }
    public UnaryOperatorKind UnaryOp { get; }
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
        Left = left;
        Right = right;
        BinaryOp = binaryOp;
        }

        public ExpressionSymbol Left { get; }
        public ExpressionSymbol Right { get; }
        public BinaryOperatorKind BinaryOp { get; }
    }

    public enum BinaryOperatorKind { Unknown = -1, Add, Sub, Mul, Div, Mod, Equality, Inequality, GreatherThan, GreatherOrEqual, LessThan, LessOrEqual, LogicAnd, LogicOr }
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
    public class UnkownStatementSymbol: StatementSymbol;
    public class ValueSymbol : ExpressionSymbol
    {
        public ValueSymbol(object? value, TypeSymbol type) : base(type)
        {
        Value = value;
    }

    public object? Value { get; }
}





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

            type.DefineSymbol(TokenKind.Plus, new BinaryOperatorSymbol(BinaryOperatorKind.Add, type, type));
            type.DefineSymbol(TokenKind.Minus, new BinaryOperatorSymbol(BinaryOperatorKind.Sub, type, type));
            type.DefineSymbol(TokenKind.Slash, new BinaryOperatorSymbol(BinaryOperatorKind.Div, type, type));
            type.DefineSymbol(TokenKind.Star, new BinaryOperatorSymbol(BinaryOperatorKind.Mul, type, type));
            type.DefineSymbol(TokenKind.Percent, new BinaryOperatorSymbol(BinaryOperatorKind.Mod, type, type));

            type.DefineSymbol(TokenKind.DoubleEquals, new BinaryOperatorSymbol(BinaryOperatorKind.Equality, type, Boolean));
            type.DefineSymbol(TokenKind.BangEquals, new BinaryOperatorSymbol(BinaryOperatorKind.Inequality, type, Boolean));
            type.DefineSymbol(TokenKind.RightArrow, new BinaryOperatorSymbol(BinaryOperatorKind.GreatherThan, type, Boolean));
            type.DefineSymbol(TokenKind.RightArrowEquals, new BinaryOperatorSymbol(BinaryOperatorKind.GreatherOrEqual, type, Boolean));
            type.DefineSymbol(TokenKind.LeftArrow, new BinaryOperatorSymbol(BinaryOperatorKind.LessThan, type, Boolean));
            type.DefineSymbol(TokenKind.LeftArrowEquals, new BinaryOperatorSymbol(BinaryOperatorKind.LessOrEqual, type, Boolean));


            type.DefineSymbol(TokenKind.Minus, new UnaryOperatorSymbol(UnaryOperatorKind.Inverse, type));
            type.DefineSymbol(TokenKind.Plus, new UnaryOperatorSymbol(UnaryOperatorKind.Identity, type));

            return type;
        }

        private static TypeSymbol CreateBool()
        {
            var type = new TypeSymbol("boolean");

            type.DefineSymbol(TokenKind.AndKeyword, new BinaryOperatorSymbol(BinaryOperatorKind.LogicAnd, type, type));
            type.DefineSymbol(TokenKind.OrKeyword, new BinaryOperatorSymbol(BinaryOperatorKind.LogicOr, type, type));
            type.DefineSymbol(TokenKind.DoubleEquals, new BinaryOperatorSymbol(BinaryOperatorKind.Equality, type, type));
            type.DefineSymbol(TokenKind.BangEquals, new BinaryOperatorSymbol(BinaryOperatorKind.Inequality, type, type));

            type.DefineSymbol(TokenKind.NotKeyword, new UnaryOperatorSymbol(UnaryOperatorKind.Negation, type));

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

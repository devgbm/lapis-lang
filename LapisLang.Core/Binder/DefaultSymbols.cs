
namespace LapisLang.Core;

public class DefaultSymbols
{
    public static class Types
    {
        public static TypeSymbol Boolean = CreateBool();
        public static TypeSymbol Integer = CreateInteger();
        public static TypeSymbol Decimal = CreateDecimal();
        public static TypeSymbol String = new TypeSymbol("string");
        public static TypeSymbol Type = new TypeSymbol("type");
        public static TypeSymbol Scope = new TypeSymbol("scope");
        public static TypeSymbol Unkown = new TypeSymbol("unkown");
        public static TypeSymbol Void = new TypeSymbol("void");
        public static TypeSymbol Function = new TypeSymbol("function");

        private static TypeSymbol CreateDecimal()
        {
            var type = new TypeSymbol("decimal");

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
    public static ScopeSymbol CreateDefaultScope()
    {
        var root = new ScopeSymbol();

        root.DefineSymbol("integer", Types.Integer);
        root.DefineSymbol("boolean", Types.Boolean);
        root.DefineSymbol("decimal", Types.Decimal);
        root.DefineSymbol("string", Types.String);
        root.DefineSymbol("type", Types.Type);
        root.DefineSymbol("scope", Types.Scope);
        root.DefineSymbol("unkown", Types.Unkown);
        root.DefineSymbol("function", Types.Function);
        root.DefineSymbol("void", Types.Void);

        root.DefineSymbol("@", root);

        return root;
    }
}

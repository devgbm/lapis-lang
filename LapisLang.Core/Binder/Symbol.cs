





namespace LapisLang.Core;

public abstract class Symbol
{
    public static Symbol Unkown = new UnkownSymbol();
}

public class VoidSymbol : ExprSymbol
{
    public static VoidSymbol Instance = new();
    private VoidSymbol(){}
    public override bool IsConstant => true;
    public override TypeSymbol Type => LangDefaults.Types.Void;
}
public class UnkownSymbol : Symbol
{
}

public static class LangDefaults
{
    public static class Types
    {
        public static TypeSymbol Unkown = new PrimitiveTypeSymbol("Unkown");
        public static TypeSymbol Void = new PrimitiveTypeSymbol("Void");
        public static TypeSymbol Type = CreateType();
        public static TypeSymbol Integer = CreateInteger();
        public static TypeSymbol Boolean = CreateBoolean();
        public static TypeSymbol String = CreateString();
        public static TypeSymbol Decimal = CreateDecimal();
        public static TypeSymbol Function = new PrimitiveTypeSymbol("Function");

        private static TypeSymbol CreateDecimal()
        {
            var symbol = new PrimitiveTypeSymbol("Decimal");
            return symbol;
        }

        private static TypeSymbol CreateString()
        {
            var symbol = new PrimitiveTypeSymbol("String");
            return symbol;
        }

        private static TypeSymbol CreateBoolean()
        {
            var symbol = new PrimitiveTypeSymbol("Boolean");
            return symbol;
        }

        private static TypeSymbol CreateInteger()
        {
            var symbol = new PrimitiveTypeSymbol("Integer");
            return symbol;
        }

        private static TypeSymbol CreateType()
        {
            var symbol = new PrimitiveTypeSymbol("Type");
            return symbol;
        }
    }
    public static BoundScope CreateDefaultScope()
    {
        var scope = new BoundScope();
        scope.Define("Void", Types.Void);
        scope.Define("Unkown", Types.Unkown);
        scope.Define("Boolean", Types.Boolean);
        scope.Define("Integer", Types.Integer);
        scope.Define("Decimal", Types.Decimal);
        scope.Define("String", Types.String);
        scope.Define("Type", Types.Type);
        scope.Define("Function", Types.Function);

        return scope;
    }
}
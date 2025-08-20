





namespace LapisLang.Core;

public abstract class Symbol
{
    public static Symbol Unkown = new UnkownSymbol();
}

public class UnkownSymbol : Symbol
{
}

public static class LangDefaults
{
    public static class Types
    {
        public static TypeSymbol Unkown = new PrimitiveTypeSymbol("unkown");
        public static TypeSymbol Type = CreateType();
        public static TypeSymbol Integer = CreateInteger();
        public static TypeSymbol Boolean = CreateBoolean();
        public static TypeSymbol String = CreateString();
        public static TypeSymbol Decimal = CreateDecimal();

        private static TypeSymbol CreateDecimal()
        {
            var symbol = new PrimitiveTypeSymbol("decimal");
            return symbol;
        }

        private static TypeSymbol CreateString()
        {
            var symbol = new PrimitiveTypeSymbol("string");
            return symbol;
        }

        private static TypeSymbol CreateBoolean()
        {
            var symbol = new PrimitiveTypeSymbol("boolean");
            return symbol;
        }

        private static TypeSymbol CreateInteger()
        {
            var symbol = new PrimitiveTypeSymbol("integer");
            return symbol;
        }

        private static TypeSymbol CreateType()
        {
            var symbol = new PrimitiveTypeSymbol("type");
            return symbol;
        }
    }
    public static BoundScope CreateDefaultScope()
    {
        var scope = new BoundScope();
        scope.Define("boolean", Types.Boolean);
        scope.Define("integer", Types.Integer);
        scope.Define("decimal", Types.Decimal);
        scope.Define("string", Types.String);
        scope.Define("type", Types.Type);

        return scope;
    }
}
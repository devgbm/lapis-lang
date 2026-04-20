





namespace LapisLang.Core;

public abstract class Symbol
{
    public static Symbol Unkown = new UnkownSymbol();
}

public class VoidSymbol : ExprSymbol
{
    public static VoidSymbol Instance = new();
    private VoidSymbol(){}
    public override bool IsCompileTime => true;
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
        public static TypeSymbol Namespace = new PrimitiveTypeSymbol("Namespace");
        public static TypeSymbol Type = CreateType();
        public static TypeSymbol Integer = CreateInteger();
        public static TypeSymbol Boolean = CreateBoolean();
        public static TypeSymbol String = CreateString();
        public static TypeSymbol Decimal = CreateDecimal();
        public static TypeSymbol Function = new PrimitiveTypeSymbol("Func");

        private static TypeSymbol CreateDecimal()
        {
            var symbol = new PrimitiveTypeSymbol("Dec");
            return symbol;
        }

        private static TypeSymbol CreateString()
        {
            var symbol = new PrimitiveTypeSymbol("Str");
            return symbol;
        }

        private static TypeSymbol CreateBoolean()
        {
            var symbol = new PrimitiveTypeSymbol("Bool");
            return symbol;
        }

        private static TypeSymbol CreateInteger()
        {
            var symbol = new PrimitiveTypeSymbol("Int");
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
        var scope = new BoundScope("global");
        scope.Define("Void", Types.Void);
        scope.Define("Unkown", Types.Unkown);
        scope.Define("Bool", Types.Boolean);
        scope.Define("Int", Types.Integer);
        scope.Define("Dec", Types.Decimal);
        scope.Define("Str", Types.String);
        scope.Define("Type", Types.Type);
        scope.Define("Func", Types.Function);

        scope.Define("Console", LangStd.Console.ConsoleNamespace);

        return scope;
    }
}


public static partial class LangStd
{
    public class Console
    {
        public static BoundScope ConsoleNamespace = CreateConsoleNs();

        public static BoundScope CreateConsoleNs()
        {
            var ns = new BoundScope("Console");
            ns.Define("log", Log());
            return ns;
        }

        public static NativeFuncSymbol Log()
        {
            var type = new FuncTypeSymbol([new ParameterSymbol("str", LangDefaults.Types.String, true)], LangDefaults.Types.Void);
            var symbol = new NativeFuncSymbol(true, type, (object? arg) =>
            {
                System.Console.WriteLine(arg?.ToString());
            });
            return symbol;
        }
    }
}
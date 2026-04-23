





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
        public static TypeSymbol Type = new PrimitiveTypeSymbol("Type");
        public static TypeSymbol Integer = new PrimitiveTypeSymbol("Int");
        public static TypeSymbol Boolean = new PrimitiveTypeSymbol("Bool");
        public static TypeSymbol String = new PrimitiveTypeSymbol("Str");
        public static TypeSymbol Decimal = new PrimitiveTypeSymbol("Dec");
        public static TypeSymbol Function = new PrimitiveTypeSymbol("Func");

        static Types()
        {
            BuildIntegerType();
            BuildDecimalType();
            BuildBoolType();
            BuildStrType();
        }
        private static void BuildStrType()
        {
        }

        private static void BuildIntegerType()
        {
            var toStringType = new FuncTypeSymbol([], String);
            var toStringNative = new NativeFuncSymbol(true, toStringType,
                (ExprSymbol self) => self is IntegerSymbol i ? i.Value.ToString() : "",
                isInstanceMethod: true);
            Integer.DefineMember("toString", toStringNative);


            var parseType = new FuncTypeSymbol([new ParameterSymbol("value", String, true)], Integer, true);
            var parseNative = new NativeFuncSymbol(false, parseType, (object? arg) =>
            {
                return  long.Parse(arg?.ToString() ?? "0");
            });
            Integer.DefineMember("parse", parseNative);
        }

        public static void BuildDecimalType()
        {
            var toStringType = new FuncTypeSymbol([], String);
            var toStringNative = new NativeFuncSymbol(true, toStringType,
                (ExprSymbol self) => self is DecimalSymbol i ? i.Value.ToString() : "",
                isInstanceMethod: true);
            Decimal.DefineMember("toString", toStringNative);

            var parseType = new FuncTypeSymbol([new ParameterSymbol("value", String, true)], Decimal, true);
            var parseNative = new NativeFuncSymbol(false, parseType, (object? arg) =>
            {
                return  decimal.Parse(arg?.ToString() ?? "0");
            });

            Decimal.DefineMember("parse", parseNative);
        }

        public static void BuildBoolType()
        {
            var toStringType = new FuncTypeSymbol([], String);
            var toStringNative = new NativeFuncSymbol(true, toStringType,
                (ExprSymbol self) => self is BooleanSymbol i ? i.Value.ToString() : "",
                isInstanceMethod: true);
            Boolean.DefineMember("toString", toStringNative);
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
            ns.Define("write", Write());
            ns.Define("read", Read());

            return ns;
        }

        public static NativeFuncSymbol Write()
        {
            var type = new FuncTypeSymbol([new ParameterSymbol("str", LangDefaults.Types.String, true)], LangDefaults.Types.Void);
            var symbol = new NativeFuncSymbol(true, type, (object? arg) =>
            {
                System.Console.WriteLine(arg?.ToString());
            });
            return symbol;
        }

        public static NativeFuncSymbol Read()
        {
            var type = new FuncTypeSymbol([], LangDefaults.Types.String);
            return new NativeFuncSymbol(false, type, () =>  System.Console.ReadLine());

        }
    }
}
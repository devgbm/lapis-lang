namespace LapisLang.Core;

public static partial class LangStd
{
    public class Compiler
    {
        public static BoundScope DebugNamespace = new BoundScope("Debug");
        static Compiler()
        {
            var funcType = new FuncTypeSymbol([], LangDefaults.Types.Integer, BindFlag.IsRuntimeOnly);
            DebugNamespace.Define("runtime", new NativeFuncSymbol(funcType, () => 0L));
        }
    }

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
            var type = new FuncTypeSymbol([new ParameterSymbol("str", LangDefaults.Types.String, true)], LangDefaults.Types.Void, BindFlag.IsRuntimeOnly);
            var symbol = new NativeFuncSymbol(type, (object? arg) =>
            {
                System.Console.WriteLine(arg?.ToString());
            });
            return symbol;
        }

        public static NativeFuncSymbol Read()
        {
            var type = new FuncTypeSymbol([], LangDefaults.Types.String, BindFlag.IsRuntimeOnly);
            return new NativeFuncSymbol(type, () => System.Console.ReadLine());
        }
    }
}

using LapisLang.Core;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0) RunREPL();
    }

    private static void RunREPL()
    {
        var scope = EvaluationScope.CreateScope();
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            var result = LapisInterpreter.Evaluate(input, scope);
            PrintResult(result);
        }
    }

    private static void PrintResult(EvaluationResult result)
    {
        if (result.Diagnostics.HasErrors)
        {
            Console.WriteLine(result.Diagnostics.GetMessages());
        }
        else
        {
            string stringifiedSymbol = StringifySymbol(result.result);
            Console.WriteLine(stringifiedSymbol);
        }
    }

    private static string StringifySymbol(Symbol result)
    {
        return result switch
        {
            IntegerSymbol integerSymbol => integerSymbol.Value.ToString(),
            BooleanSymbol booleanSymbol => booleanSymbol.Value.ToString(),
            StringSymbol stringSymbol => stringSymbol.Value,
            DecimalSymbol decimalSymbol => decimalSymbol.Value.ToString(),
            TypeSymbol typeSymbol => StringifyType(typeSymbol),
            InstanceSymbol instanceSymbol => StringifyInstance(instanceSymbol),
            FuncSymbol funcSymbol => StringifyFunc(funcSymbol),
            _ => result.ToString()!
        };
    }

    private static string StringifyFunc(FuncSymbol funcSymbol)
    {
        return $"func ({string.Join(", ", funcSymbol.Parameters.Select(e => $"{StringifySymbol(e.Expression)} {e.Name}"))}) {StringifySymbol(funcSymbol.ReturnType)}";   
    }

    private static string StringifyInstance(InstanceSymbol instanceSymbol)
    {
        return $"{instanceSymbol.Type.DebugName} {{ {string.Join(", ", instanceSymbol.Atributes.Select(e => $"{e.Key}: {StringifySymbol(e.Value)}"))} }}";

    }

    private static string StringifyType(TypeSymbol typeSymbol)
    {
        if (typeSymbol is PrimitiveTypeSymbol pts) return $"type {pts.DebugName}";
        if (typeSymbol is StructTypeSymbol sts) return $"type {sts.DebugName} {{ {string.Join(", ", sts.Fields.Select(e => $"{e.Name}: {e.Type.DebugName}"))} }}";
        return "type";
    }
}
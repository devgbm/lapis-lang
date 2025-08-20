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
            string stringifiedSymbol = StringifySymbol(result);
            Console.WriteLine(stringifiedSymbol);
        }
    }

    private static string StringifySymbol(EvaluationResult result)
    {
        return result.result switch
        {
            IntegerSymbol integerSymbol => integerSymbol.Value.ToString(),
            BooleanSymbol booleanSymbol => booleanSymbol.Value.ToString(),
            StringSymbol stringSymbol => stringSymbol.Value,
            DecimalSymbol decimalSymbol => decimalSymbol.Value.ToString(),
            TypeSymbol typeSymbol => StringifyType(typeSymbol),
            _ => result.result.ToString()!
        };
    }

    private static string StringifyType(TypeSymbol typeSymbol)
    {
        if (typeSymbol is PrimitiveTypeSymbol pts) return $"type {pts.DebugName}";
        if (typeSymbol is StructTypeSymbol sts) return $"type {sts.DebugName} {{ {string.Join(", ", sts.Fields.Select(e => $"{e.Name}: {e.Type.DebugName}"))} }}";
        return "type";
    }
}
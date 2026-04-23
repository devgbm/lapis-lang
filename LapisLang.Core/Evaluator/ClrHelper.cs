


using LapisLang.Core;

public class ClrHelper
{
    public static object? ToClr(ExprSymbol symbol)
    {
        switch(symbol)
        {
            case IntegerSymbol intSymbol:
                return intSymbol.Value;
            case BooleanSymbol boolSymbol:
                return boolSymbol.Value;
            case StringSymbol strSymbol:
                return strSymbol.Value;
            case DecimalSymbol decSymbol:
                return decSymbol.Value;

            default: return symbol; // complex types (InstanceSymbol, etc.) passed as-is
        }
    }

    public static ExprSymbol ToSymbol(object? clr)
    {
        switch(clr)
        {
            case int:
            case long:
            case uint:
            case ulong:
                return new IntegerSymbol((long)clr);

            case string:
                return new StringSymbol((string)clr);
            case bool:
                return new BooleanSymbol((bool)clr);
            case decimal:
                return new DecimalSymbol((decimal)clr);
            default: return ExprSymbol.Unkown;
        }
    }
}
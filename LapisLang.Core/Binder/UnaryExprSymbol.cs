namespace LapisLang.Core;

public class UnaryExprSymbol : ExprSymbol
{
    public ExprSymbol Operand { get; }
    public UnaryOperator UnaryOperator { get; }

    public UnaryExprSymbol(ExprSymbol left, UnaryOperator unaryOperator, TypeSymbol type, BindFlag flags = BindFlag.None)
    {
        Operand = left;
        UnaryOperator = unaryOperator;
        Type = type;
        Flags = flags;
    }

    public override TypeSymbol Type { get; }
}

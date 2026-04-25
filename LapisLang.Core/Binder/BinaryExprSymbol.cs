namespace LapisLang.Core;

public class BinaryExprSymbol : ExprSymbol
{
    public ExprSymbol Left { get; }
    public ExprSymbol Right { get; }
    public BinaryOperator BinaryOperator { get; }

    public BinaryExprSymbol(ExprSymbol left, ExprSymbol right, BinaryOperator binaryOperator, TypeSymbol type, BindFlag flags = BindFlag.None)
    {
        Left = left;
        Right = right;
        BinaryOperator = binaryOperator;
        Type = type;
        Flags = flags;
    }

    public override TypeSymbol Type { get; }
}

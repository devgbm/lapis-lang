
namespace LapisLang.Core;

public abstract class TypeSymbol : ExprSymbol
{
    public TypeSymbol()
    { }
    public TypeSymbol(string? debugName)
    {
        DebugName = debugName;
    }

    public string? DebugName { get; set;  }
    public override TypeSymbol Type { get => LangDefaults.Types.Type; }
    public abstract bool IsEquivalent(ExprSymbol symbol);
}

public enum BinaryOperator
{
    Add,
    Sub,
    Mul,
    Div,
    Mod,
    GreatherThan,
    LessThan,
    GreatherEqualThan,
    LessEqualThan,
    Equality,
    Inequality,
    TypeWith,
    TypeWithout,
    TypeContains,
    Unkown,
    LogicalAnd,
    LogicalOr
}
public class BinaryExprSymbol : ExprSymbol
{
    public ExprSymbol Left { get; }
    public ExprSymbol Right { get; }
    public BinaryOperator BinaryOperator { get; }

    public BinaryExprSymbol(ExprSymbol left, ExprSymbol right, BinaryOperator binaryOperator, TypeSymbol type)
    {
        Left = left;
        Right = right;
        BinaryOperator = binaryOperator;
        Type = type;
    }

    public override bool IsConstant => Left.IsConstant && Right.IsConstant;

    public override TypeSymbol Type { get; }
}

namespace LapisLang.Core;

public abstract class TypeSymbol : ExprSymbol
{
    private Dictionary<string, ExprSymbol> _members = new();
    public TypeSymbol()
    { }
    public TypeSymbol(string? debugName)
    {
        DebugName = debugName;
    }

    public ExprSymbol GetMember(string name)
    {
        return _members.GetValueOrDefault(name, ExprSymbol.Unkown);
    }

    public bool DefineMember(string name, ExprSymbol symbol)
    {
        if (_members.ContainsKey(name))
        {
            return false;
        }

        _members.Add(name, symbol);
        return true;
    }

    public string? DebugName { get; set; }
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
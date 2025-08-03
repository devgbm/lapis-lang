



namespace LapisLang.Core;

public class UnaryExpressionSymbol : ExpressionSymbol
{
    public UnaryExpressionSymbol(
        ExpressionSymbol expression,
        UnaryOperatorKind unaryOp,
        TypeSymbol type
    ) : base(type)
    {
        Expression = expression;
        UnaryOp = unaryOp;
    }

    public ExpressionSymbol Expression { get; }
    public UnaryOperatorKind UnaryOp { get; }
}

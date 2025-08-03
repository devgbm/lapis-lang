



namespace LapisLang.Core;

public class BinaryExpressionSymbol : ExpressionSymbol
    {
        public BinaryExpressionSymbol(
            ExpressionSymbol left,
            ExpressionSymbol right,
            BinaryOperatorKind binaryOp,
            TypeSymbol type
        ) : base(type)
        {
        Left = left;
        Right = right;
        BinaryOp = binaryOp;
        }

        public ExpressionSymbol Left { get; }
        public ExpressionSymbol Right { get; }
        public BinaryOperatorKind BinaryOp { get; }
    }

namespace LapisLang.Core;

public class BinaryExpressionSyntax : ExpressionSyntax
{
    public BinaryExpressionSyntax(
        SourceSpan sourceSpan,
        ExpressionSyntax left,
        ExpressionSyntax right,
        Token operatorToken
    )
    {
        SourceSpan = sourceSpan;
        Left = left;
        Right = right;
        OperatorToken = operatorToken;
    }
    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax Left { get; }
    public ExpressionSyntax Right { get; }
    public Token OperatorToken { get; }
}
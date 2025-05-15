namespace LapisLang.Core;

public class LiteralExpressionSyntax : ExpressionSyntax
{
    public LiteralExpressionSyntax(
        SourceSpan sourceSpan,
        LiteralType literalType
    )
    {
        SourceSpan = sourceSpan;
        LiteralType = literalType;
    }

    public override SourceSpan SourceSpan { get; }
    public LiteralType LiteralType { get; }
}
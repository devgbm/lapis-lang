namespace LapisLang.Core;

public class UnkownExpressionSyntax : ExpressionSyntax
{
    public UnkownExpressionSyntax(SourceSpan span)
    {
        SourceSpan = span;
    }
    public override SourceSpan SourceSpan { get; }
}

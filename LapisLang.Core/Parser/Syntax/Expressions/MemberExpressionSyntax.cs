namespace LapisLang.Core;

public class MemberExpressionSyntax : ExpressionSyntax
{
    public MemberExpressionSyntax(
        SourceSpan sourceSpan,
        ExpressionSyntax expresison,
        NameExpressionSyntax member)
    {
        SourceSpan = sourceSpan;
        Expresison = expresison;
        Member = member;
    }
    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax Expresison { get; }
    public NameExpressionSyntax Member { get; }
}

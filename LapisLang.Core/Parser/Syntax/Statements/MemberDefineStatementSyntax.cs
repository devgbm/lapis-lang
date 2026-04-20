namespace LapisLang.Core;

public class MemberDefineStatementSyntax : StatementSyntax
{
    public MemberDefineStatementSyntax(SourceSpan sourceSpan, Token typeName, Token memberName, ExpressionSyntax expression)
    {
        SourceSpan = sourceSpan;
        TypeName = typeName;
        MemberName = memberName;
        Expression = expression;
    }

    public override SourceSpan SourceSpan { get; }
    public Token TypeName { get; }
    public Token MemberName { get; }
    public ExpressionSyntax Expression { get; }
}

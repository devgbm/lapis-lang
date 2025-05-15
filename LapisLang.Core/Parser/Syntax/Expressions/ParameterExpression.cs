namespace LapisLang.Core;

public class ParameterExpression : Syntax
{
    public override SourceSpan SourceSpan { get; }
    public Token Name { get; }
    public ExpressionSyntax Expression { get; }

    public ParameterExpression(ExpressionSyntax expression, Token name, SourceSpan sourceSpan)
    {
        Expression = expression;
        Name = name;
        SourceSpan = sourceSpan;
    }
}

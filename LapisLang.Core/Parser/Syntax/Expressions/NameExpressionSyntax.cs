namespace LapisLang.Core;

public class NameExpressionSyntax : ExpressionSyntax
{
    public override SourceSpan SourceSpan { get; }
    public Token Name { get; }

    public NameExpressionSyntax(
        SourceSpan sourceSpan,
        Token name)
    {
        SourceSpan = sourceSpan;
        Name = name;
    }
}

public class CompilerNameExpressionSyntax : NameExpressionSyntax
{

    public CompilerNameExpressionSyntax(
        SourceSpan sourceSpan,
        Token name) : base(sourceSpan, name)
    {
    }
}
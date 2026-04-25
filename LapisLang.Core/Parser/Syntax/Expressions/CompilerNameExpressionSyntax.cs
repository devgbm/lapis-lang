namespace LapisLang.Core;

public class CompilerNameExpressionSyntax : NameExpressionSyntax
{
    public CompilerNameExpressionSyntax(
        SourceSpan sourceSpan,
        Token name) : base(sourceSpan, name)
    {
    }
}

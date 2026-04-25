namespace LapisLang.Core;

public class ArgumentSyntax : Syntax
{
    public ArgumentSyntax(SourceSpan sourceSpan, ExpressionSyntax typeName, NameExpressionSyntax name)
    {
        SourceSpan = sourceSpan;
        TypeName = typeName;
        Name = name;
    }

    public override SourceSpan SourceSpan { get; }
    public ExpressionSyntax TypeName { get; }
    public NameExpressionSyntax Name { get; }
}

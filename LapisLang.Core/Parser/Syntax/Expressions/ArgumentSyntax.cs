using System.Reflection.Metadata;

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

public class SelfArgumentSyntax : ArgumentSyntax
{
    public SelfArgumentSyntax(SourceSpan sourceSpan) : base(sourceSpan, null!, null!) { }
}

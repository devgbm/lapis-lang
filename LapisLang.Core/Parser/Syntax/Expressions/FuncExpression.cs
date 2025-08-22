using System.Collections.Immutable;

namespace LapisLang.Core;

public class FuncExpressionSyntax : ExpressionSyntax
{
    public FuncExpressionSyntax(
        SourceSpan sourceSpan,
        ImmutableArray<ArgumentSyntax> arguments,
        ExpressionSyntax returnType,
        StatementSyntax statement)
    {
        SourceSpan = sourceSpan;
        Parameters = arguments;
        ReturnType = returnType;
        Statement = statement;
    }

    public override SourceSpan SourceSpan { get; }
    public ImmutableArray<ArgumentSyntax> Parameters { get; }
    public ExpressionSyntax ReturnType { get; }
    public StatementSyntax Statement { get; }
}
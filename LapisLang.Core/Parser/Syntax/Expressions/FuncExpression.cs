using System.Collections.Immutable;

namespace LapisLang.Core;

public class FuncExpression : ExpressionSyntax
{
    public FuncExpression(
        SourceSpan sourceSpan,
        ImmutableArray<ArgumentSyntax> arguments,
        TypeNameSyntax returnType,
        StatementSyntax statement)
    {
        SourceSpan = sourceSpan;
        Arguments = arguments;
        ReturnType = returnType;
        Statement = statement;
    }

    public override SourceSpan SourceSpan { get; }
    public ImmutableArray<ArgumentSyntax> Arguments { get; }
    public TypeNameSyntax ReturnType { get; }
    public StatementSyntax Statement { get; }
}
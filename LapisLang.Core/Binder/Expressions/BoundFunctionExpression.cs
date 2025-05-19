using System.Collections.Immutable;

namespace LapisLang.Core;

public class BoundFunctionExpression : BoundExpression
{
    public BoundFunctionExpression(
        SourceSpan span,
        ImmutableArray<BoundArgument> arguments,
        TypeRune returnType,
        BoundStatement statement
    )
    {
        Span = span;
        Arguments = arguments;
        ReturnType = returnType;
        Statement = statement;
        Type = LangDefaults.Types.Function;
    }
    public override TypeRune Type { get; }

    public override SourceSpan Span { get; }
    public ImmutableArray<BoundArgument> Arguments { get; }
    public TypeRune ReturnType { get; }
    public BoundStatement Statement { get; }
}

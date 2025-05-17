
using System.Collections.Immutable;

namespace LapisLang.Core;

public class BoundTypeExpression : BoundExpression
{
    public BoundTypeExpression(SourceSpan span, ImmutableArray<BoundFieldSyntax> boundFieldSyntaxes)
    {
        Span = span;
        BoundFieldSyntaxes = boundFieldSyntaxes;
        Type = LangDefaults.Types.Type;
    }

    public override TypeRune Type { get; }

    public override SourceSpan Span { get; }

    public ImmutableArray<BoundFieldSyntax> BoundFieldSyntaxes { get; }
}

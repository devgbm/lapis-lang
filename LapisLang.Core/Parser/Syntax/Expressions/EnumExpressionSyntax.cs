using System.Collections.Immutable;

namespace LapisLang.Core;

public class EnumExpressionSyntax : ExpressionSyntax
{
    public EnumExpressionSyntax(SourceSpan sourceSpan, ImmutableArray<EnumVariantSyntax> variants)
    {
        SourceSpan = sourceSpan;
        Variants = variants;
    }

    public override SourceSpan SourceSpan { get; }
    public ImmutableArray<EnumVariantSyntax> Variants { get; }
}

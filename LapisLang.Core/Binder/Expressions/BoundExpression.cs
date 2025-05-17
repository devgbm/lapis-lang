namespace LapisLang.Core;
public abstract class BoundExpression : BoundSyntax
{
    public abstract TypeRune Type { get; }
}

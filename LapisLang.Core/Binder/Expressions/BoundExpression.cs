
namespace LapisLang.Core;
public abstract class BoundExpression : BoundSyntax
{
    public abstract LapisType Type { get; }
}

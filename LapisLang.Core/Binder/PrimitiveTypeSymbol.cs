namespace LapisLang.Core;

public class PrimitiveTypeSymbol : TypeSymbol
{
    public PrimitiveTypeSymbol(string? debugName) : base(debugName)
    {
    }

    public override bool IsCompileTime => true;

    public override bool IsEquivalent(ExprSymbol symbol)
    {
        return Equals(this, symbol);
    }
}

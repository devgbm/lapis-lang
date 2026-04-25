namespace LapisLang.Core;

public class MemberSymbol : ExprSymbol
{
    public MemberSymbol(
        ExprSymbol expression,
        TypeSymbol type,
        string name,
        BindFlag flags = BindFlag.None
    )
    {
        Expression = expression;
        Name = name;
        Type = type;
        Flags = flags;
    }

    public override TypeSymbol Type { get; }

    public ExprSymbol Expression { get; }
    public string Name { get; }
}

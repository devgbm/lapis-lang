namespace LapisLang.Core;

public class MemberDefineSymbol : StatementSymbol
{
    public MemberDefineSymbol(string typeName, TypeSymbol targetType, string memberName, ExprSymbol expression)
    {
        TypeName = typeName;
        TargetType = targetType;
        MemberName = memberName;
        Expression = expression;
    }

    public string TypeName { get; }
    public TypeSymbol TargetType { get; }
    public string MemberName { get; }
    public ExprSymbol Expression { get; }
}

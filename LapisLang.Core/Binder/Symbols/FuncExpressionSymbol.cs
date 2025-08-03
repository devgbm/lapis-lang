



using System.Collections.Immutable;

namespace LapisLang.Core;

public class FunctionParameter
{
    public string Name { get; }
    public TypeSymbol Type { get; }
    public FunctionParameter(string name, TypeSymbol type)
    {
        Name = name;
        Type = type;
    }
}

public class FuncSymbol : ExpressionSymbol
{
    public FuncSymbol(
        SourceSpan sourceSpan,
        ImmutableArray<FunctionParameter> parameters,
        TypeSymbol returnType,
        StatementSymbol statement) : base(DefaultSymbols.Types.Function)
    {
        SourceSpan = sourceSpan;
        Parameters = parameters;
        ReturnType = returnType;
        Statement = statement;
    }

    public SourceSpan SourceSpan { get; }
    public ImmutableArray<FunctionParameter> Parameters { get; }
    public TypeSymbol ReturnType { get; }
    public StatementSymbol Statement { get; }
}

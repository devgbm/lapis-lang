namespace LapisLang.Core;

public class IfSymbol : StatementSymbol
{
    public IfSymbol(ExprSymbol expression, StatementSymbol statements, StatementSymbol? elseStatements)
    {
        ConditionExpression = expression;
        Statements = statements;
        ElseStatements = elseStatements;
    }

    public ExprSymbol ConditionExpression { get; }
    public StatementSymbol Statements { get; }
    public StatementSymbol? ElseStatements { get; }
}

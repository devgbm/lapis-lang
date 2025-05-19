

using System.Collections.Immutable;

namespace LapisLang.Core;


public class FunctionValue
{
    public FunctionValue(
        ImmutableArray<BoundArgument> arguments,
        BoundStatement statement
    )
    {
        Arguments = arguments;
        Statement = statement;
    }
    public ImmutableArray<BoundArgument> Arguments { get; }
    public BoundStatement Statement { get; }
}

public class Evaluator
{
    public DiagnosticsBag Diagnostics { get; }

    public Evaluator()
    {
        Diagnostics = new DiagnosticsBag();
    }
    public object? Evaluate(BoundSyntax syntax, EvaluationContext context)
    {
        switch (syntax)
        {
            case BoundExpression be: return EvaluateExpression(be, context);
            case BoundStatement bs: return EvaluateStatement(bs, context);
            default: return null;
        }
    }

    public object? EvaluateStatement(BoundStatement statement, EvaluationContext context)
    {
        switch (statement)
        {
            case BoundVariableDeclaration bvd: return EvaluateVariableDeclaration(bvd, context);
            case BoundExpressionStatement bes: return EvaluateExpression(bes.Expression, context);
            case BoundScopeStatement bss: return EvaluateScopeStatement(bss, context);
            case BoundReturnStatement brs: return EvaluateExpression(brs.Expression, context);
            default: return null;
        }
    }

    private object? EvaluateScopeStatement(BoundScopeStatement bss, EvaluationContext context)
    {
        foreach (var statement in bss.Statements)
        {
            var value = EvaluateStatement(statement, context);
            if (statement is BoundReturnStatement) return value;
        }
        return null;
    }

    private object? EvaluateVariableDeclaration(BoundVariableDeclaration bvd, EvaluationContext context)
    {
        var value = EvaluateExpression(bvd.Rune.Expression, context);
        if (value is not null)
        {
            bvd.Rune.Value = value;
            context.DeclareVariable(bvd.Rune, value);
        }
        return null;
    }

    public object? EvaluateExpression(BoundExpression expression, EvaluationContext context)
    {
        switch (expression)
        {
            case BoundLiteralExpression ble:
                return EvaluateLiteral(ble, context);

            case BoundBinaryExpression bbe:
                return EvaluateBinaryExpression(bbe, context);

            case BoundUnaryExpression bue:
                return EvaluateUnaryExpression(bue, context);

            case BoundNameExpression bne:
                return EvaluateNameExpression(bne, context);

            case BoundInstanceInitializationExpression bie:
                return EvaluateInstanceInitialization(bie, context);

            case BoundMemberExpression bme:
                return EvaluateMemberExpression(bme, context);

            case BoundFunctionExpression bfe:
                return EvaluateFunctionExpression(bfe, context);
            
            case BoundCallExpression bce:
                return EvaluateCallExpression(bce, context);

            default: return null;
        }
    }

    private object? EvaluateCallExpression(BoundCallExpression bce, EvaluationContext context)
    {
        var expression = (EvaluateExpression(bce.Expression, context) as FunctionRune)!;

        foreach (var arg in expression.BoundArgument.Zip(bce.Arguments))
        {
            var argumentValue = EvaluateExpression(arg.Second, context);
            var variable = new VariableRune(arg.First.Name, arg.First.Type, arg.Second);
            variable.Value = argumentValue;
            context.DeclareVariable(variable, argumentValue);
        }
        return EvaluateStatement(expression.Statement, context);
    }

    private object? EvaluateFunctionExpression(BoundFunctionExpression bfe, EvaluationContext context)
    {
        return new FunctionValue(bfe.Arguments, bfe.Statement);
    }

    private object? EvaluateMemberExpression(BoundMemberExpression bme, EvaluationContext context)
    {
        var expression = EvaluateExpression(bme.Expression, context) as Dictionary<string, object?>;
        return expression![bme.Member.Name];
    }

    private object? EvaluateInstanceInitialization(BoundInstanceInitializationExpression bie, EvaluationContext context)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var fieldInit in bie.Initiaizations)
        {
            var fieldValue = EvaluateExpression(fieldInit.Expression, context);
            dict[fieldInit.Name] = fieldValue;
        }
        return dict;
    }

    private object? EvaluateNameExpression(BoundNameExpression bne, EvaluationContext context)
    {
        var rune = context.ResolveName(bne.Name);
        if (rune.Kind == RuneKind.Variable)
        {

            return ((VariableRune)rune).Value;
        }

        return rune;
    }

    private object? EvaluateUnaryExpression(BoundUnaryExpression bue, EvaluationContext context)
    {
        var expression = Evaluate(bue.Expression, context);
        Func<object?, object?> oper = bue.UnaryOperator switch
        {
            UnaryOperator.Identity => (object? obj) => obj,
            UnaryOperator.Negation => (object? obj) => !(bool)obj!,
            UnaryOperator.Inverse => (object? obj) => obj is long l ? -l : -(decimal)obj!,
            _ => throw new Exception("unable to evaluate unary expression")
        };

        return oper(expression);
    }

    private object? EvaluateBinaryExpression(BoundBinaryExpression bbe, EvaluationContext context)
    {
        var left = Evaluate(bbe.Left, context);
        var right = Evaluate(bbe.Right, context);

        Func<object?, object?, object?> oper;
        if (bbe.Left.Type == LangDefaults.Types.Integer)
        {
            oper = bbe switch
            {
                { BinaryOperator: BinaryOperator.LogicAnd } => (object? left, object? right) => (bool)left! && (bool)right!,
                { BinaryOperator: BinaryOperator.LogicOr } => (object? left, object? right) => (bool)left! || (bool)right!,

                { BinaryOperator: BinaryOperator.Add } => (object? left, object? right) => (long)left! + (long)right!,
                { BinaryOperator: BinaryOperator.Sub } => (object? left, object? right) => (long)left! - (long)right!,
                { BinaryOperator: BinaryOperator.Mul } => (object? left, object? right) => (long)left! * (long)right!,
                { BinaryOperator: BinaryOperator.Div } => (object? left, object? right) => (long)left! / (long)right!,
                { BinaryOperator: BinaryOperator.Mod } => (object? left, object? right) => (long)left! % (long)right!,
                { BinaryOperator: BinaryOperator.Equality } => (object? left, object? right) => (long)left! == (long)right!,
                { BinaryOperator: BinaryOperator.Inequality } => (object? left, object? right) => (long)left! != (long)right!,
                { BinaryOperator: BinaryOperator.Greather } => (object? left, object? right) => (long)left! > (long)right!,
                { BinaryOperator: BinaryOperator.Less } => (object? left, object? right) => (long)left! < (long)right!,
                { BinaryOperator: BinaryOperator.GreatherEquals } => (object? left, object? right) => (long)left! >= (long)right!,
                { BinaryOperator: BinaryOperator.LessEquals } => (object? left, object? right) => (long)left! <= (long)right!,
                _ => throw new Exception("unable to evaluate expression")
            };
        }
        else
        {
            oper = bbe switch
            {
                { BinaryOperator: BinaryOperator.LogicAnd } => (object? left, object? right) => (bool)left! && (bool)right!,
                { BinaryOperator: BinaryOperator.LogicOr } => (object? left, object? right) => (bool)left! || (bool)right!,

                { BinaryOperator: BinaryOperator.Add } => (object? left, object? right) => (decimal)left! + (decimal)right!,
                { BinaryOperator: BinaryOperator.Sub } => (object? left, object? right) => (decimal)left! - (decimal)right!,
                { BinaryOperator: BinaryOperator.Mul } => (object? left, object? right) => (decimal)left! * (decimal)right!,
                { BinaryOperator: BinaryOperator.Div } => (object? left, object? right) => (decimal)left! / (decimal)right!,
                { BinaryOperator: BinaryOperator.Mod } => (object? left, object? right) => (decimal)left! % (decimal)right!,
                { BinaryOperator: BinaryOperator.Equality } => (object? left, object? right) => (decimal)left! == (decimal)right!,
                { BinaryOperator: BinaryOperator.Inequality } => (object? left, object? right) => (decimal)left! != (decimal)right!,
                { BinaryOperator: BinaryOperator.Greather } => (object? left, object? right) => (decimal)left! > (decimal)right!,
                { BinaryOperator: BinaryOperator.Less } => (object? left, object? right) => (decimal)left! < (decimal)right!,
                { BinaryOperator: BinaryOperator.GreatherEquals } => (object? left, object? right) => (decimal)left! >= (decimal)right!,
                { BinaryOperator: BinaryOperator.LessEquals } => (object? left, object? right) => (decimal)left! <= (decimal)right!,
                _ => throw new Exception("unable to evaluate expression")
            };
        }

        return oper(left, right);

    }

    private object? EvaluateLiteral(BoundLiteralExpression ble, EvaluationContext context)
    {
        return ble.Value;
    }
}
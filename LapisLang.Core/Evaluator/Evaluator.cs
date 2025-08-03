

namespace LapisLang.Core;
public class Evaluator
{
    private readonly DiagnosticsBag _diagnostics = new();
    public EvaluationResult Evaluate(Symbol symbol, ScopeSymbol scope)
    {
        object? value = null;
        switch (symbol)
        {
            case ExpressionSymbol es:
                value = EvaluateExpressionSymbol(es, scope);
                break;

            case StatementSymbol ss:
                EvaluateStatementSymbol(ss, scope);
                value = null;
                break;
        }

        return new EvaluationResult(value, _diagnostics);
    }

    private object? EvaluateStatementSymbol(StatementSymbol ss, ScopeSymbol scope)
    {
        switch (ss)
        {
            case VariableDeclarationSymbol vds:
                EvaluateVariableDeclaration(vds, scope);
                break;
            case ScopeStatementSymbol sss:
                return EvaluateScopeStatement(sss, scope);

            case ReturnStatementSymbol rss:
                return EvaluateExpressionSymbol(rss.Expression, scope);
        }

        return null;
    }

    private object? EvaluateScopeStatement(ScopeStatementSymbol sss, ScopeSymbol scope)
    {
        foreach (var statement in sss.Statements)
        {
            var returnValue = EvaluateStatementSymbol(statement, scope);
            if (statement is ReturnStatementSymbol) return returnValue;
        }
        return null;
    }

    private void EvaluateVariableDeclaration(VariableDeclarationSymbol vds, ScopeSymbol scope)
    {
        if (vds.Symbol is TypeSymbol) return;
        if (vds.Symbol is FuncSymbol) return;
        var value = EvaluateExpressionSymbol(vds.Symbol, scope);
        Symbol symbol;
        if (value is TypeSymbol typeSymbol)
        {
            typeSymbol.TypeName = vds.Name;
            symbol = typeSymbol;
        }
        else
        {
            symbol = new ValueSymbol(value, vds.Symbol.Type);
        }
        scope.SetSymbol(vds.Name, symbol);
    }

    private object? EvaluateExpressionSymbol(ExpressionSymbol es, ScopeSymbol scope)
    {
        switch (es)
        {
            case ValueSymbol vs: return EvaluateValueSymbol(vs);
            case BinaryExpressionSymbol bss: return EvaluateBinarySymbol(bss, scope);
            case UnaryExpressionSymbol ues: return EvaluateUnarySymbol(ues, scope);
            case NameExpressionSymbol nes: return EvaluateNameSymbol(nes, scope);
            case MemberExpressionSymbol mes: return EvaluateMemberExpression(mes, scope);
            case InstanceInitializeSymbol iis: return EvaluateInstaceInitializeSymbol(iis, scope);
            case CallSymbol cs: return EvaluateCallSymbol(cs, scope);
            case TypeSymbol ts: return EvaluateTypeSymbol(ts, scope);
        }
        return null;
    }

    private TypeSymbol EvaluateTypeSymbol(TypeSymbol typeSymbol, ScopeSymbol scope)
    {
        if (!typeSymbol.IsGeneric) return typeSymbol;
        var evaluatedType = new TypeSymbol(typeSymbol.TypeName);

        foreach (var field in typeSymbol.GetSymbols().OfType<FieldSymbol>())
        {
            switch (field.Expression)
            {
                case TypeSymbol ts:
                    var evaluatedTypeSymbol = EvaluateTypeSymbol(ts, scope);
                    evaluatedType.DefineSymbol(field.Name, new FieldSymbol(evaluatedType, field.Name, evaluatedTypeSymbol));
                    break;
                case NameExpressionSymbol nes:
                    var evaluatedName = EvaluateExpressionSymbol(nes, scope);
                    evaluatedType.DefineSymbol(field.Name, new FieldSymbol(evaluatedType, field.Name, evaluatedName is TypeSymbol s ? s : new ValueSymbol(evaluatedName, nes.Type) ));
                    break;
            }
        }

        return evaluatedType;
    }

    private object? EvaluateCallSymbol(CallSymbol cs, ScopeSymbol scope)
    {
        var derived = scope.Derive();
        foreach (var arg in cs.Arguments)
        {
            var argumentValue = EvaluateExpressionSymbol(arg.Expression, derived);
            derived.DefineSymbol(arg.Name, argumentValue is Symbol argSymbol ? argSymbol : new ValueSymbol(argumentValue, arg.Expression.Type));
        }

        return EvaluateStatementSymbol(cs.Function.Statement, derived);
    }

    private object? EvaluateMemberExpression(MemberExpressionSymbol mes, ScopeSymbol scope)
    {
        var value = EvaluateExpressionSymbol(mes.Expression, scope);
        if (value is ValueSymbol s) value = s.Value;

        if (value is not Dictionary<string, object?> instance)
        {
            throw new Exception("value should be a instance");
        }

        return instance.GetValueOrDefault(mes.FieldSymbol.Name);
    }

    private object? EvaluateInstaceInitializeSymbol(InstanceInitializeSymbol iis, ScopeSymbol scope)
    {
        return iis.Initializers.ToDictionary(e => e.Key.Name, e => EvaluateExpressionSymbol(e.Value, scope));
    }

    private object? EvaluateNameSymbol(NameExpressionSymbol nes, ScopeSymbol scope)
    {
        var symbolToEvaluate = nes.Symbol;
        if (nes.Symbol is NameSymbol ns)
        {
            scope.GetSymbol(ns.Name, out var symbol);
            return symbol;
        }
        return symbolToEvaluate switch
            {
                ValueSymbol vs => vs.Value,
                _ => nes.Symbol
            };
    }

    private object? EvaluateUnarySymbol(UnaryExpressionSymbol ues, ScopeSymbol scope)
    {
        var value = EvaluateExpressionSymbol(ues.Expression, scope);
        Func<object?, object?> op;

        if (ues.Expression.Type == DefaultSymbols.Types.Integer)
        {
            op = ues.UnaryOp switch
            {
                UnaryOperatorKind.Identity => (object? obj) => obj,
                UnaryOperatorKind.Inverse => (object? obj) => -(long)obj,
                _ => (object? obj) => 0
            };
        }
        else if (ues.Expression.Type == DefaultSymbols.Types.Decimal)
        {
            op = ues.UnaryOp switch
            {
                UnaryOperatorKind.Identity => (object? obj) => obj,
                UnaryOperatorKind.Inverse => (object? obj) => -(decimal)obj,
                _ => (object? obj) => 0
            };
        }
        else
        {
            op = ues.UnaryOp switch
            {
                UnaryOperatorKind.Negation => (object? obj) => !(bool)obj,
                _ => (object? obj) => obj
            };
        }

        return op(value);
    }

    private object? EvaluateBinarySymbol(BinaryExpressionSymbol bss, ScopeSymbol scope)
    {
        var left = EvaluateExpressionSymbol(bss.Left, scope);
        var right = EvaluateExpressionSymbol(bss.Right, scope);

        if (left is ValueSymbol ls) left = ls.Value;
        if (right is ValueSymbol rs) right = rs.Value;


        Func<object?, object?, object?> op;
        if (bss.Left.Type == DefaultSymbols.Types.Integer)
        {
            op = bss.BinaryOp switch
            {
                BinaryOperatorKind.Add => (object? left, object? right) => (long)left + (long)right,
                BinaryOperatorKind.Sub => (object? left, object? right) => (long)left - (long)right,
                BinaryOperatorKind.Mul => (object? left, object? right) => (long)left * (long)right,
                BinaryOperatorKind.Div => (object? left, object? right) => (long)left / (long)right,
                BinaryOperatorKind.Mod => (object? left, object? right) => (long)left % (long)right,
                BinaryOperatorKind.Equality => (object? left, object? right) => (long)left == (long)right,
                BinaryOperatorKind.Inequality => (object? left, object? right) => (long)left != (long)right,
                BinaryOperatorKind.GreatherThan => (object? left, object? right) => (long)left > (long)right,
                BinaryOperatorKind.GreatherOrEqual => (object? left, object? right) => (long)left >= (long)right,
                BinaryOperatorKind.LessThan => (object? left, object? right) => (long)left < (long)right,
                BinaryOperatorKind.LessOrEqual => (object? left, object? right) => (long)left <= (long)right,
                _ => (object? left, object? right) => 0
            };
        }
        else if (bss.Left.Type == DefaultSymbols.Types.Decimal)
        {
            op = bss.BinaryOp switch
            {
                BinaryOperatorKind.Add => (object? left, object? right) => (decimal)left + (decimal)right,
                BinaryOperatorKind.Sub => (object? left, object? right) => (decimal)left - (decimal)right,
                BinaryOperatorKind.Mul => (object? left, object? right) => (decimal)left * (decimal)right,
                BinaryOperatorKind.Div => (object? left, object? right) => (decimal)left / (decimal)right,
                BinaryOperatorKind.Mod => (object? left, object? right) => (decimal)left % (decimal)right,
                BinaryOperatorKind.Equality => (object? left, object? right) => (decimal)left == (decimal)right,
                BinaryOperatorKind.Inequality => (object? left, object? right) => (decimal)left != (decimal)right,
                BinaryOperatorKind.GreatherThan => (object? left, object? right) => (decimal)left > (decimal)right,
                BinaryOperatorKind.GreatherOrEqual => (object? left, object? right) => (decimal)left >= (decimal)right,
                BinaryOperatorKind.LessThan => (object? left, object? right) => (decimal)left < (decimal)right,
                BinaryOperatorKind.LessOrEqual => (object? left, object? right) => (decimal)left <= (decimal)right,
                _ => (object? left, object? right) => 0
            };
        }
        else
        {
            op = bss.BinaryOp switch
            {
                BinaryOperatorKind.LogicOr => (object? left, object? right) => (bool)left || (bool)right,
                BinaryOperatorKind.LogicAnd => (object? left, object? right) => (bool)left && (bool)right,
                BinaryOperatorKind.Equality => (object? left, object? right) => (bool)left == (bool)right,
                BinaryOperatorKind.Inequality => (object? left, object? right) => (bool)left != (bool)right,
                _ => (object? left, object? right) => 0
            };
        }


        return op(left, right);
    }

    private object? EvaluateValueSymbol(ValueSymbol vs)
    {
        return vs.Value;
    }
}
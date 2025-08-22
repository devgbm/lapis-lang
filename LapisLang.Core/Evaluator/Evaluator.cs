



using System.Collections.Immutable;

namespace LapisLang.Core;


public class LapisEvaluator
{
    private static LapisEvaluator? _instnce;
    private bool _returnRequest = false;

    public static LapisEvaluator Instance
    {
        get
        {
            if (_instnce is null) _instnce = new();
            return _instnce;
        }
    }
    private LapisEvaluator()
    {
    }

    public Symbol Evaluate(Symbol symbol, BoundScope? scope = null)
    {
        return Evaluate(symbol, EvaluationScope.CreateScope(scope));
    }

    public Symbol Evaluate(Symbol symbol, EvaluationScope? scope = null)
    {
        var evaluationScope = scope ?? EvaluationScope.CreateScope();
        switch (symbol)
        {
            case ExprSymbol es: return EvaluateExpression(es, evaluationScope);
            case StatementSymbol ss: return EvaluateStatement(ss, evaluationScope);
            default: return Symbol.Unkown;
        }
    }

    public Symbol EvaluateStatement(StatementSymbol syntax, EvaluationScope evaluationScope)
    {
        switch (syntax)
        {
            case DefineSymbol ds: return EvaluateDefineSymbol(ds, evaluationScope);
            case ScopeSymbol ss: return EvaluateScopeSymbol(ss, evaluationScope);
            case ReturnSymbol rs: return EvaluateReturnSymbol(rs, evaluationScope); 
            case IfSymbol ifs: return EvaluateIfSymbol(ifs, evaluationScope);
            case VoidStatement vs: return vs;

            default:
                throw new Exception("Unkown or unsuported statement.");
        }
        
    }

    private Symbol EvaluateReturnSymbol(ReturnSymbol symbol, EvaluationScope evaluationScope)
    {
        _returnRequest = true;
        return EvaluateExpression(symbol.Expression, evaluationScope);
    }

    private Symbol EvaluateIfSymbol(IfSymbol symbol, EvaluationScope evaluationScope)
    {
        var result = EvaluateExpression(symbol.ConditionExpression, evaluationScope);
        if (result is not BooleanSymbol bs) throw new Exception("If condition must be a boolean symbol");

        var derivedScope = evaluationScope.Derive();
        if (bs.Value == true) return EvaluateStatement(symbol.Statements, evaluationScope);
        else if(symbol.ElseStatements is not null) return EvaluateStatement(symbol.ElseStatements, evaluationScope);

        return VoidSymbol.Instance;   
    }

    private Symbol EvaluateScopeSymbol(ScopeSymbol ss, EvaluationScope evaluationScope)
    {
        foreach (var statement in ss.StatementSymbols)
        {
            var returnSymbol = EvaluateStatement(statement, evaluationScope);
            if (_returnRequest)
            {
                _returnRequest = false;
                return returnSymbol;
            }
        }
        return VoidSymbol.Instance;
    }

    private Symbol EvaluateDefineSymbol(DefineSymbol ds, EvaluationScope evaluationScope)
    {
        if (ds.Expression.IsCompileTime) return VoidSymbol.Instance;

        return VoidSymbol.Instance;
    }
    public ExprSymbol EvaluateExpression(ExprSymbol expr, BoundScope scope) => EvaluateExpression(expr, EvaluationScope.CreateScope(scope));

    public ExprSymbol EvaluateExpression(ExprSymbol expr, EvaluationScope scope)
    {
        switch (expr)
        {
            case IntegerSymbol:
            case BooleanSymbol:
            case DecimalSymbol:
            case StringSymbol:
            case FuncSymbol:
                return expr;

            case TypeSymbol ts:
                return EvaluateTypeSymbol(ts, scope);

            case InstanceSymbol ins:
                return EvaluateInstanceSymbol(ins, scope);
                
            case NameSymbol ns:
                return EvaluateNameSymbol(ns, scope);

            case MemberSymbol ms:
                return EvaluateMemberSymbol(ms, scope);

            case CallSymbol cs:
                return EvaluateCallSymbol(cs, scope);

            case BinaryExprSymbol bes:
                return EvaluateBinaryExpr(bes, scope);
            
            case UnaryExprSymbol ues:
                return EvaluateUnaryExpr(ues, scope);

            default: return ExprSymbol.Unkown;
        }
    }

    private ExprSymbol EvaluateUnaryExpr(UnaryExprSymbol ues, EvaluationScope scope)
    {
        var expression = EvaluateExpression(ues.Operand, scope);
        var operFn = scope.GetOperatorFn(expression.Type, ues.UnaryOperator);
        return operFn(expression);
    }

    private ExprSymbol EvaluateTypeSymbol(TypeSymbol ts, EvaluationScope scope)
    {
        if (ts is not StructTypeSymbol sts) return ts;

        var fields = ImmutableArray.CreateBuilder<FieldSymbol>();

        foreach (var field in sts.Fields)
        {
            var type = EvaluateExpression(field.TypeExpression, scope);
            fields.Add(new FieldSymbol(field.Name, type, type.IsCompileTime));
        }

        return new StructTypeSymbol(fields.ToImmutableArray(),"anonimous-type");
    }

    private ExprSymbol EvaluateInstanceSymbol(InstanceSymbol ins, EvaluationScope scope)
    {
        var attributes = ImmutableDictionary.CreateBuilder<string, ExprSymbol>();
        foreach (var attribute in ins.Atributes)
        {
            var evaluated = EvaluateExpression(attribute.Value, scope);
            attributes.Add(attribute.Key, evaluated);
        }
        return new InstanceSymbol(attributes.ToImmutableDictionary(), ins.Type);
    }

    private ExprSymbol EvaluateCallSymbol(CallSymbol cs, EvaluationScope scope)
    {
        var derivedScope = scope.Derive();

        foreach (var argument in cs.Arguments)
        {
            var argumentExpr = EvaluateExpression(argument.Expression, derivedScope);
            derivedScope.Define(argument.Name, argumentExpr);
        }

        var returnedSymbol = EvaluateStatement(cs.Function.Statement, derivedScope);
        if (returnedSymbol is not ExprSymbol expr) throw new Exception("Call should return a expression");
        return expr;
    }

    private ExprSymbol EvaluateMemberSymbol(MemberSymbol ms, EvaluationScope scope)
    {
        var expression = EvaluateExpression(ms.Expression, scope);
        if (expression is InstanceSymbol instance)
        {
            return instance.Atributes.GetValueOrDefault(ms.Name, ExprSymbol.Unkown);
        }
        return ExprSymbol.Unkown;
    }

    private ExprSymbol EvaluateNameSymbol(NameSymbol ns, EvaluationScope scope)
    {
        scope.BoundScope.TryGetSymbol(ns.Name, out var symbol);
        return symbol as ExprSymbol ?? ExprSymbol.Unkown;
    }

    private ExprSymbol EvaluateBinaryExpr(BinaryExprSymbol bes, EvaluationScope scope)
    {
        var left = EvaluateExpression(bes.Left, scope);
        var right = EvaluateExpression(bes.Right, scope);

        var operatorFn = scope.GetOperatorFn(left.Type, right.Type, bes.BinaryOperator);
        return operatorFn(left, right);
    }
}
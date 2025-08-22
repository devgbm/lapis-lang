

using System.Collections.Immutable;


namespace LapisLang.Core;


public class Binder
{
    private TypeSymbol _expectedReturnType;

    public DiagnosticsBag Diagnostics { get; } = new();
    public Symbol Bind(Syntax syntax, BoundScope scope)
    {
        switch (syntax)
        {
            case ExpressionSyntax es: return BindExpressionSyntax(es, scope);
            case StatementSyntax ss: return BindStatementSyntax(ss, scope);
            default:
                Diagnostics.Report("Unkown expression", syntax);
                return Symbol.Unkown;
        }
    }

    private StatementSymbol BindStatementSyntax(StatementSyntax syntax, BoundScope scope)
    {
        switch (syntax)
        {
            case DefineStatementSyntax dss: return BindDefineStatementSyntax(dss, scope);
            case ScopeStatementSyntax sss: return BindScopeStatementSyntax(sss, scope);
            case ReturnStatementSyntax rss: return BindReturnStatementSyntax(rss, scope);
            default:
                Diagnostics.Report("unkown statement", syntax);
                return StatementSymbol.UnkownStatement;
        }
        throw new NotImplementedException();
    }

    private StatementSymbol BindReturnStatementSyntax(ReturnStatementSyntax rss, BoundScope scope)
    {
        var returnSymbol = BindExpressionSyntax(rss.Expression, scope);
        if (returnSymbol.Type != _expectedReturnType)
        {
            Diagnostics.Report("wrong return type", rss.Expression);
        }
        return new ReturnSymbol(returnSymbol);
    }

    private StatementSymbol BindScopeStatementSyntax(ScopeStatementSyntax sss, BoundScope scope)
    {
        var statements = ImmutableArray.CreateBuilder<StatementSymbol>();
        var returnType = LangDefaults.Types.Void;
        
        foreach (var statement in sss.Statements)
        {
            var boundStatement = BindStatementSyntax(statement, scope);
            statements.Add(boundStatement);
        }

        if (_expectedReturnType != LangDefaults.Types.Void && sss.Statements.Length == 0)
        {
            Diagnostics.Report("function expect return", sss);
        }

        return new ScopeSymbol(statements.ToImmutableArray(), returnType);
    }

    private StatementSymbol BindDefineStatementSyntax(DefineStatementSyntax dss, BoundScope scope)
    {
        var name = dss.Identifier.String;
        var expression = BindExpressionSyntax(dss.Expression, scope);

        if (expression.IsConstant)
        {
            expression = (ExprSymbol)LapisEvaluator.Instance.Evaluate(expression, EvaluationScope.CreateScope(scope));
            if (expression is TypeSymbol ts && ts.DebugName == "anonimous-type") ts.DebugName = name;
        }

        if (!scope.Define(name, expression))
        {
            Diagnostics.Report("Symbol canot be redeclared", dss);
            return StatementSymbol.UnkownStatement;
        }

        return new DefineSymbol(name, expression);
    }

    private ExprSymbol BindExpressionSyntax(ExpressionSyntax syntax, BoundScope scope)
    {
        switch (syntax)
        {
            case LiteralExpressionSyntax les: return BindLiteralExpression(les, scope);
            case TypeExpressionSyntax tes: return BindTypeExpressionSyntax(tes, scope);
            case BinaryExpressionSyntax bes: return BindBinaryExpressionSyntax(bes, scope);
            case NameExpressionSyntax nes: return BindNameExpressionSyntax(nes, scope);
            case InstanceInitializationExpression iie: return BindInstanceInitializationSyntax(iie, scope);
            case MemberExpressionSyntax mes: return BindMemberExpressionSyntax(mes, scope);
            case FuncExpressionSyntax fes: return BindFunctionExpressionSyntax(fes, scope);
            default:
                Diagnostics.Report("Unkown expression", syntax);
                return ExprSymbol.Unkown;
        }
    }

    private ExprSymbol BindFunctionExpressionSyntax(FuncExpressionSyntax fes, BoundScope scope)
    {
        var derivedScope = scope.Derive();
        var parameters = ImmutableArray.CreateBuilder<ArgumentSymbol>();
        foreach (var parameter in fes.Parameters)
        {
            var typeSymbol = BindExpressionSyntax(parameter.TypeName, scope);
            if (typeSymbol is not ExprSymbol es)
            {
                Diagnostics.Report("Symbol should evaluate to expression", parameter.TypeName);
                es = ExprSymbol.Unkown;
            }

            if (!es.IsConstant)
            {
                Diagnostics.Report("Parameter type should be a constant value", parameter.TypeName);
                es = ExprSymbol.Unkown;
            }

            var evaluated = LapisEvaluator.Instance.Evaluate(es, scope);
            if (evaluated is not TypeSymbol evaluatedType)
            {
                Diagnostics.Report("Parameter type shoudle evaluate to type", parameter.TypeName);
                evaluatedType = LangDefaults.Types.Unkown;
            }

            parameters.Add(new ArgumentSymbol(parameter.Name.Name.String, evaluatedType));


            var nameSymbol = new NameSymbol(parameter.Name.Name.String, evaluatedType, true);
            if (!derivedScope.Define(parameter.Name.Name.String, nameSymbol))
            {
                Diagnostics.Report("Duplicate parameter name", parameter);
            }
        }

        var returnSymbol = BindExpressionSyntax(fes.ReturnType, derivedScope);
        TypeSymbol returnType;

        if (!returnSymbol.IsConstant)
        {
            Diagnostics.Report("return type should be a compile time constant", fes.ReturnType);
            returnType = LangDefaults.Types.Unkown;

        }
        else
        {
            if (returnSymbol.Type != LangDefaults.Types.Type)
            {
                Diagnostics.Report("return type should evaluate to a type", fes.ReturnType);
                returnType = LangDefaults.Types.Unkown;
            }
            else
            {
                var concreteType = LapisEvaluator.Instance.EvaluateExpression(returnSymbol, derivedScope) as TypeSymbol;
                _expectedReturnType = concreteType ?? LangDefaults.Types.Unkown;
                returnSymbol = _expectedReturnType;
            }
        }

        var statement = BindStatementSyntax(fes.Statement, derivedScope);
        _expectedReturnType = LangDefaults.Types.Void;


        return new FuncSymbol(statement, parameters.ToImmutableArray(), returnSymbol);
    }

    private ExprSymbol BindMemberExpressionSyntax(MemberExpressionSyntax mes, BoundScope scope)
    {
        var expression = BindExpressionSyntax(mes.Expresison, scope);

        var memberExpression = expression.Type.GetMember(mes.Member.Name.String);
        if (memberExpression == ExprSymbol.Unkown)
        {
            Diagnostics.Report("unkown member", mes.Member);
            return ExprSymbol.Unkown;
        }

        return new MemberSymbol(expression, memberExpression.Type, mes.Member.Name.String);
    }

    private ExprSymbol BindInstanceInitializationSyntax(InstanceInitializationExpression syntax, BoundScope scope)
    {
        TypeSymbol type;
        var atributes = ImmutableDictionary.CreateBuilder<string, ExprSymbol>();
        if (syntax.IsAnonimous)
        {
            var typeArray = ImmutableArray.CreateBuilder<FieldSymbol>();

            foreach (var initializer in syntax.Initializers)
            {
                var expression = BindExpressionSyntax(initializer.Expression, scope);
                var initializerName = initializer.Identifier.String;
                typeArray.Add(new FieldSymbol(initializerName, expression.Type, expression.IsConstant));
                if (atributes.ContainsKey(initializerName))
                {
                    Diagnostics.Report("duplicate identifier", initializer.Identifier);
                }
                else
                {
                    var initializerValue = LapisEvaluator.Instance.Evaluate(expression, EvaluationScope.CreateScope(scope)) as ExprSymbol ?? ExprSymbol.Unkown;
                    atributes.Add(initializerName, initializerValue);
                }
            }

            type = new StructTypeSymbol(typeArray.ToImmutableArray(), "anonimous-type");
        }
        else
        {
            if (!scope.TryGetTypeSymbol(syntax.TypeName.SourceSpan.AsText.ToString(), out type)) Diagnostics.Report("unkown type", syntax.TypeName);
            if (type is not StructTypeSymbol sts)
            {
                Diagnostics.Report("type is a struct type", syntax);
                return ExprSymbol.Unkown;
            }

            foreach (var initializer in syntax.Initializers)
            {
                var expression = BindExpressionSyntax(initializer.Expression, scope);
                var initializerName = initializer.Identifier.String;

                if (!sts.Fields.Any(e => e.Name == initializerName && e.Type == expression.Type))
                {
                    Diagnostics.Report("property does not exist on type", initializer);
                }
                if (atributes.ContainsKey(initializerName))
                {
                    Diagnostics.Report("duplicate identifier", initializer.Identifier);
                }
                else
                {
                    var initializerValue = LapisEvaluator.Instance.Evaluate(expression, EvaluationScope.CreateScope(scope)) as ExprSymbol ?? ExprSymbol.Unkown;
                    atributes.Add(initializerName, initializerValue);
                }
            }
        }
        return new InstanceSymbol(atributes.ToImmutableDictionary(), type);
    }

    private ExprSymbol BindNameExpressionSyntax(NameExpressionSyntax nes, BoundScope scope)
    {
        if (!scope.TryGetExprSymbol(nes.Name.String, out var symbol))
        {
            Diagnostics.Report("Undefined name", nes);
        }
        var constantSymbol = symbol == ExprSymbol.Unkown ? false : true;
        return new NameSymbol(nes.Name.String, symbol.Type, constantSymbol);
    }

    private ExprSymbol BindBinaryExpressionSyntax(BinaryExpressionSyntax bes, BoundScope scope)
    {
        var leftExpr = BindExpressionSyntax(bes.Left, scope);
        var rightExpr = BindExpressionSyntax(bes.Right, scope);

        var (binaryOp, resultType) = scope.ResolveBinaryExpression(bes.OperatorToken, leftExpr, rightExpr);

        return new BinaryExprSymbol(leftExpr, rightExpr, binaryOp, resultType);
    }
    private ExprSymbol BindTypeExpressionSyntax(TypeExpressionSyntax tes, BoundScope scope)
    {
        var fieldsArr = ImmutableArray.CreateBuilder<FieldSymbol>();

        foreach (var fieldSyntax in tes.Fields)
        {
            var fieldName = fieldSyntax.Identifier.String;
            var expression = BindExpressionSyntax(fieldSyntax.Expression, scope);

            TypeSymbol fieldType;
            
            var evaluated = LapisEvaluator.Instance.Evaluate(expression, EvaluationScope.CreateScope(scope)) as TypeSymbol;
            if (evaluated is not TypeSymbol ets)
            {
                Diagnostics.Report("Expression should evatuale to a type symbol", fieldSyntax.Expression);
                fieldType = LangDefaults.Types.Unkown;
            }
            else
            {
                fieldType = ets;
            }

            fieldsArr.Add(new FieldSymbol(fieldName, fieldType, expression.IsConstant));
        }

        return new StructTypeSymbol(fieldsArr.ToImmutableArray(), "anonimous-type");
    }

    private ExprSymbol BindLiteralExpression(LiteralExpressionSyntax les, BoundScope scope)
    {
        switch (les.LiteralType)
        {
            case LiteralType.Integer:
                return new IntegerSymbol(long.Parse(les.SourceSpan.AsText));

            case LiteralType.Boolean:
                return new BooleanSymbol(bool.Parse(les.SourceSpan.AsText));

            case LiteralType.String:
                return new StringSymbol(les.SourceSpan.AsText.ToString());

            case LiteralType.Decimal:
                return new DecimalSymbol(decimal.Parse(les.SourceSpan.AsText));

            default:
                Diagnostics.Report("Unkown expression", les);
                return ExprSymbol.Unkown;
        }
    }
}
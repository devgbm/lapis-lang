

using System.Collections.Immutable;


namespace LapisLang.Core;


public class Binder
{
    private bool _allowTypeReference;
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
            case MemberDefineStatementSyntax mdss: return BindMemberDefineStatement(mdss, scope);
            case DefineStatementSyntax dss: return BindDefineStatementSyntax(dss, scope);
            case ScopeStatementSyntax sss: return BindScopeStatementSyntax(sss, scope);
            case IfStatementSyntax iss: return BindIfStatementSyntax(iss, scope);
            case ReturnStatementSyntax rss: return BindReturnStatementSyntax(rss, scope);
            case VarStatementSyntax vss: return BindVariableDeclaration(vss, scope);
            case AssingStatementSyntax ass: return BindAssignSyntax(ass, scope);
            case ExpressionStatementSyntax ess:
                var expr =  BindExpressionSyntax(ess.Expression,scope);
                return new ExpressionStatementSymbol(expr);
            default:
                Diagnostics.Report("unkown statement", syntax);
                return StatementSymbol.UnkownStatement;
        }
        throw new NotImplementedException();
    }

    private StatementSymbol BindAssignSyntax(AssingStatementSyntax ass, BoundScope scope)
    {
        if (!scope.TryGetSymbol(ass.Identifier.String, out var symbol) || symbol is not NameSymbol ns)
        {
            Diagnostics.Report("unkown name", ass.Identifier);
            return StatementSymbol.UnkownStatement;
        }
        var boundExpression = BindExpressionSyntax(ass.Expression, scope);

        if (ns.Type != boundExpression.Type)
        {
            Diagnostics.Report("expression is not assignable to variable type", ass.Expression);
            return StatementSymbol.UnkownStatement;
        }

        return new AssingSymbol(ass.Identifier.String, boundExpression);
    }

    private StatementSymbol BindVariableDeclaration(VarStatementSyntax vss, BoundScope scope)
    {
        var expression = BindExpressionSyntax(vss.Expression, scope);

        if (expression.Type == LangDefaults.Types.Type)
        {
            Diagnostics.Report("variable cannot reference types", vss.Expression);
            expression = UnkownExprSymbol.Unkown;
        }

        var nameSymbol = new NameSymbol(vss.Identifier.String, expression.Type, expression.IsCompileTime);

        if (!scope.Define(vss.Identifier.String, nameSymbol))
        {
            Diagnostics.Report("variable already exists in current scope", vss.Identifier);
            expression = UnkownExprSymbol.Unkown;
        }

        return new VarSymbol(vss.Identifier.String, expression);
    }

    private StatementSymbol BindIfStatementSyntax(IfStatementSyntax iss, BoundScope scope)
    {
        var expression = BindExpressionSyntax(iss.Conditional, scope);
        
        if (expression.Type != LangDefaults.Types.Boolean)
        {
            Diagnostics.Report("if conditional must evaluate to a boolean type", iss.Conditional);
        }
        var statements = BindStatementSyntax(iss.Statement, scope.Derive());
        var elseStatements = iss.ElseStatement is not null ? BindStatementSyntax(iss.ElseStatement, scope.Derive()) : null;
        return new IfSymbol(expression, statements, elseStatements);
    }

    private StatementSymbol BindReturnStatementSyntax(ReturnStatementSyntax rss, BoundScope scope)
    {
        var returnSymbol = BindExpressionSyntax(rss.Expression, scope);
        if ((scope._expectedReturn != LangDefaults.Types.Function && returnSymbol.Type != scope._expectedReturn) || (scope._expectedReturn == LangDefaults.Types.Function && returnSymbol is not FuncSymbol))
        {
            Diagnostics.Report("wrong return type", rss.Expression);
        }
        return new ReturnSymbol(returnSymbol);
    }

    private StatementSymbol BindScopeStatementSyntax(ScopeStatementSyntax sss, BoundScope scope)
    {
        var statements = ImmutableArray.CreateBuilder<StatementSymbol>();
        var returnType = scope._expectedReturn ?? LangDefaults.Types.Void;
        
        foreach (var statement in sss.Statements)
        {
            var boundStatement = BindStatementSyntax(statement, scope);
            statements.Add(boundStatement);
        }

        if (scope._expectedReturn != LangDefaults.Types.Void && sss.Statements.Length == 0)
        {
            Diagnostics.Report("function expect return", sss);
        }

        return new ScopeSymbol(statements.ToImmutableArray(), returnType);
    }

    private StatementSymbol BindMemberDefineStatement(MemberDefineStatementSyntax mdss, BoundScope scope)
    {
        if (!scope.TryGetExprSymbol(mdss.TypeName.String, out var typeExpr) || typeExpr is not TypeSymbol targetType)
        {
            Diagnostics.Report("Unknown type for method definition", mdss.TypeName);
            return StatementSymbol.UnkownStatement;
        }

        scope._selfName = mdss.MemberName.String;
        scope._selfType = targetType;

        var expression = BindExpressionSyntax(mdss.Expression, scope);

        if (expression.IsCompileTime)
            expression = (ExprSymbol)LapisEvaluator.Instance.Evaluate(expression, EvaluationScope.CreateScope(scope));

        scope._selfName = null;
        scope._selfType = null;

        if (!targetType.DefineMember(mdss.MemberName.String, expression))
        {
            Diagnostics.Report("Member already defined", mdss.MemberName);
            return StatementSymbol.UnkownStatement;
        }

        return new MemberDefineSymbol(mdss.TypeName.String, targetType, mdss.MemberName.String, expression);
    }

    private StatementSymbol BindDefineStatementSyntax(DefineStatementSyntax dss, BoundScope scope)
    {
        var name = dss.Identifier.String;
        scope._selfName = name;
        var expression = BindExpressionSyntax(dss.Expression, scope);

        if (expression.IsCompileTime)
        {
            expression = (ExprSymbol)LapisEvaluator.Instance.Evaluate(expression, EvaluationScope.CreateScope(scope));
            if (expression is TypeSymbol ts && ts.DebugName == "anonimous-type") ts.DebugName = name;
        }

        if (!scope.Define(name, expression))
        {
            Diagnostics.Report("Symbol canot be redeclared", dss);
            return StatementSymbol.UnkownStatement;
        }
        scope._selfName = null;
        return new DefineSymbol(name, expression);
    }

    private ExprSymbol BindExpressionSyntax(ExpressionSyntax syntax, BoundScope scope)
    {
        switch (syntax)
        {
            case LiteralExpressionSyntax les: return BindLiteralExpression(les, scope);
            case TypeExpressionSyntax tes: return BindTypeExpressionSyntax(tes, scope);
            case BinaryExpressionSyntax bes: return BindBinaryExpressionSyntax(bes, scope);
            case UnaryExpressionSyntax ues: return BindUnaryExpressionSyntax(ues, scope);
            case NameExpressionSyntax nes: return BindNameExpressionSyntax(nes, scope);
            case InstanceInitializationExpression iie: return BindInstanceInitializationSyntax(iie, scope);
            case MemberExpressionSyntax mes: return BindMemberExpressionSyntax(mes, scope);
            case FuncExpressionSyntax fes: return BindFunctionExpressionSyntax(fes, scope);
            case CallExpressionSyntax ces: return BindCallExpressionSyntax(ces, scope);
            case ParenthesizedExpression ps: return BindExpressionSyntax(ps.Expression, scope);

            default:
                Diagnostics.Report("Unkown expression", syntax);
                return ExprSymbol.Unkown;
        }
    }

    private ExprSymbol BindUnaryExpressionSyntax(UnaryExpressionSyntax syntax, BoundScope scope)
    {
        var operand = BindExpressionSyntax(syntax.Expression, scope);

        var (unaryOp, resultType) = scope.ResolveUnaryExpression(syntax.TokenOperator, operand);

        return new UnaryExprSymbol(operand, unaryOp, resultType);
    }

    private ExprSymbol BindCallExpressionSyntax(CallExpressionSyntax ces, BoundScope scope)
    {
        var expression = BindExpressionSyntax(ces.Expression, scope);
        if (expression.Type is not  FuncTypeSymbol fts)
        {
            Diagnostics.Report("Expression is not callable.", ces.Expression);
            return ExprSymbol.Unkown;
        }

        if (ces.Parameters.Length != fts.Parameters.Length)
        {
            Diagnostics.Report("Function arguments does not match parameter count", ces.SourceSpan);
            return ExprSymbol.Unkown;
        }


        var boundArguments = ImmutableArray.CreateBuilder<ArgumentSymbol>();

        foreach (var zip in fts.Parameters.Zip(ces.Parameters))
        {
            var boundArgument = BindExpressionSyntax(zip.Second, scope);

            if (zip.First.Expression != boundArgument.Type)
            {
                Diagnostics.Report("wrong type", zip.Second.SourceSpan);
                return ExprSymbol.Unkown;
            }
            else
            {
                boundArguments.Add(new ArgumentSymbol(zip.First.Name, boundArgument));
            }
        }

        return new CallSymbol(expression, boundArguments.ToImmutableArray(), fts.ReturnType == LangDefaults.Types.Function ? fts : fts.ReturnType);
    }

    private ExprSymbol BindFunctionExpressionSyntax(FuncExpressionSyntax fes, BoundScope scope)
    {
        var derivedScope = scope.Derive();
        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();
        var isComptimeFn = false;
        var isInstanceMethod = false;

        foreach (var parameter in fes.Parameters)
        {
            if (parameter is SelfArgumentSyntax)
            {
                isInstanceMethod = true;
                var selfType = scope._selfType ?? LangDefaults.Types.Unkown;
                derivedScope.Define("self", new NameSymbol("self", selfType, false));
                continue;
            }

            var typeSymbol = BindExpressionSyntax(parameter.TypeName, scope);
            if (typeSymbol is not ExprSymbol es)
            {
                Diagnostics.Report("Symbol should evaluate to expression", parameter.TypeName);
                es = ExprSymbol.Unkown;
            }

            if (!es.IsCompileTime)
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

            var isComptimeArg = evaluatedType == LangDefaults.Types.Type;
            if (isComptimeArg)
            {
                _allowTypeReference = true;
                isComptimeFn = true;
            }

            parameters.Add(new ParameterSymbol(parameter.Name.Name.String, evaluatedType, isComptimeArg));


            var nameSymbol = new NameSymbol(parameter.Name.Name.String, evaluatedType, true);
            if (!derivedScope.Define(parameter.Name.Name.String, nameSymbol))
            {
                Diagnostics.Report("Duplicate parameter name", parameter);
            }
        }

        var returnSymbol = BindExpressionSyntax(fes.ReturnType, derivedScope);
        TypeSymbol returnType;

        if (!returnSymbol.IsCompileTime)
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
                derivedScope._expectedReturn = concreteType ?? LangDefaults.Types.Unkown;
                returnSymbol = derivedScope._expectedReturn;
                returnType = returnSymbol.Type;
            }
        }

        var funcType = new FuncTypeSymbol(parameters.ToImmutableArray(), derivedScope._expectedReturn);

        if (scope._selfName is not null)
        {
            derivedScope.Define(scope._selfName, funcType);
        }

        derivedScope.Define("self", funcType);


        var statement = BindStatementSyntax(fes.Statement, derivedScope);
        derivedScope._expectedReturn = LangDefaults.Types.Void;

        _allowTypeReference = false;
        return new FuncSymbol(statement, parameters.ToImmutableArray(), returnType, funcType, isComptimeFn, isInstanceMethod);
    }

    private ExprSymbol BindMemberExpressionSyntax(MemberExpressionSyntax mes, BoundScope scope)
    {
        var expression = BindExpressionSyntax(mes.Expresison, scope);

        if(expression.Type == LangDefaults.Types.Namespace && expression is NameSymbol ns)
        {
            if(!scope.TryGetExprSymbol(ns.Name, out var expr) || expr is not BoundScope bs)
            {
                Diagnostics.Report("unkown member", mes.Member);
                return ExprSymbol.Unkown;
            }

            if(!bs.TryGetExprSymbol(mes.Member.Name.String, out var result))
            {
                Diagnostics.Report("unkown member", mes.Member);
                return ExprSymbol.Unkown;
            }

            return result;
        }

        var memberName = mes.Member.Name.String;

        // Static method: TypeName.method — expression is a NameSymbol with meta-type Type
        if (expression is NameSymbol nameRef && nameRef.Type == LangDefaults.Types.Type)
        {
            if (!scope.TryGetExprSymbol(nameRef.Name, out var actualTypeExpr) || actualTypeExpr is not TypeSymbol ts)
            {
                Diagnostics.Report("unkown type", mes.Expresison);
                return ExprSymbol.Unkown;
            }
            var staticMember = ts.GetMember(memberName);
            if (staticMember == ExprSymbol.Unkown)
            {
                Diagnostics.Report("unkown member", mes.Member);
                return ExprSymbol.Unkown;
            }
            var staticMemberType = staticMember is TypeSymbol sft ? sft : staticMember.Type;
            return new MemberSymbol(expression, staticMemberType, memberName, staticMember.IsCompileTime);
        }

        // Instance member (struct field or instance method)
        var member = expression.Type.GetMember(memberName);
        if (member == ExprSymbol.Unkown)
        {
            Diagnostics.Report("unkown member", mes.Member);
            return ExprSymbol.Unkown;
        }
        // Struct fields are stored as TypeSymbol (the field's type); methods are stored as FuncSymbol.
        // For TypeSymbol members use it directly; for FuncSymbol use its FuncTypeSymbol.
        var memberType = member is TypeSymbol fieldType ?
            fieldType :
            member is  MemberSymbol ms
            ? ms.Expression.Type
            : member.Type;
        return new MemberSymbol(expression, memberType, memberName, member.IsCompileTime);
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
                typeArray.Add(new FieldSymbol(initializerName, expression.Type, expression.IsCompileTime));
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
        return new NameSymbol(nes.Name.String, symbol is FuncTypeSymbol fts? fts : symbol.Type, constantSymbol);
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
            var typeExpression = BindExpressionSyntax(fieldSyntax.Expression, scope);
            
            var evaluated = LapisEvaluator.Instance.EvaluateExpression(typeExpression, EvaluationScope.CreateScope(scope));
            if (evaluated is not TypeSymbol ets)
            {
                if (!_allowTypeReference || (_allowTypeReference && evaluated.Type != LangDefaults.Types.Type))
                {
                    Diagnostics.Report("Expression should evatuale to a type symbol", fieldSyntax.Expression);
                    typeExpression = LangDefaults.Types.Unkown;
                }
                else
                {
                    typeExpression = evaluated;
                }
            }
            else
            {
                typeExpression = ets;
            }

            fieldsArr.Add(new FieldSymbol(fieldName, typeExpression, typeExpression.IsCompileTime));
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
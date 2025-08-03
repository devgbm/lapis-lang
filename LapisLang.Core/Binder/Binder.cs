



using System.Collections.Immutable;

namespace LapisLang.Core;

public class Binder
{
    public DiagnosticsBag Diagnostics { get; }

    public Binder()
    {
        Diagnostics = new DiagnosticsBag();
    }
    public Symbol Bind(Syntax syntax, ScopeSymbol? bindingContext = null)
    {
        var context = bindingContext ?? DefaultSymbols.CreateDefaultScope();
        if(typeof(ExpressionSyntax).IsAssignableFrom(syntax.GetType())) return BindExpression(((ExpressionSyntax)syntax), context);
        if(typeof(StatementSyntax).IsAssignableFrom(syntax.GetType())) return BindStatement(((StatementSyntax)syntax), context);

        return Symbol.Unkown;
    }

    private StatementSymbol BindStatement(StatementSyntax ss, ScopeSymbol context)
    {
        switch (ss)
        {
            case VariableDeclarationSyntax vds: return BindVariableDeclaration(vds, context);
            case ScopeStatementSyntax sss: return BindScopeStatement(sss, context);
            case ReturnStatementSyntax rss: return BindReturnStatementSyntax(rss, context);
            default: return StatementSymbol.Unknown;
        }
    }

    private StatementSymbol BindReturnStatementSyntax(ReturnStatementSyntax rss, ScopeSymbol context)
    {
        var expression =  rss.Expression is null ? ExpressionSymbol.Void : BindExpression(rss.Expression, context);

        if (context.ExpectedReturn != expression.Type)
        {
            Diagnostics.Report("wrong return type", rss);
        }

        return new ReturnStatementSymbol(expression);
    }

    private StatementSymbol BindScopeStatement(ScopeStatementSyntax sss, ScopeSymbol context)
    {
        var statements = ImmutableArray.CreateBuilder<StatementSymbol>();
        TypeSymbol? foundReturn = null;
        foreach (var statement in sss.Statements)
        {
            var boundStatement = BindStatement(statement, context);
            statements.Add(boundStatement);
            if (boundStatement is ReturnStatementSymbol rss) foundReturn = rss.Expression.Type;
        }

        if (context.ExpectedReturn is not null && context.ExpectedReturn != DefaultSymbols.Types.Void && foundReturn is null)
        {
            Diagnostics.Report("function must return a value", sss);
        }

        return new ScopeStatementSymbol(statements.ToImmutableArray(), foundReturn ?? DefaultSymbols.Types.Unkown);
    }

    private StatementSymbol BindVariableDeclaration(VariableDeclarationSyntax vds, ScopeSymbol context)
    {
        var expression = BindExpression(vds.Expression, context);

        var name = vds.Identifier.String;
        if (expression is TypeSymbol typeSymbol && typeSymbol.TypeName == "<anonimous-type>")
        {
            typeSymbol.TypeName = vds.Identifier.String;
        }
        context.DefineSymbol(name, expression);


        return new VariableDeclarationSymbol(name, expression, expression.Type);
    }

    private TypeSymbol PromoteToType(TypeSymbol typeSymbol, string name)
    {
        // [TODO] clone value
        typeSymbol.TypeName = name;
        return typeSymbol;
    }


    private ExpressionSymbol BindExpression(ExpressionSyntax es, ScopeSymbol context)
    {
        switch (es)
        {
            case LiteralExpressionSyntax les: return BindLiteralExpression(les, context);
            case BinaryExpressionSyntax bes: return BindBinaryExpression(bes, context);
            case UnaryExpressionSyntax ues: return BindUnaryExpression(ues, context);
            case ParenthesizedExpression pe: return BindExpression(pe.Expression, context);
            case NameExpressionSyntax ne: return BindNameExpression(ne, context);
            case TypeExpressionSyntax tes: return BindTypeEspression(tes, context);
            case MemberExpressionSyntax mes: return BindMemberExpression(mes, context);
            case InstanceInitializationExpression iie: return BindInstanceInitializationExpression(iie, context);
            case FuncExpression fe: return BindFuncExpression(fe, context);
            case CallExpressionSyntax ces: return BindCallExpressionSyntax(ces, context);
            default: return ExpressionSymbol.Unknown;
        }
    }

    private ExpressionSymbol BindCallExpressionSyntax(CallExpressionSyntax ces, ScopeSymbol context)
    {
        var expression = BindExpression(ces.Expression, context);
        if (expression.Type != DefaultSymbols.Types.Function || !(expression is NameExpressionSymbol nes && nes.Symbol is FuncSymbol functionSymbol))
        {
            Diagnostics.Report("expression is not callable", ces);
            return ExpressionSymbol.Unknown;
        }

        var parameterPairs = functionSymbol.Parameters.Zip(ces.Parameters);
        var parameterExpressions = ImmutableArray.CreateBuilder<CallArgument>();

        foreach (var pair in parameterPairs)
        {
            var parameterExpression = BindExpression(pair.Second, context);
            if (pair.First.Type != parameterExpression.Type)
            {
                Diagnostics.Report("wrong parameter type", pair.Second);
            }
            parameterExpressions.Add(new CallArgument(pair.First.Name, parameterExpression));
        }
        if (functionSymbol.Parameters.Count() != ces.Parameters.Count())
        {
            Diagnostics.Report("wrong parameter count", ces);
        }

        return new CallSymbol(functionSymbol, parameterExpressions.ToImmutableArray());
    }

    private ExpressionSymbol BindFuncExpression(FuncExpression fe, ScopeSymbol context)
    {
        var returnType = BindTypeName(fe.ReturnType, context);
        var parameters = fe.Arguments.Select(e => new FunctionParameter(e.Name.String, BindTypeName(e.TypeName, context))).ToImmutableArray();

        var derived = context.Derive();
        derived.ExpectedReturn = returnType;

        foreach (var param in parameters) derived.DefineSymbol(param.Name, new NameSymbol(param.Name, param.Type));
        
        var statement = BindStatement(fe.Statement, derived);

        return new FuncSymbol(fe.SourceSpan, parameters, returnType, statement);
    }

    private ExpressionSymbol BindMemberExpression(MemberExpressionSyntax mes, ScopeSymbol context)
    {
        var expression = BindExpression(mes.Expresison, context);
        var foundFieldMember = expression.Type.GetSymbols().OfType<FieldSymbol>().FirstOrDefault(e => e.Name == mes.Member.Name.String);
        if (foundFieldMember is null)
        {
            Diagnostics.Report("Unkown member", mes.Member);
            return ExpressionSymbol.Unknown;
        }


        return new MemberExpressionSymbol(expression, foundFieldMember);
    }

    private TypeSymbol BindTypeName(TypeNameSyntax typeName, ScopeSymbol context)
    {
        if (!context.GetSymbol(typeName.Identifier.String, out var found))
        {
            Diagnostics.Report("Unkow type simbol", typeName);
            found = DefaultSymbols.Types.Unkown;
        }

        if (found is not TypeSymbol typeSymbol)
        {
            Diagnostics.Report("symbol is not a type", typeName);
            typeSymbol = DefaultSymbols.Types.Unkown;
        }

        foreach (var argument in typeName.TypeArguments) BindTypeName(argument, context);

        return typeSymbol;
    }
    private ExpressionSymbol BindInstanceInitializationExpression(InstanceInitializationExpression iie, ScopeSymbol context)
    {
        var typesymbol = BindTypeName(iie.TypeName, context);

        Dictionary<FieldSymbol, ExpressionSymbol?> fieldInitializers = typesymbol.GetSymbols().OfType<FieldSymbol>().ToDictionary(e => e, e => null as ExpressionSymbol);

        foreach (var initializer in iie.Initializers)
        {
            var foundInitializer = fieldInitializers.Keys.FirstOrDefault(e => e.Name == initializer.Identifier.String);
            if (foundInitializer is null)
            {
                Diagnostics.Report("unkwon field", initializer.Identifier);
                continue;
            }

            var expression = BindExpression(initializer.Expression, context);

            if (foundInitializer.Type is FieldSymbolType fst)
            {
                if (fst.TypeSymbol != expression.Type)
                {
                    Diagnostics.Report("field have mismatched types", initializer.Expression);
                    continue;
                }
            }
            else if (foundInitializer.Type is FieldSymbolArgument fsa)
            {
                var type = typesymbol.Arguments[fsa.Index].Type;
                if (type != expression.Type)
                {
                    Diagnostics.Report("field have mismatched types", initializer.Expression);
                    continue;
                }
            }
            else
            {
                Diagnostics.Report("unkown field type", initializer);
                continue;
            }


            var fieldKey = fieldInitializers.Keys.First(e => e.Name == initializer.Identifier.String);
            fieldInitializers[fieldKey] = expression;
        }

        if (fieldInitializers.Any(e => e.Value == null))
        {
            var field = fieldInitializers.Where(e => e.Value == null).First();
            Diagnostics.Report("uninitialized field", iie.TypeName);
        }

        return new InstanceInitializeSymbol(typesymbol, fieldInitializers);
    }

    private ExpressionSymbol BindTypeEspression(TypeExpressionSyntax tes, ScopeSymbol context)
    {
        var argumentsBuilder = ImmutableArray.CreateBuilder<TypeSymbolArgument>();

        foreach (var argument in tes.Arguments)
        {
            var typeSymbol = BindTypeName(argument.TypeName, context);
            argumentsBuilder.Add(new TypeSymbolArgument(typeSymbol, argument.Identifier.String));
        }

        var arguments = argumentsBuilder.ToImmutableArray();
        var type = new TypeSymbol("<anonimous-type>", arguments);

        foreach (var field in tes.Fields)
        {
            if (arguments.Length > 0)
            {
                var foundArgument = arguments.FirstOrDefault(e => e.Name == field.TypeName.Identifier.String);
                if (foundArgument is not null)
                {
                    var fieldTypeReference = new FieldSymbolArgument(foundArgument, arguments.IndexOf(foundArgument));
                    type.DefineSymbol(field.Identifier.String, new FieldSymbol(type, field.Identifier.String, fieldTypeReference));
                    continue;
                }
            }
            if (!context.GetSymbol(field.TypeName.Identifier.String, out var fieldType))
            {
                Diagnostics.Report("unkow type of field", field.TypeName);
                fieldType = DefaultSymbols.Types.Unkown;
            }

            if (fieldType is not TypeSymbol typeSymbol)
            {
                Diagnostics.Report("Symbol is not a type", field.TypeName);
                typeSymbol = DefaultSymbols.Types.Unkown;
            }

            var fieldSymbolValue = new FieldSymbolType(typeSymbol);
            type.DefineSymbol(field.Identifier.String, new FieldSymbol(type, field.Identifier.String, fieldSymbolValue));
        }

        return type;
    }

    private ExpressionSymbol BindNameExpression(NameExpressionSyntax ne, ScopeSymbol context)
    {
        if (!context.GetSymbol(ne.Name.String, out var symbol))
        {
            Diagnostics.Report("unkown symbol", ne);
            return ExpressionSymbol.Unknown;
        }

        var type = symbol switch
        {
            ExpressionSymbol es => es.Type,
            ScopeSymbol => DefaultSymbols.Types.Scope,
            _ => DefaultSymbols.Types.Unkown
        };

        return new NameExpressionSymbol(symbol, type);
    }

    private ExpressionSymbol BindUnaryExpression(UnaryExpressionSyntax ues, ScopeSymbol context)
    {
        var operand = BindExpression(ues.Expression, context);
        var oper = operand.Type.GetUnaryOperatorFor(ues.TokenOperator.Kind);

        return new UnaryExpressionSymbol(operand, oper.Kind, oper.Result);
    }

    private BinaryExpressionSymbol BindBinaryExpression(BinaryExpressionSyntax bes, ScopeSymbol context)
    {
        var left = BindExpression(bes.Left, context);
        var right = BindExpression(bes.Right, context);
        var oper = left.Type.GetBinaryOperatorFor(bes.OperatorToken.Kind);

        if (oper.With != right.Type)
        {
            Diagnostics.Report("Operator type mismatch", bes.OperatorToken);
        }

        return new BinaryExpressionSymbol(left, right, oper.Kind, oper.Result);
    }

    private ExpressionSymbol BindLiteralExpression(LiteralExpressionSyntax es, ScopeSymbol context)
    {
        switch (es.LiteralType)
        {
            case LiteralType.Integer:
            {
                var value = long.Parse(es.SourceSpan.AsText);
                return new ValueSymbol(value, DefaultSymbols.Types.Integer);
            }
            case LiteralType.String:
            {
                var value = new String(es.SourceSpan.AsText);
                return new ValueSymbol(value, DefaultSymbols.Types.String);
            }
            case LiteralType.Decimal:
            {
                var value = decimal.Parse(es.SourceSpan.AsText);
                return new ValueSymbol(value, DefaultSymbols.Types.Decimal);
            }
            case LiteralType.Boolean:
            {
                var value = bool.Parse(es.SourceSpan.AsText);
                return new ValueSymbol(value, DefaultSymbols.Types.Boolean);
            }
            default: return ExpressionSymbol.Unknown;
        }
    }
}

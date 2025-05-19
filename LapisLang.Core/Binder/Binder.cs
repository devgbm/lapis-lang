



using System.Collections.Immutable;

namespace LapisLang.Core;

public record BindResult(BoundSyntax BoundSyntax, DiagnosticsBag Diagnostics);
public class Binder
{
    private List<BinaryOperatorDefinition> _operators = new()
    {
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.Add, LangDefaults.Types.Integer, LangDefaults.Types.Integer),
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.Sub, LangDefaults.Types.Integer, LangDefaults.Types.Integer),
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.Mul, LangDefaults.Types.Integer, LangDefaults.Types.Integer),
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.Div, LangDefaults.Types.Integer, LangDefaults.Types.Integer),
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.Mod, LangDefaults.Types.Integer, LangDefaults.Types.Integer),

        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.Equality, LangDefaults.Types.Integer, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.Inequality, LangDefaults.Types.Integer, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.Less, LangDefaults.Types.Integer, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.LessEquals, LangDefaults.Types.Integer, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.Greather, LangDefaults.Types.Integer, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Integer, BinaryOperator.GreatherEquals, LangDefaults.Types.Integer, LangDefaults.Types.Boolean),

        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.Add, LangDefaults.Types.Decimal, LangDefaults.Types.Decimal),
        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.Sub, LangDefaults.Types.Decimal, LangDefaults.Types.Decimal),
        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.Mul, LangDefaults.Types.Decimal, LangDefaults.Types.Decimal),
        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.Div, LangDefaults.Types.Decimal, LangDefaults.Types.Decimal),
        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.Mod, LangDefaults.Types.Decimal, LangDefaults.Types.Decimal),

        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.Equality, LangDefaults.Types.Decimal, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.Inequality, LangDefaults.Types.Decimal, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.Less, LangDefaults.Types.Decimal, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.LessEquals, LangDefaults.Types.Decimal, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.Greather, LangDefaults.Types.Decimal, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Decimal, BinaryOperator.GreatherEquals, LangDefaults.Types.Decimal, LangDefaults.Types.Boolean),


        new BinaryOperatorDefinition(LangDefaults.Types.Boolean, BinaryOperator.LogicAnd, LangDefaults.Types.Boolean, LangDefaults.Types.Boolean),
        new BinaryOperatorDefinition(LangDefaults.Types.Boolean, BinaryOperator.LogicOr, LangDefaults.Types.Boolean, LangDefaults.Types.Boolean),
    };

    private List<UnaryOperatorDefinition> _unarys = new()
    {
        new UnaryOperatorDefinition(LangDefaults.Types.Integer, UnaryOperator.Identity, LangDefaults.Types.Integer),
        new UnaryOperatorDefinition(LangDefaults.Types.Integer, UnaryOperator.Inverse, LangDefaults.Types.Integer),
        new UnaryOperatorDefinition(LangDefaults.Types.Boolean, UnaryOperator.Negation, LangDefaults.Types.Boolean),
    };
    private DiagnosticsBag _diagnostics;


    public Binder()
    {
        _diagnostics = new DiagnosticsBag();
    }
    public BindResult Bind(Syntax syntax, BindingContext context)
    {
        if (syntax is ExpressionSyntax es)
        {
            var bounded = BindExpression(es, context);
            return new BindResult(bounded, _diagnostics);
        }
        else if (syntax is StatementSyntax ss)
        {
            var bounded = BindStatement(ss, context);
            return new BindResult(bounded, _diagnostics);
        }
        else
        {
            _diagnostics.Report("unsuported syntax type", syntax);
            return new BindResult(null!, _diagnostics);
        }
    }

    private BoundStatement BindStatement(StatementSyntax statement, BindingContext context)
    {
        switch (statement)
        {
            case VariableDeclarationSyntax vds: return BindVariableDeclaration(vds, context);
            case ExpressionStatementSyntax ess: return BindExpressionStatement(ess, context);
            case ScopeStatement ss: return BindScopeStatement(ss, context);
            case ReturnStatement rs: return BindReturnStatement(rs, context);
            default: return new BoundUnkownStatement(statement);
        }
    }

    private BoundStatement BindReturnStatement(ReturnStatement rs, BindingContext context)
    {
        var expression = BindExpression(rs.Expression, context);
        return new BoundReturnStatement(rs, expression);
    }

    private BoundScopeStatement BindScopeStatement(ScopeStatement ss, BindingContext context)
    {
        var scopeContext = new BindingContext(context.NamespaceRune);
        var statements = ImmutableArray.CreateBuilder<BoundStatement>();
        foreach (var statement in ss.Statements)
        {
            var bound = BindStatement(statement, scopeContext);
            statements.Add(bound);

        }
        return new BoundScopeStatement(ss, statements.ToImmutableArray());
    }

    private BoundExpressionStatement BindExpressionStatement(ExpressionStatementSyntax ess, BindingContext context)
    {
        var expression = BindExpression(ess.Expression, context);
        return new BoundExpressionStatement(ess, expression);
    }

    private BoundVariableDeclaration BindVariableDeclaration(VariableDeclarationSyntax vds, BindingContext context)
    {
        var expression = BindExpression(vds.Expression, context);
        var name = vds.Identifier.String;
        var type = context.ResolveTypename(vds.TypeName);
        var rune = new VariableRune(name, type, expression);
        if (!context.TryDeclareVariable(rune))
        {
            _diagnostics.Report("variable redeclared", vds);
        }
        return new BoundVariableDeclaration(vds, rune);
    }

    public BoundExpression BindExpression(ExpressionSyntax expression, BindingContext context)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax les: return BindLiteral(les, context);
            case BinaryExpressionSyntax bes: return BindBinaryExpression(bes, context);
            case UnaryExpressionSyntax ues: return BindUnaryExpression(ues, context);
            case ParenthesizedExpression ps: return BindExpression(ps.Expression, context);
            case NameExpressionSyntax nes: return BindNameExpression(nes, context);
            case TypeExpressionSyntax tes: return BindTypeExpression(tes, context);
            case InstanceInitializationExpression iie: return BindInstanceInitialization(iie, context);
            case MemberExpressionSyntax mes: return BindMemberExpressionSyntax(mes, context);
            case FuncExpression fes: return BindFuncExpressionSyntax(fes, context);
            case CallExpressionSyntax ces: return BindCallExpressionSyntax(ces, context);
            default:
                _diagnostics.Report("unkown expression syntax", expression);
                return new BoundUnkownExpression(expression);
        }
    }

    private BoundExpression BindCallExpressionSyntax(CallExpressionSyntax ces, BindingContext context)
    {
        var expresion = BindExpression(ces.Expression, context);
        if (expresion.Type != LangDefaults.Types.Function)
        {
            _diagnostics.Report("expression is not callable", ces.Expression);
            return new BoundCallExpression(ces, expresion, []);
        }

        var a = expresion is BoundNameExpression bne ? bne.Rune as FunctionRune : null; // [TODO] verificar outros tipos

        var parameters = ImmutableArray.CreateBuilder<BoundExpression>();
        foreach (var item in ces.Parameters)
        {
            var parameter = BindExpression(item, context);
            parameters.Add(parameter);
        }

        return new BoundCallExpression(ces, expresion, parameters.ToImmutableArray());
    }

    private BoundExpression BindFuncExpressionSyntax(FuncExpression fes, BindingContext context)
    {
        var arguments = ImmutableArray.CreateBuilder<BoundArgument>();
        var scope = new ScopeRune("_temp", RuneKind.Scope);
        context.NamespaceRune.Add(new ScopeRune("_temp", RuneKind.Scope));
        var auxContext = new BindingContext(scope);
        foreach (var argument in fes.Arguments)
        {
            var bound = BindArgument(argument, auxContext);
            var variable = new ArgumentRune(bound.Name, bound.Type);
            if (!auxContext.TryDeclareVariable(variable))
            {
                _diagnostics.Report("argument redeclared", argument);
            }
            else
            {
                arguments.Add(bound);
            }

        }
        var returnType = auxContext.ResolveTypename(fes.ReturnType);
        auxContext.ExpectedReturn(returnType);
        var statement = BindStatement(fes.Statement, auxContext);
        return new BoundFunctionExpression(fes, arguments.ToImmutableArray(), returnType, statement);
    }

    private BoundArgument BindArgument(ArgumentSyntax e, BindingContext context)
    {
        var type = context.ResolveTypename(e.TypeName);
        return new BoundArgument(e, e.Name.String, type);
    }

    private BoundExpression BindMemberExpressionSyntax(MemberExpressionSyntax mes, BindingContext context)
    {
        var expression = BindExpression(mes.Expresison, context);
        var name = mes.Member.Name.String;
        var member = expression.Type.Query(name);

        if (member is not FieldRune fr)
        {
            _diagnostics.Report("unkown member", mes.Member);
            return new BoundMemberExpression(mes, LangDefaults.Types.Unkown, expression, LangDefaults.Types.Unkown);
        }

        return new BoundMemberExpression(mes, fr.Type, expression, fr);
    }

    private BoundInstanceInitializationExpression BindInstanceInitialization(InstanceInitializationExpression iie, BindingContext context)
    {
        var instanceType = context.ResolveTypename(iie.TypeName);

        if (instanceType == LangDefaults.Types.Unkown)
        {
            _diagnostics.Report("unkow type", iie.TypeName);
            return new BoundInstanceInitializationExpression(iie, LangDefaults.Types.Unkown, []);
        }

        var initializers = ImmutableArray.CreateBuilder<BoundFieldInitialization>();
        foreach (var initializer in iie.Initializers)
        {
            var foundRune = instanceType.Query(initializer.Identifier.String);
            if (foundRune is null)
            {
                _diagnostics.Report("unkow field", initializer.Identifier);
            }
            else if (foundRune is not FieldRune fieldRune)
            {
                _diagnostics.Report("name is not a field", initializer.Identifier);
            }
            else
            {
                var boundExpression = BindExpression(initializer.Expression, context);
                if (fieldRune.Type != boundExpression.Type)
                {
                    _diagnostics.Report("expresion type mismatch", initializer.Expression);
                }
                else
                {
                    initializers.Add(new BoundFieldInitialization(initializer.Identifier, initializer.Identifier.String, boundExpression));
                }
            }
        }

        return new BoundInstanceInitializationExpression(iie, instanceType, initializers.ToImmutableArray());
    }

    private BoundTypeExpression BindTypeExpression(TypeExpressionSyntax tes, BindingContext context)
    {
        var fieldDict = new Dictionary<string, (TypeRune Rune, SourceSpan Span)>();
        foreach (var field in tes.Fields)
        {
            var type = context.ResolveTypename(field.TypeName);
            var name = field.Identifier.String;
            if (fieldDict.ContainsKey(name))
            {
                _diagnostics.Report("Field with name already exists in type", field);
            }
            else
            {
                fieldDict.Add(name, (type, field));
            }
        }

        return new BoundTypeExpression(tes, fieldDict.Select(e => new BoundFieldSyntax(e.Value.Span, e.Key, e.Value.Rune)).ToImmutableArray());
    }

    private BoundExpression BindNameExpression(NameExpressionSyntax nes, BindingContext context)
    {
        if (!context.TryResolveName(nes.Name.String, out var rune))
        {
            _diagnostics.Report($"name '{nes.Name.String}' does not exist in current context", nes.Name);
            return new BoundNameExpression(nes, LangDefaults.Types.Unkown, LangDefaults.Types.Unkown, nes.Name.String);
        }

        var type = rune.Kind switch
        {
            RuneKind.Type => LangDefaults.Types.Type,
            RuneKind.Namespace => LangDefaults.Types.Namespace,
            RuneKind.Variable => ((VariableRune)rune).Type,
            RuneKind.Argument => ((ArgumentRune)rune).Type,
            RuneKind.Function => LangDefaults.Types.Function,
            _ => LangDefaults.Types.Unkown,
        };

        return new BoundNameExpression(nes,rune, type, rune.Name);
    }

    private BoundBinaryExpression BindBinaryExpression(BinaryExpressionSyntax bes, BindingContext context)
    {
        var left = BindExpression(bes.Left, context);
        var right = BindExpression(bes.Right, context);
        var oper = GetBinaryOperator(bes.OperatorToken);
        var resultType = _operators.FirstOrDefault(e => e.Left == left.Type && e.Right == right.Type && e.Oper == oper);
        if (resultType is null)
        {
            _diagnostics.Report("Operator not defined for types", bes);
        }
        return new BoundBinaryExpression(bes, left, right, oper, resultType?.Result ?? LangDefaults.Types.Unkown);
    }

    private BoundUnaryExpression BindUnaryExpression(UnaryExpressionSyntax ues, BindingContext context)
    {
        var left = BindExpression(ues.Expression, context);
        var oper = GetUnaryOperator(ues.TokenOperator);
        var resultType = _unarys.FirstOrDefault(e => e.Operand == left.Type && e.Operator == oper);
        if (resultType is null)
        {
            _diagnostics.Report("Operator not defined for types", ues);
        }
        return new BoundUnaryExpression(ues, left, oper, resultType?.Result ?? LangDefaults.Types.Unkown);
    }

    private BinaryOperator GetBinaryOperator(Token operatorToken)
    {
        return operatorToken.Kind switch
        {
            TokenKind.Plus => BinaryOperator.Add,
            TokenKind.Minus => BinaryOperator.Sub,
            TokenKind.Star => BinaryOperator.Mul,
            TokenKind.Slash => BinaryOperator.Div,
            TokenKind.Percent => BinaryOperator.Mod,
            TokenKind.AndKeyword => BinaryOperator.LogicAnd,
            TokenKind.OrKeyword => BinaryOperator.LogicOr,
            TokenKind.DoubleEquals => BinaryOperator.Equality,
            TokenKind.BangEquals => BinaryOperator.Inequality,
            TokenKind.LeftArrowEquals => BinaryOperator.LessEquals,
            TokenKind.RightArrowEquals => BinaryOperator.GreatherEquals,
            TokenKind.LeftArrow => BinaryOperator.Less,
            TokenKind.RightArrow => BinaryOperator.Greather,
            _ => BinaryOperator.Unkown
        };
    }

    private UnaryOperator GetUnaryOperator(Token operatorToken)
    {
        return operatorToken.Kind switch
        {
            TokenKind.NotKeyword => UnaryOperator.Negation,
            TokenKind.Minus => UnaryOperator.Inverse,
            TokenKind.Plus => UnaryOperator.Identity,
            _ => UnaryOperator.Unkown
        };
    }

    private BoundLiteralExpression BindLiteral(LiteralExpressionSyntax les, BindingContext context)
    {
        TypeRune type;
        object? value;
        switch (les)
        {
            case { LiteralType: LiteralType.Integer }:
                type = LangDefaults.Types.Integer;
                value = long.Parse(les.SourceSpan.AsText);
                break;
            case { LiteralType: LiteralType.String }:
                type = LangDefaults.Types.String;
                value = les.SourceSpan.AsText.ToString();
                break;
            case { LiteralType: LiteralType.Boolean }:
                type = LangDefaults.Types.Boolean;
                value = bool.Parse(les.SourceSpan.AsText);
                break;
            case { LiteralType: LiteralType.Decimal }:
                type = LangDefaults.Types.Decimal;
                value = decimal.Parse(les.SourceSpan.AsText);
                break;
            default:
                _diagnostics.Report("unkown type", les);
                type = LangDefaults.Types.Unkown;
                value = null;
                break;
        }

        return new BoundLiteralExpression(les, type, value);
    }
}

internal record BinaryOperatorDefinition(TypeRune Left, BinaryOperator Oper, TypeRune Right, TypeRune Result);
internal record UnaryOperatorDefinition(TypeRune Operand, UnaryOperator Operator, TypeRune Result);
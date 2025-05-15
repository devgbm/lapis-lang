

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
    public BindResult Bind(Syntax syntax)
    {
        if (syntax is ExpressionSyntax es)
        {
            var bounded = BindExpression(es);
            return new BindResult(bounded, _diagnostics);
        }
        else
        {
            _diagnostics.Report("unsuported syntax type", syntax);
            return new BindResult(null!, _diagnostics);
        }
    }
    public BoundExpression BindExpression(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax les: return BindLiteral(les);
            case BinaryExpressionSyntax bes: return BindBinaryExpression(bes);
            case UnaryExpressionSyntax ues: return BindUnaryExpression(ues);

            default:
                _diagnostics.Report("unkown expression syntax", expression);
                return new BoundUnkownExpression(expression);
        }
    }

    private BoundBinaryExpression BindBinaryExpression(BinaryExpressionSyntax bes)
    {
        var left = BindExpression(bes.Left);
        var right = BindExpression(bes.Right);
        var oper = GetBinaryOperator(bes.OperatorToken);
        var resultType = _operators.FirstOrDefault(e => e.Left == left.Type && e.Right == right.Type && e.Oper == oper);
        if (resultType is null)
        {
            _diagnostics.Report("Operator not defined for types", bes);
        }
        return new BoundBinaryExpression(bes, left, right, oper, resultType?.Result ?? LangDefaults.Types.Unkown );
    }

    private BoundUnaryExpression BindUnaryExpression(UnaryExpressionSyntax ues)
    {
        var left = BindExpression(ues.Expression);
        var oper = GetUnaryOperator(ues.TokenOperator);
        var resultType = _unarys.FirstOrDefault(e => e.Operand == left.Type  && e.Operator == oper);
        if (resultType is null)
        {
            _diagnostics.Report("Operator not defined for types", ues);
        }
        return new BoundUnaryExpression(ues, left, oper, resultType?.Result ?? LangDefaults.Types.Unkown );
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

    private BoundLiteralExpression BindLiteral(LiteralExpressionSyntax les)
    {
        LapisType type;
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

internal record  BinaryOperatorDefinition(LapisType Left, BinaryOperator Oper, LapisType Right, LapisType Result);
internal record  UnaryOperatorDefinition(LapisType Operand, UnaryOperator Operator, LapisType Result);
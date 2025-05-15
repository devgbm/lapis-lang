
namespace LapisLang.Core;

public record BindResult(BoundSyntax BoundSyntax, DiagnosticsBag Diagnostics);
public class Binder
{
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
            default:
                _diagnostics.Report("unkown expression syntax", expression);
                return new BoundUnkownExpression(expression);
        }
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
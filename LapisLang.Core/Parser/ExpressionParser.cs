






using System.Collections.Immutable;

namespace LapisLang.Core;

public class ExpressionParser : ParserBase
{
    public ExpressionParser(TokenStream tokenStream) : base(tokenStream)
    {
    }

    public ExpressionSyntax ParseExpression()
    {
        var primary = ParseLiteralExpression();
        return primary;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        ExpressionSyntax expression; 
        switch(Current.Kind)
        {
            case TokenKind.Identifier:
                expression = ParseNameExpression();
                break;
            
            case TokenKind.IntNumber:
            case TokenKind.True:
            case TokenKind.False:
            case TokenKind.SingleQuoteString:            
            case TokenKind.DoubleQuoteString:            
            case TokenKind.DecimalNumber:
                expression = ParseLiteralExpression();
                break;

            default: return null!;
        }

        // while(Current.Kind == TokenKind.Dot || Current.Kind == TokenKind.LeftArrow)
        // {
        //     if(Current.Kind == TokenKind.Dot) expression = ParseMemberExpression(expression);
        //     if(expression is NameExpressionSyntax || (expression is MemberExpressionSyntax mes && mes.Member is NameExpressionSyntax))
        //     {
        //         if(Current.Kind == TokenKind.LeftArrow) expression = ParseNameParameters(expression);
        //     }
        // }

        return expression;
    }

    private ExpressionSyntax ParseLiteralExpression()
    {
        var token = NextToken();
        var literalType = token switch {
            { Kind: TokenKind.IntNumber } => LiteralType.Integer,
            { Kind: TokenKind.True } => LiteralType.Boolean,
            { Kind: TokenKind.False } => LiteralType.Boolean,
            { Kind: TokenKind.SingleQuoteString } => LiteralType.String,
            { Kind: TokenKind.DecimalNumber } => LiteralType.Decimal,
            _ => LiteralType.Unkown
        };

        return new LiteralExpressionSyntax(token, literalType);
    }

    private ExpressionSyntax ParseNameParameters(ExpressionSyntax expression)
    {
        var open = Match(TokenKind.LeftArrow);

        var parameters = MatchUntil(TokenKind.RightArrow, () => {
            var name = Match(TokenKind.Identifier);
            Match(TokenKind.Collon);
            var parameterExpression = ParseExpression();
            if(Current.Kind != TokenKind.RightArrow)
            {
                Match(TokenKind.Comma);
            }
            var parameterSpan = SourceSpan.Between(name, parameterExpression.SourceSpan);
            return new ParameterExpression(parameterExpression, name, parameterSpan);
        });


        var close = Match(TokenKind.RightArrow);
        var sourceSpan = SourceSpan.Between(open, close);
        return new NameParametersExpressionSyntax(sourceSpan, expression, parameters);
    }

    private ExpressionSyntax ParseMemberExpression(ExpressionSyntax expression)
    {
        ExpressionSyntax member = expression;
        while(Current.Kind == TokenKind.Dot)
        {
            NextToken();
            var nextName = ParseNameExpression();
            member = new MemberExpressionSyntax(nextName.SourceSpan, member, nextName);
        }
        return member;
    }
    
    private NameExpressionSyntax ParseNameExpression()
    {
        var identifier = Match(TokenKind.Identifier);
        return new NameExpressionSyntax(identifier, identifier);
    }
}

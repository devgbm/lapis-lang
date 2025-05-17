

namespace LapisLang.Core;

public class ExpressionParser : ParserBase
{
    public ExpressionParser(TokenStream tokenStream) : base(tokenStream)
    {
    }

    public ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
        ExpressionSyntax expression;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);

        if(unaryPrecedence != 0 && unaryPrecedence > parentPrecedence)
        {
            var operatorToken = NextToken();
            var operand = ParseExpression(unaryPrecedence);
            expression = new UnaryExpressionSyntax(SourceSpan.Between(operatorToken, operand.SourceSpan), operand, operatorToken); 
        }
        else
        {
            expression = ParsePrimaryExpression();
        }

        while(true)
        {

            var binaryPrecedence = GetBinaryPrecedence(Current.Kind);
            if (binaryPrecedence == 0 || binaryPrecedence <= parentPrecedence) break;

            var operatorToken = NextToken();
            var right = ParseExpression(binaryPrecedence);
            expression = new BinaryExpressionSyntax(SourceSpan.Between(expression.SourceSpan, right.SourceSpan), expression, right, operatorToken);
        }

        return expression;
    }

    private int GetUnaryPrecedence(TokenKind kind)
    {
        switch(kind)
        {
            case TokenKind.NotKeyword:
            case TokenKind.Minus:
            case TokenKind.Plus:
                return 5;

            default: return 0;
        }
    }

    private int GetBinaryPrecedence(TokenKind kind)
    {
        switch(kind)
        {
            case TokenKind.Star:
            case TokenKind.Slash:
            case TokenKind.Percent:
                return 5;
                
            case TokenKind.Minus:
            case TokenKind.Plus:
                return 4;

            case TokenKind.DoubleEquals:
            case TokenKind.BangEquals:
            case TokenKind.LeftArrow:
            case TokenKind.LeftArrowEquals:
            case TokenKind.RightArrow:
            case TokenKind.RightArrowEquals:
                return 3;

            case TokenKind.AndKeyword:
                return 2;

            case TokenKind.OrKeyword:
                return 1;
            
            
            default: return 0;
        }
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        ExpressionSyntax expression;
        switch (Current.Kind)
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

            case TokenKind.OpenParenthesis:
                expression = ParseParenthesizedExpression();
                break;

            case TokenKind.TypeKeyword:
                expression = ParseTypeExpression();
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

    private TypeExpressionSyntax ParseTypeExpression()
    {
        var keyword = Match(TokenKind.TypeKeyword);
        var open = Match(TokenKind.OpenCurlyBrace);
        var fields = MatchUntil(TokenKind.CloseCurlyBrace, () =>
        {
            var field = ParseTypeField();
            Match(TokenKind.SemiCollon);
            return field;
        });
        var close = Match(TokenKind.CloseCurlyBrace);
        return new TypeExpressionSyntax(SourceSpan.Between(keyword, close), fields);
    }

    public FieldDeclarationSyntax ParseTypeField()
    {
        var identifier = Match(TokenKind.Identifier);
        Match(TokenKind.Collon);
        var typename = ParseTypename();
        return new FieldDeclarationSyntax(SourceSpan.Between(identifier, typename), identifier, typename);
    }

    public TypeNameSyntax ParseTypename()
    {
        var identifier = Match(TokenKind.Identifier);
        return new TypeNameSyntax(identifier, identifier);
    }

    private ExpressionSyntax ParseParenthesizedExpression()
    {
        var open = Match(TokenKind.OpenParenthesis);
        var expression = ParseExpression();
        var close = Match(TokenKind.CloseParenthesis);
        return new ParenthesizedExpression(SourceSpan.Between(open, close), expression);
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

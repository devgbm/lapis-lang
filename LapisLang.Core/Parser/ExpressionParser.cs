







using System.Collections.Immutable;

namespace LapisLang.Core;

public class LapisParser : ParserBase
{
    public LapisParser(TokenStream tokenStream) : base(tokenStream)
    {
    }

    public Syntax Parse()
    {
        switch (Current.Kind)
        {
            case TokenKind.DefineKeyword:
            case TokenKind.OpenCurlyBrace:
            case TokenKind.IfKeyword:
            case TokenKind.ReturnKeyword:
            case TokenKind.VarKeyword:
            case TokenKind.Identifier when Peek(1).Kind == TokenKind.Equal:
                return ParseStatement();

            default: return ParseExpression();
        }
    }

    public ScopeStatementSyntax ParseProgram()
    {
        var start = Current.SourceSpan;
        var statements = MatchUntil(TokenKind.EoFToken, ParseStatement);
        var end = Current.SourceSpan;
        return new ScopeStatementSyntax(SourceSpan.Between(start, end), statements);
    }

    public ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
        ExpressionSyntax expression;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);

        if (unaryPrecedence != 0 && unaryPrecedence > parentPrecedence)
        {
            var operatorToken = NextToken();
            var operand = ParseExpression(unaryPrecedence);
            expression = new UnaryExpressionSyntax(SourceSpan.Between(operatorToken, operand.SourceSpan), operand, operatorToken);
        }
        else
        {
            expression = ParsePrimaryExpression();
        }

        while (true)
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
        switch (kind)
        {
            case TokenKind.NotKeyword:
            case TokenKind.Minus:
            case TokenKind.Plus:
                return 5;
            case TokenKind.TypeofKeyword:
                return 4;

            default: return 0;
        }
    }

    private int GetBinaryPrecedence(TokenKind kind)
    {
        switch (kind)
        {
            case TokenKind.Star:
            case TokenKind.Slash:
            case TokenKind.Percent:
            case TokenKind.BangAnd:
            case TokenKind.And:
                return 5;

            case TokenKind.Minus:
            case TokenKind.Plus:
            case TokenKind.MatchKeyword:
                return 4;

            case TokenKind.DoubleEquals:
            case TokenKind.BangEquals:
            case TokenKind.LeftArrow:
            case TokenKind.LeftArrowEquals:
            case TokenKind.RightArrow:
            case TokenKind.RightArrowEquals:
                return 3;

            case TokenKind.AndKeyword:
            case TokenKind.NotKeyword:
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
            case TokenKind.Identifier when Peek(1).Kind == TokenKind.OpenCurlyBrace:
                expression = ParseInstanceInitializationExpression();
                break;

            case TokenKind.Dot when Peek(1).Kind == TokenKind.OpenCurlyBrace:
                expression = ParseAnonimousInstanceInitializationExpression();
                break;

            case TokenKind.Identifier:
            case TokenKind.SelfKeyword:
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
            case TokenKind.FuncKeyword:
                expression = ParseFuncExpression();
                break;

            default:
                expression = new UnkownExpressionSyntax(NextToken().SourceSpan);
                break;
        }

        while (Current.Kind == TokenKind.Dot || Current.Kind == TokenKind.OpenParenthesis)
        {
            if (Current.Kind == TokenKind.Dot) expression = ParseMemberExpression(expression);
            if (Current.Kind == TokenKind.OpenParenthesis) expression = ParseCallExpression(expression);
        }

        return expression;
    }

    private CallExpressionSyntax ParseCallExpression(ExpressionSyntax expression)
    {
        var open = Match(TokenKind.OpenParenthesis);
        var parameters = MatchUntil(TokenKind.CloseParenthesis, () =>
        {
            var expression = ParseExpression();
            if (Current.Kind != TokenKind.CloseParenthesis) Match(TokenKind.Comma);
            return expression;
        });

        var close = Match(TokenKind.CloseParenthesis);
        return new CallExpressionSyntax(SourceSpan.Between(open,close), expression, parameters);
    }

    private ExpressionSyntax ParseFuncExpression()
    {
        var keyword = Match(TokenKind.FuncKeyword);
        var openParams = Match(TokenKind.OpenParenthesis);
        var arguments = MatchUntil(TokenKind.CloseParenthesis, () =>
        {
            var parameterSyntax = ParseArgumentSyntax();
            if (Current.Kind != TokenKind.CloseParenthesis) Match(TokenKind.Comma);
            return parameterSyntax;
        });
        var closeParams = Match(TokenKind.CloseParenthesis);

        var returnType = ParseExpression();
        Match(TokenKind.Collon);

        var statement = ParseStatement();
        return new FuncExpressionSyntax(keyword, arguments, returnType, statement);
    }

    private ArgumentSyntax ParseArgumentSyntax()
    {
        if (Current.Kind == TokenKind.SelfKeyword)
        {
            var selfToken = NextToken();
            return new SelfArgumentSyntax(selfToken.SourceSpan);
        }
        var typename = ParseExpression();
        var identifier = ParseNameExpression();
        return new ArgumentSyntax(SourceSpan.Between(typename, identifier), typename, identifier);
    }

    private TypeNameSyntax ParseTypenameNew()
    {
        var identifier = Match(TokenKind.Identifier);
        
        if (identifier.String == "Func")
        {
            var parameters = MatchUntil(TokenKind.CloseBracket, () =>
            {
                var typename = ParseTypenameNew();
                if (Current.Kind != TokenKind.CloseBracket) Match(TokenKind.Comma);
                return typename;
            });

            var collon = Match(TokenKind.Collon);

            var returnTypename = ParseTypenameNew();
            return new FuncTypenameSyntax(SourceSpan.Between(identifier, returnTypename.SourceSpan), parameters, returnTypename);
        }

        return new TypeNameSyntax(identifier, identifier);

    }

    private ExpressionSyntax ParseInstanceInitializationExpression()
    {
        var typename = ParseTypename();
        Match(TokenKind.OpenCurlyBrace);
        var initializers = MatchUntil(TokenKind.CloseCurlyBrace, () =>
        {
            var propertyInitialization = ParsePropertyInitialization();
            if (Current.Kind == TokenKind.CloseCurlyBrace) MatchOptional(TokenKind.SemiCollon);
            else Match(TokenKind.SemiCollon);
            return propertyInitialization;
        });
        var close = Match(TokenKind.CloseCurlyBrace);
        return new InstanceInitializationExpression(SourceSpan.Between(typename, close), typename, initializers);
    }

    private ExpressionSyntax ParseAnonimousInstanceInitializationExpression()
    {
        var dot = Match(TokenKind.Dot);
        Match(TokenKind.OpenCurlyBrace);
        var initializers = MatchUntil(TokenKind.CloseCurlyBrace, () =>
        {
            var propertyInitialization = ParsePropertyInitialization();
            if (Current.Kind == TokenKind.CloseCurlyBrace) MatchOptional(TokenKind.SemiCollon);
            else Match(TokenKind.SemiCollon);
            return propertyInitialization;
        });
        var close = Match(TokenKind.CloseCurlyBrace);
        return new InstanceInitializationExpression(SourceSpan.Between(dot, close), initializers);
    }


    private FieldInitilizationSyntax ParsePropertyInitialization()
    {
        var identifier = Match(TokenKind.Identifier);
        Match(TokenKind.Collon);
        var expression = ParseExpression();
        return new FieldInitilizationSyntax(SourceSpan.Between(identifier, expression), identifier, expression);
    }

    private TypeExpressionSyntax ParseTypeExpression()
    {
        var keyword = Match(TokenKind.TypeKeyword);

        var arguments = ImmutableArray.Create<TypeArgumentSyntax>();

        var open = Match(TokenKind.OpenCurlyBrace);


        var fields = MatchUntil(TokenKind.CloseCurlyBrace, () =>
        {
            var field = ParseTypeField();
            Match(TokenKind.SemiCollon);
            return field;
        });
        var close = Match(TokenKind.CloseCurlyBrace);

        return new TypeExpressionSyntax(SourceSpan.Between(keyword, close), fields, arguments);
    }

    public TypeArgumentSyntax ParseTypeArgument()
    {
        var typeName = ParseTypename();
        var identifier = Match(TokenKind.Identifier);
        return new TypeArgumentSyntax(typeName, identifier, SourceSpan.Between(typeName, identifier));
    }

    public FieldDeclarationSyntax ParseTypeField()
    {
        var identifier = Match(TokenKind.Identifier);
        Match(TokenKind.Collon);
        var expression = ParseExpression();
        return new FieldDeclarationSyntax(SourceSpan.Between(identifier, expression), identifier, expression);
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
        var literalType = token switch
        {
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

        var parameters = MatchUntil(TokenKind.RightArrow, () =>
        {
            var name = Match(TokenKind.Identifier);
            Match(TokenKind.Collon);
            var parameterExpression = ParseExpression();
            if (Current.Kind != TokenKind.RightArrow)
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
        while (Current.Kind == TokenKind.Dot)
        {
            NextToken();
            var nextName = ParseNameExpression();
            member = new MemberExpressionSyntax(SourceSpan.Between(member, nextName.SourceSpan), member, nextName);
        }
        return member;
    }

    private NameExpressionSyntax ParseNameExpression()
    {
        var identifier = Current.Kind == TokenKind.SelfKeyword
            ? NextToken()
            : Match(TokenKind.Identifier);
        return new NameExpressionSyntax(identifier, identifier);
    }

    #region Statements
    public StatementSyntax ParseStatement()
    {
        switch (Current.Kind)
        {
            case TokenKind.DefineKeyword: return ParseDefinition();
            case TokenKind.OpenCurlyBrace: return ParseScopeStatement();
            case TokenKind.ReturnKeyword: return ParseReturnStatement();
            case TokenKind.VarKeyword: return ParseVariableDeclaration();
            case TokenKind.IfKeyword: return ParseIfStatement();
            case TokenKind.Identifier when Peek(1).Kind == TokenKind.Equal: return ParseAssingStatement(); 
            default:
                {
                    var expression = ParseExpression();
                    var collon = Match(TokenKind.SemiCollon);
                    return new ExpressionStatementSyntax(expression, expression);
                }
        }
    }

    private StatementSyntax ParseAssingStatement()
    {
        var identifier = Match(TokenKind.Identifier);
        Match(TokenKind.Equal);
        var expression = ParseExpression();
        var semi = Match(TokenKind.SemiCollon);
        return new AssingStatementSyntax(SourceSpan.Between(identifier, semi), identifier, expression);
        throw new NotImplementedException();
    }

    private StatementSyntax ParseVariableDeclaration()
    {
        var keyword = Match(TokenKind.VarKeyword);
        var identifier = Match(TokenKind.Identifier);
        Match(TokenKind.Equal);
        var expression = ParseExpression();
        var semi = Match(TokenKind.SemiCollon);
        return new VarStatementSyntax(SourceSpan.Between(keyword, semi), identifier, expression);
    }

    private StatementSyntax ParseIfStatement()
    {
        var keyword = Match(TokenKind.IfKeyword);
        var conditional = ParseExpression();
        var statement = ParseStatement();
        if (Current.Kind != TokenKind.ElseKeyword)
        {
            return new IfStatementSyntax(SourceSpan.Between(keyword, statement), conditional, statement, null);
        }
        Match(TokenKind.ElseKeyword);

        var elseStatement = ParseStatement();
        return new IfStatementSyntax(SourceSpan.Between(keyword, statement), conditional, statement, elseStatement);
    }

    private ScopeStatementSyntax ParseScopeStatement()
    {
        var open = Match(TokenKind.OpenCurlyBrace);

        var statements = MatchUntil(TokenKind.CloseCurlyBrace, ParseStatement);

        var close = Match(TokenKind.CloseCurlyBrace);
        return new ScopeStatementSyntax(SourceSpan.Between(open, close), statements);
    }

    private ReturnStatementSyntax ParseReturnStatement()
    {
        var keyword = Match(TokenKind.ReturnKeyword);
        var expression = ParseExpression();
        var semi = Match(TokenKind.SemiCollon);
        return new ReturnStatementSyntax(SourceSpan.Between(keyword, semi), expression);
    }

    private StatementSyntax ParseDefinition()
    {
        var keyword = Match(TokenKind.DefineKeyword);
        var identifier = Match(TokenKind.Identifier);

        if (Current.Kind == TokenKind.Dot)
        {
            NextToken();
            var memberName = Match(TokenKind.Identifier);
            MatchOptional(TokenKind.Equal);
            var memberExpr = ParseExpression();
            var memberSemi = Match(TokenKind.SemiCollon);
            return new MemberDefineStatementSyntax(SourceSpan.Between(keyword, memberSemi), identifier, memberName, memberExpr);
        }

        Match(TokenKind.Equal);
        var expression = ParseExpression();
        var semi = Match(TokenKind.SemiCollon);
        SourceSpan sourceSpan = SourceSpan.Between(keyword, semi);
        return new DefineStatementSyntax(sourceSpan, identifier, expression);
    }
    #endregion
}

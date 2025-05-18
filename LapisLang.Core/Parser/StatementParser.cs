
using System.Collections.Immutable;

namespace LapisLang.Core;

public class StatementParser : ParserBase
{
    private ExpressionParser _expression;
    public StatementParser(TokenStream tokenStream) : base(tokenStream)
    {
        _expression = new ExpressionParser(tokenStream);
    }

    public ExpressionSyntax ParseExpression()
    {
        return _expression.ParseExpression();
    }

    public StatementSyntax ParseStatement()
    {
        switch (Current.Kind)
        {
            case TokenKind.VarKeyword: return ParseVariableDeclaration();
            default:
                {
                    var expression = ParseExpression();
                    return new ExpressionStatementSyntax(expression, expression);
                }
        }
    }

    private StatementSyntax ParseVariableDeclaration()
    {
        var keyword = Match(TokenKind.VarKeyword);
        var identifier = Match(TokenKind.Identifier);
        Match(TokenKind.Collon);
        var typeName = _expression.ParseTypename();
        Match(TokenKind.Equal);
        var expression = ParseExpression();
        var semi = Match(TokenKind.SemiCollon);
        SourceSpan sourceSpan = SourceSpan.Between(keyword, semi);
        return new VariableDeclarationSyntax(sourceSpan, identifier, typeName, expression);
    }
}

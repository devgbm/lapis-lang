using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;
using Lapis.Lexer;

namespace Lapis.Parser;

/// <summary>
/// <c>tokens → Surface AST</c>.
///
/// Recursivo descendente para statements e primárias, precedence climbing para
/// binários. Escrito à mão porque as ambiguidades reais da gramática exigem
/// controle explícito de backtracking (plano 04).
///
/// Nunca executa código (spec §36) e nunca lança: erros viram diagnósticos e a
/// recuperação garante que o resto do arquivo continue sendo analisado.
/// </summary>
public sealed class Parser
{
    private const int MaxDepth = 200;

    private readonly TokenStream _tokens;
    private readonly DiagnosticBag _diagnostics;
    private int _depth;

    private Parser(TokenStream tokens, DiagnosticBag diagnostics)
    {
        _tokens = tokens;
        _diagnostics = diagnostics;
    }

    public static SourceFile Parse(SourceText source, DiagnosticBag diagnostics)
    {
        var tokens = Lexer.Lexer.Tokenize(source, diagnostics);
        return Parse(tokens, diagnostics);
    }

    public static SourceFile Parse(ImmutableArray<Token> tokens, DiagnosticBag diagnostics)
    {
        var parser = new Parser(new TokenStream(tokens), diagnostics);
        return parser.ParseSourceFile();
    }

    private Token Current => _tokens.Current;

    // ------------------------------------------------------------ arquivo

    private SourceFile ParseSourceFile()
    {
        var statements = ImmutableArray.CreateBuilder<Statement>();
        var start = Current.Span.Start;

        while (!_tokens.AtEnd)
        {
            var before = _tokens.Mark();
            var statement = ParseStatement();

            if (statement is not null)
            {
                statements.Add(statement);
            }

            // Garantia de progresso: se nada foi consumido, força um passo.
            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        return new SourceFile(statements.ToImmutable())
        {
            Span = SourceSpan.FromBounds(start, Current.Span.End),
        };
    }

    // --------------------------------------------------------- statements

    private Statement? ParseStatement()
    {
        if (Current.Kind == TokenKind.DefKeyword)
        {
            return ParseDefStatement();
        }

        if (Current.Kind == TokenKind.Bad)
        {
            // O lexer já reportou; consumir em silêncio evita erro duplo.
            _tokens.Advance();
            return null;
        }

        var start = Current.Span.Start;
        var expression = ParseExpression();

        if (!_tokens.Match(TokenKind.Semicolon) && !IsBlockLike(expression) && !ExpectSemicolon(start))
        {
            RecoverToStatementBoundary();
        }

        return new ExpressionStatement(expression)
        {
            Span = SourceSpan.FromBounds(start, PreviousEnd()),
        };
    }

    /// <summary>
    /// Expressões que terminam em bloco dispensam o <c>;</c> quando usadas como
    /// statement. Sem essa regra o próprio exemplo <c>abs</c> da spec §12 —
    /// <c>if x &lt; 0 { return -x; }</c> seguido de <c>return x;</c> — não parsearia.
    /// </summary>
    private static bool IsBlockLike(Expression expression) =>
        expression is BlockExpression or IfExpression or MatchExpression;

    private Statement ParseDefStatement()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'def'

        var nameToken = Current;
        var name = "?";

        if (Current.Kind == TokenKind.Identifier)
        {
            name = Current.Text;
            _tokens.Advance();
        }
        else
        {
            Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado um identificador após 'def'");
        }

        TypeSyntax? annotation = null;

        if (_tokens.Match(TokenKind.Colon))
        {
            annotation = ParseType();
        }

        Expression value;

        if (Current.Kind != TokenKind.Equals)
        {
            Report(DiagnosticCodes.ExpectedEquals, Current.Span, "esperado '=' em 'def'");
            value = ErrorExpr(Current.Span);
            RecoverToStatementBoundary();
        }
        else
        {
            _tokens.Advance();
            value = ParseExpression();

            if (!ExpectSemicolon(start))
            {
                RecoverToStatementBoundary();
            }
        }

        return new DefStatement(name, annotation, value)
        {
            Span = SourceSpan.FromBounds(start, PreviousEnd()),
            NameSpan = nameToken.Span,
        };
    }

    private bool ExpectSemicolon(int statementStart)
    {
        if (_tokens.Match(TokenKind.Semicolon))
        {
            return true;
        }

        // Mesmo no fim do arquivo o problema útil de relatar é o `;` faltando;
        // LAP0109 fica para entrada realmente truncada (`def x =`).
        Report(DiagnosticCodes.ExpectedSemicolon, SpanFrom(statementStart), "esperado ';' ao final da declaração");
        return false;
    }

    /// <summary>
    /// Descarta tokens até um limite seguro: <c>;</c> (consumido), ou um token que
    /// começa statement. O contador de chaves impede saltar para fora do bloco.
    /// </summary>
    private void RecoverToStatementBoundary()
    {
        var depth = 0;

        while (!_tokens.AtEnd)
        {
            switch (Current.Kind)
            {
                case TokenKind.Semicolon when depth == 0:
                    _tokens.Advance();
                    return;

                case TokenKind.DefKeyword when depth == 0:
                    return;

                case TokenKind.CloseBrace when depth == 0:
                    return;

                case TokenKind.OpenBrace or TokenKind.OpenParen or TokenKind.OpenBracket:
                    depth++;
                    break;

                case TokenKind.CloseBrace or TokenKind.CloseParen or TokenKind.CloseBracket:
                    depth--;
                    break;
            }

            _tokens.Advance();
        }
    }

    // -------------------------------------------------------- expressions

    private Expression ParseExpression()
    {
        if (++_depth > MaxDepth)
        {
            _depth--;
            Report(
                DiagnosticCodes.ExpressionTooDeep,
                Current.Span,
                $"expressão aninhada profundamente demais (limite {MaxDepth})");
            RecoverToStatementBoundary();
            return ErrorExpr(Current.Span);
        }

        try
        {
            return Current.Kind == TokenKind.ReturnKeyword ? ParseReturn() : ParseBinary(0);
        }
        finally
        {
            _depth--;
        }
    }

    private Expression ParseReturn()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'return'

        // `return;` e `return }` são retornos vazios; qualquer outra coisa é o valor.
        var hasValue = Current.Kind is not (TokenKind.Semicolon or TokenKind.CloseBrace
            or TokenKind.EndOfFile or TokenKind.Comma or TokenKind.CloseParen);

        var value = hasValue ? ParseExpression() : null;

        return new ReturnExpression(value) { Span = SpanFrom(start) };
    }

    private Expression ParseBinary(int minPrecedence)
    {
        var left = ParseUnary();
        var comparisonSeen = false;

        while (TryGetBinaryOperator(Current.Kind, out var op))
        {
            var precedence = op.Precedence();

            if (precedence < minPrecedence)
            {
                break;
            }

            // `a < b < c` é rejeitado em vez de parsear como `(a<b)<c`: a segunda
            // comparação teria Bool à esquerda e o erro de tipo seria confuso.
            // Parentetizar resolve, porque cada nível de parênteses reinicia a flag.
            if (op.IsComparison())
            {
                if (comparisonSeen)
                {
                    Report(
                        DiagnosticCodes.ChainedComparison,
                        Current.Span,
                        "comparações não podem ser encadeadas; use parênteses");
                }

                comparisonSeen = true;
            }

            var operatorToken = _tokens.Advance();
            var right = ParseBinary(precedence + 1);

            left = new BinaryExpression(op, left, right)
            {
                Span = SourceSpan.Union(left.Span, right.Span),
                OperatorSpan = operatorToken.Span,
            };
        }

        return left;
    }

    private Expression ParseUnary()
    {
        if (Current.Kind is TokenKind.Minus or TokenKind.Bang)
        {
            var start = Current.Span.Start;
            var op = _tokens.Advance().Kind == TokenKind.Minus ? UnaryOperator.Negate : UnaryOperator.Not;
            var operand = ParseUnary();

            return new UnaryExpression(op, operand) { Span = SpanFrom(start) };
        }

        return ParsePostfix();
    }

    private Expression ParsePostfix()
    {
        var expression = ParsePrimary();

        while (true)
        {
            switch (Current.Kind)
            {
                case TokenKind.OpenParen:
                    expression = ParseCall(expression);
                    continue;

                case TokenKind.OpenBracket:
                    expression = ParseIndex(expression);
                    continue;

                case TokenKind.Dot:
                    expression = ParseMember(expression);
                    continue;

                default:
                    return expression;
            }
        }
    }

    private Expression ParseIndex(Expression target)
    {
        _tokens.Advance(); // '['

        var index = ParseExpression();
        var end = Current.Span.End;

        if (!_tokens.Match(TokenKind.CloseBracket))
        {
            Report(DiagnosticCodes.ExpectedCloseBracket, Current.Span, "esperado ']' para fechar a indexação");
            end = PreviousEnd();
        }

        return new IndexExpression(target, index)
        {
            Span = SourceSpan.FromBounds(target.Span.Start, end),
        };
    }

    private Expression ParseMember(Expression target)
    {
        _tokens.Advance(); // '.'

        var nameToken = Current;

        if (Current.Kind != TokenKind.Identifier)
        {
            Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado um nome após '.'");

            return new MemberExpression(target, "?")
            {
                Span = SourceSpan.FromBounds(target.Span.Start, PreviousEnd()),
                NameSpan = nameToken.Span,
            };
        }

        _tokens.Advance();

        return new MemberExpression(target, nameToken.Text)
        {
            Span = SourceSpan.FromBounds(target.Span.Start, nameToken.Span.End),
            NameSpan = nameToken.Span,
        };
    }

    private Expression ParseCall(Expression callee)
    {
        _tokens.Advance(); // '('

        var arguments = ImmutableArray.CreateBuilder<Expression>();

        while (Current.Kind != TokenKind.CloseParen && !_tokens.AtEnd)
        {
            arguments.Add(ParseExpression());

            if (!_tokens.Match(TokenKind.Comma))
            {
                break;
            }
        }

        var end = Current.Span.End;

        if (!_tokens.Match(TokenKind.CloseParen))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado ')' para fechar a chamada");
            end = PreviousEnd();
        }

        return new CallExpression(callee, arguments.ToImmutable())
        {
            Span = SourceSpan.FromBounds(callee.Span.Start, end),
        };
    }

    private Expression ParsePrimary()
    {
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.IntegerLiteral:
                _tokens.Advance();
                return new IntLiteral(token.IntegerValue, token.Text) { Span = token.Span };

            case TokenKind.FloatLiteral:
                _tokens.Advance();
                return new FloatLiteral(token.FloatValue, token.Text) { Span = token.Span };

            case TokenKind.StringLiteral:
                _tokens.Advance();
                return new StrLiteral(token.StringValue) { Span = token.Span };

            case TokenKind.TrueKeyword:
            case TokenKind.FalseKeyword:
                _tokens.Advance();
                return new BoolLiteral(token.Kind == TokenKind.TrueKeyword) { Span = token.Span };

            case TokenKind.Identifier:
                _tokens.Advance();
                return new IdentifierExpression(token.Text) { Span = token.Span };

            case TokenKind.OpenParen:
                return ParseParenthesized();

            case TokenKind.OpenBrace:
                return ParseBlock();

            case TokenKind.FnKeyword:
                return ParseFunction();

            case TokenKind.IfKeyword:
                return ParseIf();

            case TokenKind.OpenBracket:
                return ParseArrayLiteral();

            case TokenKind.EnumKeyword:
                return ParseEnum();

            case TokenKind.MatchKeyword:
                return ParseMatch();

            case TokenKind.TypeKeyword:
                return ParseTypeDeclaration();

            case TokenKind.Dot:
                return ParseConstruct();

            default:
                var code = token.Kind == TokenKind.EndOfFile
                    ? DiagnosticCodes.UnexpectedEndOfFile
                    : DiagnosticCodes.ExpectedExpression;
                Report(code, token.Span, $"esperado uma expressão, encontrado {token.Kind.Describe()}");
                return ErrorExpr(token.Span);
        }
    }

    /// <summary><c>()</c> é o literal Void; <c>(e)</c> apenas agrupa.</summary>
    private Expression ParseParenthesized()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // '('

        if (Current.Kind == TokenKind.CloseParen)
        {
            _tokens.Advance();
            return new UnitLiteral { Span = SpanFrom(start) };
        }

        var inner = ParseExpression();

        if (!_tokens.Match(TokenKind.CloseParen))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado ')' para fechar a expressão");
        }

        return inner;
    }

    private Expression ParseArrayLiteral()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // '['

        var elements = ImmutableArray.CreateBuilder<Expression>();

        while (Current.Kind != TokenKind.CloseBracket && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();
            elements.Add(ParseExpression());

            if (!_tokens.Match(TokenKind.Comma))
            {
                break;
            }

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        if (!_tokens.Match(TokenKind.CloseBracket))
        {
            Report(DiagnosticCodes.ExpectedCloseBracket, Current.Span, "esperado ']' para fechar o array");
        }

        return new ArrayExpression(elements.ToImmutable()) { Span = SpanFrom(start) };
    }

    /// <summary>
    /// <c>enum&lt;T, E&gt; { Ok(T), Err(E) }</c>. O enum não tem nome próprio: o nome
    /// vem do <c>def</c> que o recebe (spec §15).
    /// </summary>
    private Expression ParseEnum()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'enum'

        var typeParameters = ParseTypeParameterList();
        var variants = ImmutableArray.CreateBuilder<VariantSyntax>();

        if (!_tokens.Match(TokenKind.OpenBrace))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '{' no corpo do enum");
            return new EnumExpression(typeParameters, variants.ToImmutable()) { Span = SpanFrom(start) };
        }

        while (Current.Kind != TokenKind.CloseBrace && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();
            variants.Add(ParseVariant());

            if (!_tokens.Match(TokenKind.Comma))
            {
                break;
            }

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        if (!_tokens.Match(TokenKind.CloseBrace))
        {
            Report(DiagnosticCodes.UnclosedBrace, Current.Span, "esperado '}' para fechar o enum");
        }

        return new EnumExpression(typeParameters, variants.ToImmutable()) { Span = SpanFrom(start) };
    }

    /// <summary><c>type&lt;T&gt; { campo: T; }</c> — declaração de tipo (spec §14).</summary>
    private Expression ParseTypeDeclaration()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'type'

        var typeParameters = ParseTypeParameterList();
        var fields = ImmutableArray.CreateBuilder<FieldSyntax>();

        if (!_tokens.Match(TokenKind.OpenBrace))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '{' no corpo do type");
            return new TypeExpression(typeParameters, fields.ToImmutable()) { Span = SpanFrom(start) };
        }

        while (Current.Kind != TokenKind.CloseBrace && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();
            var fieldStart = Current.Span.Start;
            var name = "?";

            if (Current.Kind == TokenKind.Identifier)
            {
                name = Current.Text;
                _tokens.Advance();
            }
            else
            {
                Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome do campo");
            }

            TypeSyntax type;

            if (_tokens.Match(TokenKind.Colon))
            {
                type = ParseType();
            }
            else
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, $"esperado ':' após o campo '{name}'");
                type = NamedTypeSyntax.Of("?", Current.Span);
            }

            if (!_tokens.Match(TokenKind.Semicolon))
            {
                Report(DiagnosticCodes.ExpectedFieldSemicolon, Current.Span, "campo de 'type' requer ';'");
            }

            fields.Add(new FieldSyntax(name, type) { Span = SpanFrom(fieldStart) });

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        if (!_tokens.Match(TokenKind.CloseBrace))
        {
            Report(DiagnosticCodes.UnclosedBrace, Current.Span, "esperado '}' para fechar o type");
        }

        return new TypeExpression(typeParameters, fields.ToImmutable()) { Span = SpanFrom(start) };
    }

    /// <summary>
    /// <c>.Nome { campo: valor }</c> — construção de instância (Q2).
    ///
    /// O ponto inicial é o que torna esta produção reconhecível com um único token
    /// de lookahead, dispensando qualquer restrição contextual em <c>if</c>/<c>match</c>.
    /// </summary>
    private Expression ParseConstruct()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // '.'

        var nameToken = Current;

        if (Current.Kind != TokenKind.Identifier)
        {
            Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome do tipo após '.'");
            return ErrorExpr(SpanFrom(start));
        }

        _tokens.Advance();
        var typeArguments = ImmutableArray<TypeSyntax>.Empty;

        if (Current.Kind == TokenKind.Less)
        {
            _tokens.Advance();
            var builder = ImmutableArray.CreateBuilder<TypeSyntax>();

            while (Current.Kind != TokenKind.Greater && !_tokens.AtEnd)
            {
                var before = _tokens.Mark();
                builder.Add(ParseType());

                if (!_tokens.Match(TokenKind.Comma))
                {
                    break;
                }

                if (_tokens.Mark() == before)
                {
                    _tokens.Advance();
                }
            }

            if (!_tokens.Match(TokenKind.Greater))
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '>' nos argumentos genéricos");
            }

            typeArguments = builder.ToImmutable();
        }

        var fields = ImmutableArray.CreateBuilder<FieldInitSyntax>();

        if (!_tokens.Match(TokenKind.OpenBrace))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '{' na construção");
            return new ConstructExpression(nameToken.Text, typeArguments, fields.ToImmutable())
            {
                Span = SpanFrom(start),
                TypeNameSpan = nameToken.Span,
            };
        }

        while (Current.Kind != TokenKind.CloseBrace && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();
            var fieldStart = Current.Span.Start;
            var fieldToken = Current;
            var name = "?";

            if (Current.Kind == TokenKind.Identifier)
            {
                name = Current.Text;
                _tokens.Advance();
            }
            else
            {
                Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome do campo");
            }

            if (!_tokens.Match(TokenKind.Colon))
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, $"esperado ':' após o campo '{name}'");
            }

            var value = ParseExpression();

            fields.Add(new FieldInitSyntax(name, value)
            {
                Span = SpanFrom(fieldStart),
                NameSpan = fieldToken.Span,
            });

            if (!_tokens.Match(TokenKind.Comma))
            {
                break;
            }

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        if (!_tokens.Match(TokenKind.CloseBrace))
        {
            Report(DiagnosticCodes.UnclosedBrace, Current.Span, "esperado '}' para fechar a construção");
        }

        return new ConstructExpression(nameToken.Text, typeArguments, fields.ToImmutable())
        {
            Span = SpanFrom(start),
            TypeNameSpan = nameToken.Span,
        };
    }

    /// <summary>
    /// <c>match e { padrão =&gt; corpo, ... }</c>. O escrutinado usa a gramática de
    /// expressão sem restrição: como a construção de tipo leva ponto inicial (Q2),
    /// não há como confundir o `{` do match com um literal de struct.
    /// </summary>
    private Expression ParseMatch()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'match'

        var scrutinee = ParseExpression();
        var arms = ImmutableArray.CreateBuilder<MatchArm>();

        if (!_tokens.Match(TokenKind.OpenBrace))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '{' no corpo do match");
            return new MatchExpression(scrutinee, arms.ToImmutable()) { Span = SpanFrom(start) };
        }

        while (Current.Kind != TokenKind.CloseBrace && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();
            var armStart = Current.Span.Start;
            var pattern = ParsePattern();

            if (!_tokens.Match(TokenKind.FatArrow))
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '=>' no braço do match");
            }

            var body = ParseExpression();
            arms.Add(new MatchArm(pattern, body) { Span = SpanFrom(armStart) });

            if (!_tokens.Match(TokenKind.Comma))
            {
                break;
            }

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        if (!_tokens.Match(TokenKind.CloseBrace))
        {
            Report(DiagnosticCodes.UnclosedBrace, Current.Span, "esperado '}' para fechar o match");
        }

        if (arms.Count == 0)
        {
            Report(DiagnosticCodes.MatchRequiresArm, SpanFrom(start), "'match' requer ao menos um braço");
        }

        return new MatchExpression(scrutinee, arms.ToImmutable()) { Span = SpanFrom(start) };
    }

    /// <summary>
    /// Q3 tornou os padrões não ambíguos: `x` é sempre binding novo, `A.B` é
    /// sempre variante. Nada aqui depende do escopo.
    /// </summary>
    private Pattern ParsePattern()
    {
        var start = Current.Span.Start;
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.Underscore:
                _tokens.Advance();
                return new WildcardPattern { Span = token.Span };

            case TokenKind.IntegerLiteral:
                _tokens.Advance();
                return new LiteralPattern(new ConstInt(token.IntegerValue)) { Span = token.Span };

            case TokenKind.FloatLiteral:
                _tokens.Advance();
                return new LiteralPattern(new ConstFloat(token.FloatValue)) { Span = token.Span };

            case TokenKind.StringLiteral:
                _tokens.Advance();
                return new LiteralPattern(new ConstStr(token.StringValue)) { Span = token.Span };

            case TokenKind.TrueKeyword:
            case TokenKind.FalseKeyword:
                _tokens.Advance();
                return new LiteralPattern(token.Kind == TokenKind.TrueKeyword ? ConstBool.True : ConstBool.False)
                {
                    Span = token.Span,
                };

            case TokenKind.Minus:
                return ParseNegativeLiteralPattern(start);

            case TokenKind.Identifier:
                return ParseNamePattern(start);

            default:
                Report(
                    DiagnosticCodes.ExpectedPattern,
                    token.Span,
                    $"esperado um padrão, encontrado {token.Kind.Describe()}");
                _tokens.Advance();
                return new WildcardPattern { Span = token.Span };
        }
    }

    private Pattern ParseNegativeLiteralPattern(int start)
    {
        _tokens.Advance(); // '-'
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.IntegerLiteral:
                _tokens.Advance();
                return new LiteralPattern(new ConstInt(-token.IntegerValue)) { Span = SpanFrom(start) };

            case TokenKind.FloatLiteral:
                _tokens.Advance();
                return new LiteralPattern(new ConstFloat(-token.FloatValue)) { Span = SpanFrom(start) };

            default:
                Report(DiagnosticCodes.ExpectedPattern, token.Span, "esperado um literal numérico após '-'");
                return new WildcardPattern { Span = SpanFrom(start) };
        }
    }

    private Pattern ParseNamePattern(int start)
    {
        var first = _tokens.Advance();

        // Sem ponto: é um binding novo.
        if (Current.Kind != TokenKind.Dot)
        {
            return new BindingPattern(first.Text) { Span = first.Span };
        }

        _tokens.Advance(); // '.'
        var variantToken = Current;

        if (Current.Kind != TokenKind.Identifier)
        {
            Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome da variante após '.'");
            return new WildcardPattern { Span = SpanFrom(start) };
        }

        _tokens.Advance();
        var arguments = ImmutableArray<Pattern>.Empty;

        if (_tokens.Match(TokenKind.OpenParen))
        {
            var builder = ImmutableArray.CreateBuilder<Pattern>();

            while (Current.Kind != TokenKind.CloseParen && !_tokens.AtEnd)
            {
                var before = _tokens.Mark();
                builder.Add(ParsePattern());

                if (!_tokens.Match(TokenKind.Comma))
                {
                    break;
                }

                if (_tokens.Mark() == before)
                {
                    _tokens.Advance();
                }
            }

            if (!_tokens.Match(TokenKind.CloseParen))
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado ')' no padrão de variante");
            }

            arguments = builder.ToImmutable();
        }

        return new VariantPattern(first.Text, variantToken.Text, arguments)
        {
            Span = SpanFrom(start),
            VariantSpan = variantToken.Span,
        };
    }

    private VariantSyntax ParseVariant()
    {
        var start = Current.Span.Start;
        var name = "?";

        if (Current.Kind == TokenKind.Identifier)
        {
            name = Current.Text;
            _tokens.Advance();
        }
        else
        {
            Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome da variante");
        }

        var payload = ImmutableArray<TypeSyntax>.Empty;

        if (_tokens.Match(TokenKind.OpenParen))
        {
            var builder = ImmutableArray.CreateBuilder<TypeSyntax>();

            while (Current.Kind != TokenKind.CloseParen && !_tokens.AtEnd)
            {
                var before = _tokens.Mark();
                builder.Add(ParseType());

                if (!_tokens.Match(TokenKind.Comma))
                {
                    break;
                }

                if (_tokens.Mark() == before)
                {
                    _tokens.Advance();
                }
            }

            if (!_tokens.Match(TokenKind.CloseParen))
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado ')' na carga da variante");
            }

            payload = builder.ToImmutable();
        }

        return new VariantSyntax(name, payload) { Span = SpanFrom(start) };
    }

    /// <summary>
    /// Parâmetros genéricos de uma declaração: sempre <b>nomeados</b> (Q1). Const
    /// generics (<c>N: Int</c>) chegam no M4.
    /// </summary>
    private ImmutableArray<TypeParameterSyntax> ParseTypeParameterList()
    {
        if (Current.Kind != TokenKind.Less)
        {
            return [];
        }

        _tokens.Advance();
        var parameters = ImmutableArray.CreateBuilder<TypeParameterSyntax>();

        while (Current.Kind != TokenKind.Greater && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();

            if (Current.Kind == TokenKind.Identifier)
            {
                parameters.Add(new TypeParameterSyntax(Current.Text) { Span = Current.Span });
                _tokens.Advance();
            }
            else
            {
                Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome do parâmetro genérico");
            }

            if (!_tokens.Match(TokenKind.Comma))
            {
                break;
            }

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        if (!_tokens.Match(TokenKind.Greater))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '>' nos parâmetros genéricos");
        }

        return parameters.ToImmutable();
    }

    private BlockExpression ParseBlock()
    {
        var start = Current.Span.Start;
        var openBrace = Current.Span;
        _tokens.Advance(); // '{'

        var statements = ImmutableArray.CreateBuilder<Statement>();
        Expression? tail = null;

        while (Current.Kind != TokenKind.CloseBrace && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();

            if (Current.Kind == TokenKind.DefKeyword)
            {
                statements.Add(ParseDefStatement());
            }
            else if (Current.Kind == TokenKind.Bad)
            {
                _tokens.Advance();
            }
            else
            {
                var statementStart = Current.Span.Start;
                var expression = ParseExpression();

                // Sem `;` e antes de `}`: é a cauda, o valor do bloco (spec §9).
                if (Current.Kind == TokenKind.CloseBrace)
                {
                    tail = expression;
                    break;
                }

                if (!_tokens.Match(TokenKind.Semicolon) && !IsBlockLike(expression))
                {
                    Report(DiagnosticCodes.ExpectedSemicolon, Current.Span, "esperado ';' ao final da declaração");
                    RecoverToStatementBoundary();
                }

                statements.Add(new ExpressionStatement(expression)
                {
                    Span = SourceSpan.FromBounds(statementStart, PreviousEnd()),
                });
            }

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        if (!_tokens.Match(TokenKind.CloseBrace))
        {
            Report(DiagnosticCodes.UnclosedBrace, openBrace, "'{' sem '}' correspondente");
        }

        return new BlockExpression(statements.ToImmutable(), tail) { Span = SpanFrom(start) };
    }

    private Expression ParseFunction()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'fn'

        var parameters = ParseParameterList();
        TypeSyntax? returnType = null;

        if (Current.Kind != TokenKind.OpenBrace)
        {
            returnType = ParseType();
        }

        BlockExpression body;

        if (Current.Kind == TokenKind.OpenBrace)
        {
            body = ParseBlock();
        }
        else
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '{' para o corpo da função");
            body = new BlockExpression([], null) { Span = Current.Span };
        }

        return new FunctionExpression(parameters, returnType, body) { Span = SpanFrom(start) };
    }

    private ImmutableArray<ParameterSyntax> ParseParameterList()
    {
        if (!_tokens.Match(TokenKind.OpenParen))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '(' na lista de parâmetros");
            return [];
        }

        var parameters = ImmutableArray.CreateBuilder<ParameterSyntax>();

        while (Current.Kind != TokenKind.CloseParen && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();
            var parameterStart = Current.Span.Start;
            var name = "?";

            if (Current.Kind == TokenKind.Identifier)
            {
                name = Current.Text;
                _tokens.Advance();
            }
            else
            {
                Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome do parâmetro");
            }

            TypeSyntax type;

            if (_tokens.Match(TokenKind.Colon))
            {
                type = ParseType();
            }
            else
            {
                Report(
                    DiagnosticCodes.ParameterRequiresType,
                    Current.Span,
                    $"o parâmetro '{name}' requer anotação de tipo");
                type = NamedTypeSyntax.Of("?", Current.Span);
            }

            parameters.Add(new ParameterSyntax(name, type) { Span = SpanFrom(parameterStart) });

            if (!_tokens.Match(TokenKind.Comma))
            {
                break;
            }

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        if (!_tokens.Match(TokenKind.CloseParen))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado ')' na lista de parâmetros");
        }

        return parameters.ToImmutable();
    }

    private Expression ParseIf()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'if'

        var condition = ParseExpression();

        BlockExpression then;

        if (Current.Kind == TokenKind.OpenBrace)
        {
            then = ParseBlock();
        }
        else
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '{' após a condição do 'if'");
            then = new BlockExpression([], null) { Span = Current.Span };
        }

        Expression? elseBranch = null;

        if (_tokens.Match(TokenKind.ElseKeyword))
        {
            if (Current.Kind == TokenKind.IfKeyword)
            {
                elseBranch = ParseIf();
            }
            else if (Current.Kind == TokenKind.OpenBrace)
            {
                elseBranch = ParseBlock();
            }
            else
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '{' ou 'if' após 'else'");
                elseBranch = ErrorExpr(Current.Span);
            }
        }

        return new IfExpression(condition, then, elseBranch) { Span = SpanFrom(start) };
    }

    // --------------------------------------------------------------- tipos

    private TypeSyntax ParseType()
    {
        var type = ParseTypePrimary();

        while (Current.Kind == TokenKind.OpenBracket)
        {
            var start = type.Span.Start;
            _tokens.Advance();

            if (!_tokens.Match(TokenKind.CloseBracket))
            {
                Report(DiagnosticCodes.ExpectedCloseBracket, Current.Span, "esperado ']' no tipo de array");
            }

            type = new ArrayTypeSyntax(type) { Span = SpanFrom(start) };
        }

        return type;
    }

    private TypeSyntax ParseTypePrimary()
    {
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.Identifier:
                return ParseNamedType();

            case TokenKind.FnKeyword:
                return ParseFunctionType();

            case TokenKind.OpenParen:
                {
                    _tokens.Advance();
                    var inner = ParseType();

                    if (!_tokens.Match(TokenKind.CloseParen))
                    {
                        Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado ')' no tipo");
                    }

                    return inner;
                }

            default:
                Report(
                    DiagnosticCodes.ExpectedType,
                    token.Span,
                    $"esperado um tipo, encontrado {token.Kind.Describe()}");
                return NamedTypeSyntax.Of("?", token.Span);
        }
    }

    /// <summary>
    /// <c>Nome</c> ou <c>Nome&lt;A, B&gt;</c>. Em posição de tipo não há ambiguidade
    /// com o operador `&lt;`: a gramática de tipos não tem comparação.
    /// </summary>
    private TypeSyntax ParseNamedType()
    {
        var token = _tokens.Advance();
        var arguments = ImmutableArray<TypeSyntax>.Empty;

        if (Current.Kind == TokenKind.Less)
        {
            _tokens.Advance();
            var builder = ImmutableArray.CreateBuilder<TypeSyntax>();

            while (Current.Kind != TokenKind.Greater && !_tokens.AtEnd)
            {
                var before = _tokens.Mark();
                builder.Add(ParseType());

                if (!_tokens.Match(TokenKind.Comma))
                {
                    break;
                }

                if (_tokens.Mark() == before)
                {
                    _tokens.Advance();
                }
            }

            if (builder.Count == 0)
            {
                Report(
                    DiagnosticCodes.EmptyGenericArgumentList,
                    Current.Span,
                    "lista de argumentos genéricos vazia");
            }

            if (!_tokens.Match(TokenKind.Greater))
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '>' nos argumentos genéricos");
            }

            arguments = builder.ToImmutable();
        }

        return new NamedTypeSyntax(token.Text, arguments)
        {
            Span = SourceSpan.FromBounds(token.Span.Start, PreviousEnd()),
        };
    }

    private TypeSyntax ParseFunctionType()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'fn'

        var parameters = ImmutableArray.CreateBuilder<TypeSyntax>();

        if (!_tokens.Match(TokenKind.OpenParen))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '(' no tipo de função");
        }
        else
        {
            while (Current.Kind != TokenKind.CloseParen && !_tokens.AtEnd)
            {
                var before = _tokens.Mark();
                parameters.Add(ParseType());

                if (!_tokens.Match(TokenKind.Comma))
                {
                    break;
                }

                if (_tokens.Mark() == before)
                {
                    _tokens.Advance();
                }
            }

            if (!_tokens.Match(TokenKind.CloseParen))
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado ')' no tipo de função");
            }
        }

        var returnType = ParseType();

        return new FunctionTypeSyntax(parameters.ToImmutable(), returnType) { Span = SpanFrom(start) };
    }

    // ------------------------------------------------------------ auxiliar

    private static bool TryGetBinaryOperator(TokenKind kind, out BinaryOperator op)
    {
        op = kind switch
        {
            TokenKind.Plus => BinaryOperator.Add,
            TokenKind.Minus => BinaryOperator.Subtract,
            TokenKind.Star => BinaryOperator.Multiply,
            TokenKind.Slash => BinaryOperator.Divide,
            TokenKind.EqualsEquals => BinaryOperator.Equal,
            TokenKind.BangEquals => BinaryOperator.NotEqual,
            TokenKind.Less => BinaryOperator.Less,
            TokenKind.Greater => BinaryOperator.Greater,
            TokenKind.LessEquals => BinaryOperator.LessOrEqual,
            TokenKind.GreaterEquals => BinaryOperator.GreaterOrEqual,
            TokenKind.AmpersandAmpersand => BinaryOperator.AndAlso,
            TokenKind.PipePipe => BinaryOperator.OrElse,
            _ => (BinaryOperator)(-1),
        };

        return (int)op >= 0;
    }

    private static Expression ErrorExpr(SourceSpan span) => new ErrorExpression { Span = span };

    private SourceSpan SpanFrom(int start) => SourceSpan.FromBounds(start, PreviousEnd());

    private int PreviousEnd() => _tokens.Peek(-1).Span.End;

    private void Report(string code, SourceSpan span, string message) =>
        _diagnostics.ReportError(code, span, message);
}

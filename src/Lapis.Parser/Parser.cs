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
    private int _speculating;

    /// <summary>
    /// Ligado quando <see cref="MaxDepth"/> estoura. As ~200 chamadas recursivas
    /// que ainda vão desempilhar reportariam, cada uma, o seu <c>)</c> faltante —
    /// uma cascata que enterra o único erro que o autor precisa ler. Desliga ao
    /// voltar ao nível de statement, onde a análise volta a ser confiável.
    /// </summary>
    private bool _unwindingFromDepthLimit;

    private Parser(TokenStream tokens, DiagnosticBag diagnostics)
    {
        _tokens = tokens;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Um parser posicionado sobre uma lista de tokens arbitrária, para o matcher
    /// de macros (plano 17 §17.5). Fica <c>internal</c> porque só o
    /// <see cref="SyntaxFragmentReader"/> deste projeto tem por que usá-lo.
    /// </summary>
    internal static Parser OverFragment(ImmutableArray<Token> tokens, DiagnosticBag diagnostics) =>
        new(new TokenStream(tokens), diagnostics);

    internal TokenStream Tokens => _tokens;

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

    internal Statement? ParseStatement()
    {
        _unwindingFromDepthLimit = false;

        if (Current.Kind is TokenKind.DefKeyword or TokenKind.VarKeyword)
        {
            return ParseDefStatement();
        }

        if (AtAssignment())
        {
            return ParseAssignStatement();
        }

        if (Current.Kind == TokenKind.MacroKeyword)
        {
            return ParseMacroDeclaration();
        }

        if (Current.Kind == TokenKind.GotoKeyword)
        {
            return ParseGotoStatement();
        }

        if (AtLabelStatement())
        {
            return ParseLabelStatement();
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
            Span = SpanFrom(start),
        };
    }

    /// <summary>
    /// Expressões que terminam em bloco dispensam o <c>;</c> quando usadas como
    /// statement. Sem essa regra o próprio exemplo <c>abs</c> da spec §12 —
    /// <c>if x &lt; 0 { return -x; }</c> seguido de <c>return x;</c> — não parsearia.
    /// </summary>
    private static bool IsBlockLike(Expression expression) =>
        expression is BlockExpression or IfExpression or MatchExpression
        // Uma invocação que terminou em bloco dispensa `;` pelo mesmo motivo que
        // `if p { }` dispensa: `@unless c { }` é a forma que a macro define, e
        // exigir `;` ali contrariaria a sintaxe que ela escolheu (spec de macros §4).
        || expression is MacroInvocation { Arguments: [.., { Kind: TokenKind.CloseBrace }] };

    /// <summary>
    /// <c>IDENT "=" expressão ";"</c>. Não há ambiguidade a resolver: <c>==</c> é
    /// outro token, e um identificador seguido de <c>=</c> em posição de statement
    /// não pode ser mais nada.
    /// </summary>
    private bool AtAssignment() =>
        Current.Kind == TokenKind.Identifier && _tokens.Peek(1).Kind == TokenKind.Equals;

    private Statement ParseAssignStatement()
    {
        var start = Current.Span.Start;
        var nameToken = _tokens.Advance();
        _tokens.Advance(); // '='

        var value = ParseExpression();

        if (!ExpectSemicolon(start))
        {
            RecoverToStatementBoundary();
        }

        return new AssignStatement(nameToken.Text, value)
        {
            Span = SpanFrom(start),
            NameSpan = nameToken.Span,
        };
    }

    private Statement ParseDefStatement()
    {
        var start = Current.Span.Start;
        var isMutable = Current.Kind == TokenKind.VarKeyword;
        _tokens.Advance(); // 'def' ou 'var'

        var nameToken = Current;
        var name = "?";

        if (Current.Kind == TokenKind.Identifier)
        {
            name = Current.Text;
            _tokens.Advance();
        }
        else
        {
            Report(
                DiagnosticCodes.ExpectedIdentifier,
                Current.Span,
                $"esperado um identificador após '{(isMutable ? "var" : "def")}'");
        }

        TypeSyntax? annotation = null;

        if (_tokens.Match(TokenKind.Colon))
        {
            annotation = ParseType();
        }

        Expression value;

        if (Current.Kind != TokenKind.Equals)
        {
            Report(
                DiagnosticCodes.ExpectedEquals,
                Current.Span,
                $"esperado '=' em '{(isMutable ? "var" : "def")}'");
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
            Span = SpanFrom(start),
            NameSpan = nameToken.Span,
            IsMutable = isMutable,
        };
    }

    // -------------------------------------------------------------- macros

    /// <summary>
    /// <c>macro nome (match &lt;padrão&gt; (constraint bloco)? expand bloco)+ ";"</c>
    /// (spec de macros §3).
    /// </summary>
    private Statement ParseMacroDeclaration()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'macro'

        var nameToken = Current;
        var name = "?";

        if (Current.Kind == TokenKind.Identifier)
        {
            name = Current.Text;
            _tokens.Advance();
        }
        else
        {
            Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado um nome de macro após 'macro'");
        }

        var rules = ImmutableArray.CreateBuilder<MacroRule>();

        while (Current.Kind == TokenKind.MatchKeyword)
        {
            rules.Add(ParseMacroRule());
        }

        if (rules.Count == 0)
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "uma macro precisa de ao menos um 'match'");
        }

        if (!ExpectSemicolon(start))
        {
            RecoverToStatementBoundary();
        }

        return new MacroDeclaration(name, rules.ToImmutable())
        {
            Span = SpanFrom(start),
            NameSpan = nameToken.Span,
        };
    }

    private MacroRule ParseMacroRule()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'match'

        var pattern = ParseMacroPattern();

        BlockExpression? constraint = null;

        if (_tokens.Match(TokenKind.ConstraintKeyword))
        {
            constraint = ParseMacroBody("constraint");
        }

        if (Current.Kind != TokenKind.ExpandKeyword)
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado 'expand' na regra da macro");

            return new MacroRule(pattern, constraint, EmptyBlock(Current.Span)) { Span = SpanFrom(start) };
        }

        _tokens.Advance(); // 'expand'

        return new MacroRule(pattern, constraint, ParseMacroBody("expand")) { Span = SpanFrom(start) };
    }

    private BlockExpression ParseMacroBody(string keyword)
    {
        if (Current.Kind == TokenKind.OpenBrace)
        {
            return ParseBlock();
        }

        Report(DiagnosticCodes.UnexpectedToken, Current.Span, $"esperado '{{' depois de '{keyword}'");
        return EmptyBlock(Current.Span);
    }

    private static BlockExpression EmptyBlock(SourceSpan span) => new([], null) { Span = span };

    /// <summary>
    /// O padrão vai do <c>match</c> até o <c>constraint</c> ou o <c>expand</c>.
    ///
    /// Nada aqui é a gramática da linguagem: é uma sequência de capturas e de
    /// tokens literais, e é justamente isso que permite a uma macro definir
    /// sintaxe própria sem tocar no lexer (spec de macros §5).
    /// </summary>
    private MacroPattern ParseMacroPattern()
    {
        var start = Current.Span.Start;
        var items = ImmutableArray.CreateBuilder<MacroPattern>();

        while (Current.Kind is not (TokenKind.ExpandKeyword or TokenKind.ConstraintKeyword
            or TokenKind.Semicolon or TokenKind.EndOfFile))
        {
            var before = _tokens.Mark();
            items.Add(ParseMacroPatternItem());

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        return new PatternSequence(items.ToImmutable()) { Span = SpanFrom(start) };
    }

    private MacroPattern ParseMacroPatternItem()
    {
        var start = Current.Span.Start;

        // `( sub-padrão )* separado por <token>` — repetição de grupo.
        if (Current.Kind == TokenKind.OpenParen)
        {
            _tokens.Advance();
            var group = ImmutableArray.CreateBuilder<MacroPattern>();

            while (Current.Kind is not (TokenKind.CloseParen or TokenKind.EndOfFile))
            {
                var before = _tokens.Mark();
                group.Add(ParseMacroPatternItem());

                if (_tokens.Mark() == before)
                {
                    _tokens.Advance();
                }
            }

            if (!_tokens.Match(TokenKind.CloseParen))
            {
                Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado ')' no grupo do padrão");
            }

            var inner = new PatternSequence(group.ToImmutable()) { Span = SpanFrom(start) };
            return FinishRepeat(inner, start);
        }

        // `Categoria:nome` — captura.
        if (Current.Kind == TokenKind.Identifier && _tokens.Peek(1).Kind == TokenKind.Colon)
        {
            var categoryToken = _tokens.Advance();
            _tokens.Advance(); // ':'

            var capturedName = Current.Kind == TokenKind.Identifier ? _tokens.Advance().Text : "?";

            if (!Enum.TryParse<SyntaxCategory>(categoryToken.Text, ignoreCase: false, out var category))
            {
                Report(
                    DiagnosticCodes.UnknownSyntaxCategory,
                    categoryToken.Span,
                    $"categoria sintática desconhecida: '{categoryToken.Text}'",
                    new DiagnosticNote(
                        "categorias: " + string.Join(", ", Enum.GetNames<SyntaxCategory>())));
            }

            var capture = new PatternCapture(category, capturedName) { Span = SpanFrom(start) };
            return FinishRepeat(capture, start);
        }

        // Qualquer outro token é literal sintático desta macro.
        var literal = _tokens.Advance();
        return new PatternLiteral(literal.Text) { Span = literal.Span };
    }

    /// <summary><c>* separado por &lt;token&gt;</c>, quando houver.</summary>
    private MacroPattern FinishRepeat(MacroPattern item, int start)
    {
        if (Current.Kind != TokenKind.Star)
        {
            return item;
        }

        _tokens.Advance(); // '*'

        // `separado` e `por` são identificadores comuns: a repetição é sintaxe da
        // declaração de macro, não da linguagem, então não há palavra a reservar.
        if (Current.Kind == TokenKind.Identifier && Current.Text == "separado")
        {
            _tokens.Advance();

            if (Current.Kind == TokenKind.Identifier && Current.Text == "por")
            {
                _tokens.Advance();
            }
        }

        var separator = Current.Kind is TokenKind.ExpandKeyword or TokenKind.ConstraintKeyword
            ? ","
            : _tokens.Advance().Text;

        return new PatternRepeat(item, separator) { Span = SpanFrom(start) };
    }

    /// <summary>
    /// <c>@nome &lt;tokens&gt;</c>.
    ///
    /// O parser não conhece a forma da macro, então só <b>delimita</b>: consome
    /// até o <c>;</c> de nível 0 de aninhamento, ou até o <c>}</c> que fecha um
    /// bloco aberto no nível 0. Também para em <c>,</c> e <c>)</c> de nível 0,
    /// para que <c>f(@foo a, b)</c> não engula o resto da chamada.
    /// </summary>
    private Expression ParseMacroInvocation()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // '@'

        var nameToken = Current;
        var name = "?";

        if (Current.Kind == TokenKind.Identifier)
        {
            name = Current.Text;
            _tokens.Advance();
        }
        else
        {
            Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado um nome de macro após '@'");
        }

        var arguments = ImmutableArray.CreateBuilder<Token>();
        var depth = 0;

        while (!_tokens.AtEnd)
        {
            var token = Current;

            switch (token.Kind)
            {
                case TokenKind.Semicolon when depth == 0:
                case TokenKind.Comma when depth == 0:
                case TokenKind.CloseParen when depth == 0:
                case TokenKind.CloseBracket when depth == 0:
                case TokenKind.CloseBrace when depth == 0:
                    return Invocation();

                case TokenKind.OpenParen or TokenKind.OpenBrace or TokenKind.OpenBracket:
                    depth++;
                    break;

                case TokenKind.CloseParen or TokenKind.CloseBrace or TokenKind.CloseBracket:
                    depth--;
                    break;
            }

            arguments.Add(_tokens.Advance());

            // Um bloco aberto no nível 0 e fechado encerra a invocação: é a forma
            // `@unless c { ... }`, que não termina em `;`.
            if (depth == 0 && token.Kind != TokenKind.OpenBrace && arguments[^1].Kind == TokenKind.CloseBrace)
            {
                return Invocation();
            }
        }

        return Invocation();

        Expression Invocation() => new MacroInvocation(name, arguments.ToImmutable())
        {
            Span = SpanFrom(start),
            NameSpan = nameToken.Span,
        };
    }

    /// <summary>
    /// <c>goto IDENT ("if" expressão)? ";"</c>.
    ///
    /// O <c>if</c> reaproveita <see cref="TokenKind.IfKeyword"/> sem ambiguidade:
    /// o <c>goto</c> já determinou a produção.
    /// </summary>
    private Statement ParseGotoStatement()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'goto'

        var (label, labelSpan) = ParseLabelName();

        Expression? condition = null;

        if (_tokens.Match(TokenKind.IfKeyword))
        {
            condition = ParseExpression();
        }

        if (!ExpectSemicolon(start))
        {
            RecoverToStatementBoundary();
        }

        return new GotoStatement(label, condition)
        {
            Span = SpanFrom(start),
            LabelSpan = labelSpan,
        };
    }

    /// <summary>
    /// <c>label</c> é palavra-chave <b>contextual</b>: só vale quando inicia um
    /// statement e vem seguida de um identificador.
    ///
    /// Reservá-la quebraria programa válido — o exemplo da própria spec §13 usa
    /// <c>label</c> como nome de campo (<c>type&lt;Label: Str, ...&gt; { label: Str; }</c>).
    /// A ambiguidade não existe: dois identificadores seguidos nunca formam uma
    /// expressão. <c>goto</c>, ao contrário, é reservada — ninguém a usa como nome,
    /// e reservá-la é o que permite dizer "esperado um rótulo" em vez de deixar a
    /// linha virar uma expressão malformada.
    /// </summary>
    private bool AtLabelStatement() =>
        Current.Kind == TokenKind.Identifier
        && Current.Text == "label"
        && _tokens.Peek(1).Kind == TokenKind.Identifier;

    private Statement ParseLabelStatement()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'label'

        var (label, labelSpan) = ParseLabelName();

        if (!ExpectSemicolon(start))
        {
            RecoverToStatementBoundary();
        }

        return new LabelStatement(label)
        {
            Span = SpanFrom(start),
            LabelSpan = labelSpan,
        };
    }

    /// <summary>
    /// Rótulos são identificadores comuns (spec §7). Vivem num espaço de nomes
    /// separado do de valores, então <c>label x</c> e <c>def x</c> convivem.
    /// </summary>
    private (string Name, SourceSpan Span) ParseLabelName()
    {
        var token = Current;

        if (token.Kind != TokenKind.Identifier)
        {
            Report(DiagnosticCodes.ExpectedIdentifier, token.Span, "esperado um rótulo");
            return ("?", token.Span);
        }

        _tokens.Advance();
        return (token.Text, token.Span);
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

                case TokenKind.DefKeyword or TokenKind.VarKeyword or TokenKind.MacroKeyword when depth == 0:
                    return;

                case TokenKind.CloseBrace when depth == 0:
                    return;

                case TokenKind.OpenBrace or TokenKind.OpenParen or TokenKind.OpenBracket:
                    depth++;
                    break;

                case TokenKind.CloseBrace or TokenKind.CloseParen or TokenKind.CloseBracket:
                    // Nunca negativo: quando a recuperação começa já dentro de
                    // parênteses, os fechamentos excedentes levariam o contador
                    // abaixo de zero e nenhum `;` voltaria a contar como limite —
                    // a recuperação engoliria o resto do arquivo.
                    depth = Math.Max(0, depth - 1);
                    break;
            }

            _tokens.Advance();
        }
    }

    // -------------------------------------------------------- expressions

    internal Expression ParseExpression()
    {
        if (++_depth > MaxDepth)
        {
            _depth--;
            Report(
                DiagnosticCodes.ExpressionTooDeep,
                Current.Span,
                $"expressão aninhada profundamente demais (limite {MaxDepth})");
            RecoverToStatementBoundary();
            _unwindingFromDepthLimit = true;
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

                case TokenKind.Less when TryParseInstantiation(out var arguments):
                    expression = new InstantiateExpression(expression, arguments)
                    {
                        Span = SpanFrom(expression.Span.Start),
                    };
                    continue;

                default:
                    return expression;
            }
        }
    }

    /// <summary>
    /// Q5 — a ambiguidade entre <c>f&lt;Int&gt;(x)</c> e <c>a &lt; b</c>.
    ///
    /// O parser tenta consumir uma lista de argumentos genéricos e aceita quando o
    /// token seguinte ao <c>&gt;</c> de fechamento é:
    ///
    /// <list type="bullet">
    /// <item><c>(</c> — uma chamada: <c>identity&lt;Int&gt;(10)</c>;</item>
    /// <item><c>.</c> — uma variante: <c>Result&lt;Int, E&gt;.Ok(1)</c>;</item>
    /// <item>qualquer token que <b>não</b> inicia uma expressão — aí a leitura
    /// relacional não teria operando à direita e só resta a genérica, que é o que
    /// faz <c>def t = SomeType&lt;"v", 1&gt;;</c> (spec §13) parsear.</item>
    /// </list>
    ///
    /// Caso contrário devolve o cursor e <c>&lt;</c> volta a ser o operador
    /// relacional. A tentativa consome no máximo os tokens da lista, então o custo
    /// é linear: <c>a &lt; b &lt; c</c> falha no segundo token e não recomeça.
    /// </summary>
    private bool TryParseInstantiation(out ImmutableArray<GenericArgumentSyntax> arguments)
    {
        var mark = _tokens.Mark();

        _speculating++;
        var parsed = TryConsumeGenericArguments(out arguments);
        _speculating--;

        if (parsed
            && (Current.Kind is TokenKind.OpenParen or TokenKind.Dot
                || !Current.Kind.CanBeginExpression()))
        {
            return true;
        }

        _tokens.Reset(mark);
        arguments = [];
        return false;
    }

    /// <summary>
    /// Consome <c>&lt;A, B&gt;</c> sem recuperação de erro: qualquer desvio devolve
    /// <c>false</c> e o chamador restaura o cursor.
    /// </summary>
    private bool TryConsumeGenericArguments(out ImmutableArray<GenericArgumentSyntax> arguments)
    {
        arguments = [];
        _tokens.Advance(); // '<'

        var builder = ImmutableArray.CreateBuilder<GenericArgumentSyntax>();

        while (Current.Kind != TokenKind.Greater)
        {
            if (_tokens.AtEnd)
            {
                return false;
            }

            var before = _tokens.Mark();
            builder.Add(ParseGenericArgument(typePosition: false));

            // Nenhum token consumido significa que não havia argumento algum.
            if (_tokens.Mark() == before)
            {
                return false;
            }

            if (!_tokens.Match(TokenKind.Comma))
            {
                break;
            }
        }

        if (builder.Count == 0 || !_tokens.Match(TokenKind.Greater))
        {
            return false;
        }

        arguments = builder.ToImmutable();
        return true;
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
                Span = SpanFrom(target.Span.Start),
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

            case TokenKind.At:
                return ParseMacroInvocation();

            case TokenKind.MacroKeyword:
                // Q19: macro não é valor. `def m = macro ...` diria o contrário.
                Report(
                    DiagnosticCodes.MacroIsNotAValue,
                    token.Span,
                    "uma macro não é um valor e não pode aparecer em posição de expressão",
                    new DiagnosticNote("declare-a no topo: 'macro nome match ... expand { ... };'"));
                RecoverToStatementBoundary();
                return ErrorExpr(token.Span);

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

        var typeArguments = Current.Kind == TokenKind.Less
            ? ParseGenericArgumentList(typePosition: false)
            : ImmutableArray<GenericArgumentSyntax>.Empty;

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
    /// Parâmetros genéricos de uma <b>declaração</b>: sempre nomeados (Q1).
    /// <c>T</c> é parâmetro de tipo; <c>N: Int</c> é parâmetro const (spec §13).
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
            var start = Current.Span.Start;
            var name = "?";

            if (Current.Kind == TokenKind.Identifier)
            {
                name = Current.Text;
                _tokens.Advance();
            }
            else
            {
                Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome do parâmetro genérico");
            }

            var constType = _tokens.Match(TokenKind.Colon) ? ParseType() : null;

            parameters.Add(new TypeParameterSyntax(name, constType) { Span = SpanFrom(start) });

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

    /// <summary>
    /// Argumentos genéricos de um <b>uso</b>, com recuperação de erro. A versão
    /// especulativa é <see cref="TryConsumeGenericArguments"/>.
    /// </summary>
    private ImmutableArray<GenericArgumentSyntax> ParseGenericArgumentList(bool typePosition)
    {
        _tokens.Advance(); // '<'

        var arguments = ImmutableArray.CreateBuilder<GenericArgumentSyntax>();

        while (Current.Kind != TokenKind.Greater && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();
            arguments.Add(ParseGenericArgument(typePosition));

            if (!_tokens.Match(TokenKind.Comma))
            {
                break;
            }

            if (_tokens.Mark() == before)
            {
                _tokens.Advance();
            }
        }

        if (arguments.Count == 0)
        {
            Report(DiagnosticCodes.EmptyGenericArgumentList, Current.Span, "lista de argumentos genéricos vazia");
        }

        if (!_tokens.Match(TokenKind.Greater))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '>' nos argumentos genéricos");
        }

        return arguments.ToImmutable();
    }

    /// <summary>
    /// Um argumento genérico é um tipo ou um valor constante (spec §13). Um
    /// identificador nu é os dois ao mesmo tempo — <c>Foo&lt;N&gt;</c> não diz se
    /// <c>N</c> nomeia um tipo ou uma constante — e sai daqui como
    /// <see cref="NameArgumentSyntax"/> para o checker desempatar pelo escopo.
    ///
    /// Em posição de <b>tipo</b>, <c>fn(Int) Int</c> é sempre um tipo de função:
    /// uma função literal só é escrevível como argumento em posição de expressão
    /// (Q17).
    /// </summary>
    private GenericArgumentSyntax ParseGenericArgument(bool typePosition)
    {
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.IntegerLiteral:
            case TokenKind.FloatLiteral:
            case TokenKind.StringLiteral:
            case TokenKind.TrueKeyword:
            case TokenKind.FalseKeyword:
            case TokenKind.Minus:
                return ParseConstArgument();

            case TokenKind.FnKeyword when !typePosition:
                return ParseFunctionArgument();

            // Só um IDENT sozinho é ambíguo: `Foo<Bar>` e `Int[]` são tipos.
            case TokenKind.Identifier when _tokens.Peek(1).Kind is TokenKind.Comma or TokenKind.Greater:
                _tokens.Advance();
                return new NameArgumentSyntax(token.Text) { Span = token.Span };

            default:
                var type = ParseType();
                return new TypeArgumentSyntax(type) { Span = type.Span };
        }
    }

    /// <summary>
    /// Literal em posição de argumento genérico. O sinal é absorvido no literal,
    /// como no desugar de <c>-10</c>: assim <c>Foo&lt;-1&gt;</c> carrega uma
    /// constante, e não uma expressão a avaliar.
    /// </summary>
    private GenericArgumentSyntax ParseConstArgument()
    {
        var start = Current.Span.Start;
        var negated = _tokens.Match(TokenKind.Minus);
        var token = Current;

        Func<SourceSpan, Expression>? literal = null;

        switch (token.Kind)
        {
            case TokenKind.IntegerLiteral:
                _tokens.Advance();
                var integer = negated ? -token.IntegerValue : token.IntegerValue;
                literal = span => new IntLiteral(integer, token.Text) { Span = span };
                break;

            case TokenKind.FloatLiteral:
                _tokens.Advance();
                var real = negated ? -token.FloatValue : token.FloatValue;
                literal = span => new FloatLiteral(real, token.Text) { Span = span };
                break;

            case TokenKind.StringLiteral when !negated:
                _tokens.Advance();
                literal = span => new StrLiteral(token.StringValue) { Span = span };
                break;

            case TokenKind.TrueKeyword or TokenKind.FalseKeyword when !negated:
                _tokens.Advance();
                var flag = token.Kind == TokenKind.TrueKeyword;
                literal = span => new BoolLiteral(flag) { Span = span };
                break;
        }

        if (literal is null)
        {
            Report(DiagnosticCodes.ExpectedExpression, token.Span, "esperado um literal numérico após '-'");
            return new TypeArgumentSyntax(NamedTypeSyntax.Of("?", token.Span)) { Span = SpanFrom(start) };
        }

        var argumentSpan = SpanFrom(start);

        return new ValueArgumentSyntax(literal(argumentSpan)) { Span = argumentSpan };
    }

    /// <summary>
    /// <c>fn(Int) Int</c> é um tipo; <c>fn() Int { return 1; }</c> é um valor
    /// (spec §13). O discriminador é o corpo: só o valor tem <c>{</c>.
    /// </summary>
    private GenericArgumentSyntax ParseFunctionArgument()
    {
        var mark = _tokens.Mark();

        _speculating++;
        var type = ParseType();
        _speculating--;

        if (Current.Kind is TokenKind.Comma or TokenKind.Greater)
        {
            return new TypeArgumentSyntax(type) { Span = type.Span };
        }

        _tokens.Reset(mark);
        var value = ParseFunction();

        return new ValueArgumentSyntax(value) { Span = value.Span };
    }

    internal BlockExpression ParseBlock()
    {
        var start = Current.Span.Start;
        var openBrace = Current.Span;
        _tokens.Advance(); // '{'

        var statements = ImmutableArray.CreateBuilder<Statement>();
        Expression? tail = null;

        while (Current.Kind != TokenKind.CloseBrace && !_tokens.AtEnd)
        {
            var before = _tokens.Mark();

            if (Current.Kind is TokenKind.DefKeyword or TokenKind.VarKeyword)
            {
                statements.Add(ParseDefStatement());
            }
            else if (AtAssignment())
            {
                statements.Add(ParseAssignStatement());
            }
            else if (Current.Kind == TokenKind.MacroKeyword)
            {
                statements.Add(ParseMacroDeclaration());
            }
            else if (Current.Kind == TokenKind.GotoKeyword)
            {
                statements.Add(ParseGotoStatement());
            }
            else if (AtLabelStatement())
            {
                statements.Add(ParseLabelStatement());
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
                    Span = SpanFrom(statementStart),
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

        var typeParameters = ParseTypeParameterList();
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

        return new FunctionExpression(typeParameters, parameters, returnType, body) { Span = SpanFrom(start) };
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

    internal TypeSyntax ParseType()
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

        var arguments = Current.Kind == TokenKind.Less
            ? ParseGenericArgumentList(typePosition: true)
            : ImmutableArray<GenericArgumentSyntax>.Empty;

        return new NamedTypeSyntax(token.Text, arguments)
        {
            Span = SpanFrom(token.Span.Start),
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

    /// <summary>
    /// Do início da construção até o último token consumido.
    ///
    /// O limite nunca inverte: quando a recuperação rebobina o fluxo para antes de
    /// <paramref name="start"/>, o fim fica sendo o próprio início — um span vazio
    /// ali é a resposta honesta, e o parser não pode lançar por causa de um
    /// programa mal escrito (plano 04).
    /// </summary>
    private SourceSpan SpanFrom(int start) => SourceSpan.FromBounds(start, Math.Max(start, PreviousEnd()));

    private int PreviousEnd() => _tokens.Peek(-1).Span.End;

    /// <summary>
    /// Durante uma tentativa especulativa (Q5) nada é reportado: o parse pode
    /// falhar de propósito e o texto ainda ser um programa perfeitamente válido
    /// sob a outra leitura. Durante o desempilhamento do limite de profundidade
    /// também não: ver <see cref="_unwindingFromDepthLimit"/>.
    /// </summary>
    private void Report(string code, SourceSpan span, string message, params DiagnosticNote[] notes)
    {
        if (_speculating == 0 && !_unwindingFromDepthLimit)
        {
            _diagnostics.ReportError(code, span, message, notes);
        }
    }
}

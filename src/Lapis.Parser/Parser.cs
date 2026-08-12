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
    ///
    /// <c>if</c> não é mais sempre bloco (plano 26 §26.9): <c>Then</c>/<c>Else</c>
    /// podem ser uma expressão qualquer. A regra passa a olhar o <b>último ramo
    /// escrito</b> — <c>Else</c> quando existe, senão <c>Then</c> — e não é regra
    /// nova, é a mesma de sempre ("o que fecha por último é bloco?") exercitada
    /// pela primeira vez, porque antes o `if` sempre terminava em bloco por
    /// construção.
    /// </summary>
    private static bool IsBlockLike(Expression expression) => expression switch
    {
        BlockExpression or MatchExpression or LoopExpression => true,

        // Um `if` nunca precisa de `;` externo, com chaves ou sem: o corpo com
        // chaves se autotermina como sempre, e o corpo sem chaves consome o
        // próprio `;` dentro de `ParseIfBody` — é o que deixa um `else` seguinte
        // visível para `ParseIf` sem depender de quem chamou.
        IfExpression => true,

        // Uma invocação que terminou em bloco dispensa `;` pelo mesmo motivo que
        // `if p { }` dispensa: `@unless c { }` é a forma que a macro define, e
        // exigir `;` ali contrariaria a sintaxe que ela escolheu (spec de macros §4).
        MacroInvocation { Arguments: [.., { Kind: TokenKind.CloseBrace }] } => true,

        _ => false,
    };

    /// <summary>
    /// <c>IDENT ("." IDENT)* "=" expressão ";"</c>. Não há ambiguidade a resolver:
    /// <c>==</c> é outro token, e um caminho de identificadores seguido de <c>=</c>
    /// em posição de statement não pode ser mais nada.
    ///
    /// O caminho existe por <c>u.name = "b";</c> (plano 21 §21.3b). Sem ele o
    /// parser respondia <c>LAP0102</c> — uma reclamação de pontuação para um
    /// problema semântico, e a primeira coisa que alguém escreve.
    /// </summary>
    private bool AtAssignment()
    {
        if (Current.Kind != TokenKind.Identifier)
        {
            return false;
        }

        // Anda `IDENT ('.' IDENT)*` sem consumir e pergunta o que vem depois.
        var ahead = 1;

        while (_tokens.Peek(ahead).Kind == TokenKind.Dot
            && _tokens.Peek(ahead + 1).Kind == TokenKind.Identifier)
        {
            ahead += 2;
        }

        return _tokens.Peek(ahead).Kind == TokenKind.Equals;
    }

    private Statement ParseAssignStatement()
    {
        var start = Current.Span.Start;
        var nameToken = _tokens.Advance();

        var path = ImmutableArray.CreateBuilder<string>();
        var pathSpans = ImmutableArray.CreateBuilder<SourceSpan>();

        while (Current.Kind == TokenKind.Dot)
        {
            _tokens.Advance(); // '.'
            var segment = _tokens.Advance();
            path.Add(segment.Text);
            pathSpans.Add(segment.Span);
        }

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
            Path = path.ToImmutable(),
            PathSpans = pathSpans.ToImmutable(),
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

        // `def T.m = ...` — membro de tipo (plano 21). A desambiguação é de um
        // token: depois do primeiro identificador, `.` significa membro; `=` ou
        // `:` significa `def` comum.
        //
        // `def Result<Int, ?>.m` acrescenta o padrão do dono (plano 23). O `<`
        // aqui não é ambíguo com o operador: depois do nome de um `def` só cabem
        // `:`, `=` ou `.`, e nenhum deles começa uma comparação.
        TypeSyntax? owner = null;
        SourceSpan? ownerSpan = null;
        var ownerStart = nameToken.Span.Start;

        var ownerArguments = Current.Kind == TokenKind.Less
            ? ParseGenericArgumentList(typePosition: true)
            : ImmutableArray<GenericArgumentSyntax>.Empty;

        if (!ownerArguments.IsEmpty && Current.Kind != TokenKind.Dot)
        {
            Report(
                DiagnosticCodes.UnexpectedToken,
                Current.Span,
                "esperado '.' após os argumentos genéricos do dono do membro");
        }

        if (Current.Kind == TokenKind.Dot)
        {
            var ownerNameSpan = SpanFrom(ownerStart);

            _tokens.Advance(); // '.'

            owner = new NamedTypeSyntax(name, ownerArguments) { Span = ownerNameSpan };
            ownerSpan = ownerNameSpan;

            if (Current.Kind == TokenKind.Identifier)
            {
                nameToken = Current;
                name = Current.Text;
                _tokens.Advance();
            }
            else
            {
                Report(
                    DiagnosticCodes.ExpectedIdentifier,
                    Current.Span,
                    "esperado o nome do membro após '.'");
            }

            // Um membro é definitivo. Um membro mutável exigiria decidir onde vive
            // o slot — pergunta que a Q25 fechou para bindings e que não vale
            // reabrir aqui.
            if (isMutable)
            {
                Report(
                    DiagnosticCodes.MemberCannotBeVar,
                    SpanFrom(start),
                    "um membro de tipo não pode ser 'var'",
                    new DiagnosticNote("troque por 'def'; um membro é definitivo"));
            }
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
            Owner = owner,
            OwnerSpan = ownerSpan,
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
            return Current.Kind switch
            {
                TokenKind.ReturnKeyword => ParseReturn(),
                TokenKind.ThrowKeyword => ParseThrow(),
                TokenKind.BreakKeyword => ParseBreak(),
                TokenKind.ContinueKeyword => ParseContinue(),
                _ => ParseBinary(0),
            };
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

    /// <summary>
    /// <c>throw e</c>. Ao contrário de <c>return</c>, o valor é obrigatório: a
    /// mensagem <b>é</b> o diagnóstico (spec de macros §8.2), e um <c>throw</c> sem
    /// ela não teria o que dizer.
    /// </summary>
    private Expression ParseThrow()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'throw'

        var value = ParseExpression();

        return new ThrowExpression(value) { Span = SpanFrom(start) };
    }

    /// <summary>
    /// A precedência de <c>is</c> (plano 25 §25.7): mais forte que <c>&amp;&amp;</c>
    /// (2), mais fraca que <c>==</c>/<c>!=</c> (4). Não é um valor de
    /// <see cref="BinaryOperator"/> — <c>is</c> produz <see cref="IsExpression"/>,
    /// não <see cref="BinaryExpression"/> — então mora aqui, e não em
    /// <c>Operators.cs</c>.
    /// </summary>
    private const int IsPrecedence = 3;

    private Expression ParseBinary(int minPrecedence)
    {
        var left = ParseUnary();
        var comparisonSeen = false;
        var isSeen = false;

        while (true)
        {
            if (Current.Kind == TokenKind.IsKeyword && IsPrecedence >= minPrecedence)
            {
                // `a is P is Q` não tem leitura: mesmo motivo de `a < b < c` — o
                // segundo `is` teria um `Bool` à esquerda. Mesmo diagnóstico
                // (§25.7), e a mesma recuperação: reporta e ainda assim parseia.
                if (isSeen)
                {
                    Report(DiagnosticCodes.ChainedComparison, Current.Span, "'is' não encadeia; use parênteses");
                }

                isSeen = true;
                left = ParseIsExpression(left);
                continue;
            }

            if (!TryGetBinaryOperator(Current.Kind, out var op))
            {
                break;
            }

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

    /// <summary>
    /// <c>scrutinee is is_pattern</c> (plano 25 §25.5). O <c>is</c> já foi visto
    /// por <see cref="ParseBinary"/>, que só decide <b>se</b> consome; quem lê o
    /// resto é este método.
    /// </summary>
    private Expression ParseIsExpression(Expression scrutinee)
    {
        var start = scrutinee.Span.Start;
        _tokens.Advance(); // 'is'

        var (ownerName, ownerSpan, ownerArguments, variantName, variantSpan) = ParseIsPattern();
        var (bindingName, bindingSpan) = ParseIsBinding();

        return new IsExpression(scrutinee, ownerName, ownerArguments, variantName, bindingName)
        {
            Span = SpanFrom(start),
            VariantSpan = variantSpan,
            OwnerSpan = ownerSpan,
            BindingSpan = bindingSpan,
        };
    }

    /// <summary>
    /// <c>is_pattern = ( IDENT generic_args? "." )? IDENT</c> (plano 25 §25.5) —
    /// só a parte do dono e da variante; a ligação entre parênteses é
    /// <see cref="ParseIsBinding"/>.
    ///
    /// Não é <see cref="ParsePattern"/>: aquela produção já existe para
    /// <c>match</c> e é mais rica (aninha, liga vários nomes). A diferença é
    /// deliberada — <c>is</c> existe para o caso de uma ligação só, e o caso
    /// completo já tem <c>match</c> (§25.5, LAP0734).
    /// </summary>
    private (string? OwnerName, SourceSpan? OwnerSpan, ImmutableArray<GenericArgumentSyntax> OwnerArguments,
        string VariantName, SourceSpan VariantSpan) ParseIsPattern()
    {
        var firstToken = Current;

        if (Current.Kind != TokenKind.Identifier)
        {
            Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome de uma variante após 'is'");
            return (null, null, ImmutableArray<GenericArgumentSyntax>.Empty, "?", Current.Span);
        }

        _tokens.Advance();

        var ownerArguments = Current.Kind == TokenKind.Less
            ? ParseGenericArgumentList(typePosition: true)
            : ImmutableArray<GenericArgumentSyntax>.Empty;

        // Sem '.' (e sem genéricos, que só fazem sentido num dono escrito): o
        // identificador já lido é a própria variante, sem dono — `e is Some`.
        if (Current.Kind != TokenKind.Dot && ownerArguments.IsEmpty)
        {
            return (null, null, ownerArguments, firstToken.Text, firstToken.Span);
        }

        if (!_tokens.Match(TokenKind.Dot))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado '.' após o dono da variante");
        }

        var variantToken = Current;

        if (Current.Kind != TokenKind.Identifier)
        {
            Report(DiagnosticCodes.ExpectedIdentifier, Current.Span, "esperado o nome da variante após '.'");
            return (firstToken.Text, firstToken.Span, ownerArguments, "?", Current.Span);
        }

        _tokens.Advance();

        return (firstToken.Text, firstToken.Span, ownerArguments, variantToken.Text, variantToken.Span);
    }

    /// <summary><c>("(" IDENT ")")?</c> — a parte que liga a carga (plano 25 §25.5).</summary>
    private (string? Name, SourceSpan? Span) ParseIsBinding()
    {
        if (!_tokens.Match(TokenKind.OpenParen))
        {
            return (null, null);
        }

        string? name = null;
        SourceSpan? span = null;
        var token = Current;

        if (token.Kind == TokenKind.Identifier)
        {
            _tokens.Advance();
            name = token.Text;
            span = token.Span;
        }
        else
        {
            Report(DiagnosticCodes.ExpectedIdentifier, token.Span, "esperado um identificador para ligar a carga");
        }

        if (!_tokens.Match(TokenKind.CloseParen))
        {
            Report(DiagnosticCodes.UnexpectedToken, Current.Span, "esperado ')' após o nome da ligação");
        }

        return (name, span);
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

            case TokenKind.LoopKeyword:
                return ParseLoop();

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

    private Expression ParseSpanLiteral()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // '.'
        _tokens.Advance(); // '['

        if (TryParseSpanRepeat(start, out var repeat))
        {
            return repeat;
        }

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
            Report(DiagnosticCodes.ExpectedCloseBracket, Current.Span, "esperado ']' para fechar o span");
        }

        return new SpanExpression(elements.ToImmutable()) { Span = SpanFrom(start) };
    }

    /// <summary>
    /// <c>.[T; inicial; n]</c> — a forma por repetição, decidida por um token.
    ///
    /// As duas leituras de <c>.[</c> divergem no separador: a lista usa
    /// <c>,</c> e a repetição usa <c>;</c>, os mesmos <c>;</c> de <c>[Int;8]</c>.
    /// A decisão precisa de especulação porque o primeiro componente é um
    /// <b>tipo</b> aqui e uma <b>expressão</b> lá, e <c>a</c> parseia como os dois.
    ///
    /// Só depois de ver o <c>;</c> a forma está escolhida — daí em diante os
    /// erros são reportados, não engolidos, senão um erro dentro do
    /// inicializador faria a expressão inteira ser relida como lista e produziria
    /// um diagnóstico sobre a coisa errada.
    /// </summary>
    private bool TryParseSpanRepeat(int start, out Expression repeat)
    {
        repeat = null!;

        var mark = _tokens.Mark();

        _speculating++;
        var element = ParseType();
        var isRepeat = !_tokens.AtEnd && Current.Kind == TokenKind.Semicolon;
        _speculating--;

        if (!isRepeat)
        {
            _tokens.Reset(mark);
            return false;
        }

        _tokens.Advance(); // ';'

        var initializer = ParseExpression();

        if (!_tokens.Match(TokenKind.Semicolon))
        {
            Report(
                DiagnosticCodes.ExpectedSemicolon,
                Current.Span,
                "esperado ';' antes da quantidade do span",
                new DiagnosticNote("a forma é `.[T; inicial; n]`"));
        }

        var size = ParseExpression();

        if (!_tokens.Match(TokenKind.CloseBracket))
        {
            Report(DiagnosticCodes.ExpectedCloseBracket, Current.Span, "esperado ']' para fechar o span");
        }

        repeat = new SpanRepeatExpression(element, initializer, size) { Span = SpanFrom(start) };
        return true;
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
    /// <summary>
    /// O que vem depois do ponto decide: <c>.User { }</c> constrói um
    /// <c>type</c>, <c>.[1, 2, 3]</c> constrói um span (plano 24).
    ///
    /// O ponto passa a significar, uniformemente, "isto é um valor sendo
    /// construído" — e é o que libera <c>[</c> para ser sempre tipo.
    /// </summary>
    private Expression ParseConstruct()
    {
        if (_tokens.Peek(1).Kind == TokenKind.OpenBracket)
        {
            return ParseSpanLiteral();
        }

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

            // `?` — curinga do dono de um membro (plano 23). O parser aceita em
            // qualquer lista; quem restringe à posição de dono é o checker, que é
            // quem sabe onde a lista está.
            case TokenKind.Question:
                _tokens.Advance();
                return new WildcardArgumentSyntax { Span = token.Span };

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

            TypeSyntax? type;

            if (_tokens.Match(TokenKind.Colon))
            {
                type = ParseType();
            }
            else if (string.Equals(name, MemberNames.Self, StringComparison.Ordinal))
            {
                // `self` sem anotação é a única exceção à spec §26, e é o gatilho
                // do membro de instância (plano 22 §22.1). Se a posição não for
                // legítima, quem reclama é o checker (`LAP0712`) — o parser não
                // sabe se esta função é o valor de um `def T.m`.
                type = null;
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
        var then = ParseIfBody();

        Expression? elseBranch = null;

        if (_tokens.Match(TokenKind.ElseKeyword))
        {
            // `else if` encadeia sem chaves — a única exceção à regra de §26.9.
            // Este `if` não é o `Then` de ninguém: é a continuação explícita do
            // `else`, então não compete com outro `else` mais adiante por
            // pertencimento (dangling-else). Cada `if` do encadeamento continua
            // sujeito à mesma regra no próprio `Then`.
            elseBranch = Current.Kind == TokenKind.IfKeyword ? ParseIf() : ParseIfBody();
        }

        return new IfExpression(condition, then, elseBranch) { Span = SpanFrom(start) };
    }

    /// <summary>
    /// O corpo de um <c>if</c>/<c>else</c> fora da posição de encadeamento
    /// (<c>if_body</c>, plano 26 §26.9): um bloco, ou qualquer expressão que
    /// <b>não</b> seja outro <c>if</c> sem chaves.
    ///
    /// A exclusão é o que evita o dangling-else sem regra de precedência —
    /// <c>if a if b c; else d;</c> é <c>LAP0527</c>; escrito com chaves
    /// (<c>if a { if b c; } else d;</c>) o aninhamento continua livre, porque aí
    /// não há ambiguidade nenhuma: o `}` já fechou o `if` de dentro.
    /// </summary>
    private Expression ParseIfBody()
    {
        if (Current.Kind == TokenKind.OpenBrace)
        {
            return ParseBlock();
        }

        if (Current.Kind == TokenKind.IfKeyword)
        {
            Report(
                DiagnosticCodes.BareIfCannotHaveBareIfBody,
                Current.Span,
                "'if'/'else' sem chaves não pode ter 'if' como corpo direto; use chaves");
        }

        var start = Current.Span.Start;
        var body = ParseExpression();

        // Um corpo sem chaves precisa consumir o próprio `;` aqui — senão um
        // `else` que vier a seguir fica escondido de `ParseIf` atrás dele (quem
        // chama só olha o token atual depois que este método retorna). Um corpo
        // que já se autotermina (bloco, outro `if` que já resolveu o seu) não
        // passa por aqui ou já não tem `;` para consumir.
        if (!IsBlockLike(body) && !_tokens.Match(TokenKind.Semicolon))
        {
            Report(DiagnosticCodes.ExpectedSemicolon, SpanFrom(start), "esperado ';' ao final da declaração");
        }

        return body;
    }

    /// <summary>
    /// <c>loop (: IDENT)? bloco</c> (plano 26, M16). O rótulo é opcional — só
    /// importa quando um <c>break</c>/<c>continue</c> de um laço aninhado precisa
    /// alcançar este.
    /// </summary>
    private Expression ParseLoop()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'loop'

        var (label, labelSpan) = TryParseLoopLabel();
        var body = ParseBlock();

        return new LoopExpression(label, body) { Span = SpanFrom(start), LabelSpan = labelSpan };
    }

    /// <summary>
    /// <c>break (: IDENT)? (","? expressão)?</c> (plano 26, M16). A vírgula entre
    /// rótulo e valor evita <c>break :x valor;</c> parsear como <c>break :x</c>
    /// seguido de uma expressão solta — sem ela, onde o rótulo termina e o valor
    /// começa seria ambíguo de olhar.
    /// </summary>
    private Expression ParseBreak()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'break'

        var (label, labelSpan) = TryParseLoopLabel();

        if (label is not null)
        {
            var value = _tokens.Match(TokenKind.Comma) ? ParseExpression() : null;
            return new BreakExpression(label, value) { Span = SpanFrom(start), LabelSpan = labelSpan };
        }

        // Sem rótulo, o valor é opcional e não precisa de vírgula: `break;` e
        // `break 5;` são as duas formas. Mesma lista de terminadores de
        // `ParseReturn`.
        var hasValue = Current.Kind is not (TokenKind.Semicolon or TokenKind.CloseBrace
            or TokenKind.EndOfFile or TokenKind.Comma or TokenKind.CloseParen);

        return new BreakExpression(null, hasValue ? ParseExpression() : null)
        {
            Span = SpanFrom(start),
            LabelSpan = labelSpan,
        };
    }

    /// <summary><c>continue (: IDENT)?</c> (plano 26, M16). Sem valor: reinicia a iteração, não sai do laço.</summary>
    private Expression ParseContinue()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // 'continue'

        var (label, labelSpan) = TryParseLoopLabel();

        return new ContinueExpression(label) { Span = SpanFrom(start), LabelSpan = labelSpan };
    }

    /// <summary>
    /// <c>(":" IDENT)?</c> — o rótulo de <c>loop</c>/<c>break</c>/<c>continue</c>.
    /// Vive no mesmo espaço de nomes separado que os rótulos de <c>label</c>
    /// viviam (plano 16 §16.3, retirado): um <c>loop :x</c> e um <c>def x</c>
    /// convivem sem colidir.
    /// </summary>
    private (string? Label, SourceSpan? Span) TryParseLoopLabel()
    {
        if (Current.Kind != TokenKind.Colon)
        {
            return (null, null);
        }

        _tokens.Advance(); // ':'

        var token = Current;

        if (token.Kind != TokenKind.Identifier)
        {
            Report(DiagnosticCodes.ExpectedIdentifier, token.Span, "esperado um rótulo após ':'");
            return ("?", token.Span);
        }

        _tokens.Advance();
        return (token.Text, token.Span);
    }

    // --------------------------------------------------------------- tipos

    internal TypeSyntax ParseType() => ParseTypePrimary();

    /// <summary>
    /// <c>[Int;3]</c>, <c>[Int;?]</c>, <c>[Int;N]</c> — o tamanho vive no tipo
    /// (plano 24).
    ///
    /// A forma pós-fixa <c>Int[]</c> saiu: manter as duas exigiria escolher qual
    /// delas carrega o tamanho, e dois jeitos de escrever o mesmo tipo é o que
    /// este projeto evita. Com ela fora, <c>[</c> é sempre tipo e <c>.[</c> é
    /// sempre valor.
    /// </summary>
    private TypeSyntax ParseSpanType()
    {
        var start = Current.Span.Start;
        _tokens.Advance(); // '['

        var element = ParseType();

        if (!_tokens.Match(TokenKind.Semicolon))
        {
            Report(DiagnosticCodes.ExpectedSemicolon, Current.Span, "esperado ';' no tipo de span");

            return new SpanTypeSyntax(element, new UnknownSizeSyntax { Span = Current.Span })
            {
                Span = SpanFrom(start),
            };
        }

        var size = ParseSpanSize();

        if (!_tokens.Match(TokenKind.CloseBracket))
        {
            Report(DiagnosticCodes.ExpectedCloseBracket, Current.Span, "esperado ']' no tipo de span");
        }

        return new SpanTypeSyntax(element, size) { Span = SpanFrom(start) };
    }

    private SpanSizeSyntax ParseSpanSize()
    {
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.Question:
                _tokens.Advance();
                return new UnknownSizeSyntax { Span = token.Span };

            case TokenKind.IntegerLiteral:
                _tokens.Advance();

                if (token.IntegerValue < 0)
                {
                    Report(
                        DiagnosticCodes.InvalidSpanSize,
                        token.Span,
                        "o tamanho de um span deve ser um Int não negativo");
                }

                return new FixedSizeSyntax(token.IntegerValue) { Span = token.Span };

            // Um parâmetro const genérico como tamanho: `fn<N: Int>(s: [Int;N])`.
            case TokenKind.Identifier:
                _tokens.Advance();
                return new NamedSizeSyntax(token.Text) { Span = token.Span };

            default:
                Report(
                    DiagnosticCodes.InvalidSpanSize,
                    token.Span,
                    $"esperado o tamanho do span, encontrado {token.Kind.Describe()}");

                // Consome o token ofensivo para que o `]` ainda case: sem isto um
                // tamanho inválido vira uma cascata de três diagnósticos.
                _tokens.Advance();

                return new UnknownSizeSyntax { Span = token.Span };
        }
    }

    private TypeSyntax ParseTypePrimary()
    {
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.OpenBracket:
                return ParseSpanType();

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

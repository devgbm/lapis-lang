using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lapis.Diagnostics;

namespace Lapis.Lexer;

/// <summary>
/// <c>SourceText → tokens</c>.
///
/// Scanner de um caractere de lookahead, sem regex, para spans exatos e
/// previsibilidade. Nunca lança: todo defeito vira diagnóstico, token
/// <see cref="TokenKind.Bad"/> e progresso garantido (plano 03).
/// </summary>
public sealed class Lexer
{
    private static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.Ordinal)
    {
        ["def"] = TokenKind.DefKeyword,
        ["var"] = TokenKind.VarKeyword,
        ["fn"] = TokenKind.FnKeyword,
        ["type"] = TokenKind.TypeKeyword,
        ["enum"] = TokenKind.EnumKeyword,
        ["return"] = TokenKind.ReturnKeyword,
        ["true"] = TokenKind.TrueKeyword,
        ["false"] = TokenKind.FalseKeyword,
        ["if"] = TokenKind.IfKeyword,
        ["else"] = TokenKind.ElseKeyword,
        ["match"] = TokenKind.MatchKeyword,
        ["goto"] = TokenKind.GotoKeyword,
        ["macro"] = TokenKind.MacroKeyword,
        ["expand"] = TokenKind.ExpandKeyword,
        ["constraint"] = TokenKind.ConstraintKeyword,
        ["throw"] = TokenKind.ThrowKeyword,
    };

    private readonly SourceText _source;
    private readonly DiagnosticBag _diagnostics;
    private int _position;

    private Lexer(SourceText source, DiagnosticBag diagnostics)
    {
        _source = source;
        _diagnostics = diagnostics;
    }

    public static ImmutableArray<Token> Tokenize(SourceText source, DiagnosticBag diagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var lexer = new Lexer(source, diagnostics);
        var builder = ImmutableArray.CreateBuilder<Token>();

        while (true)
        {
            var token = lexer.NextToken();
            builder.Add(token);

            if (token.Kind == TokenKind.EndOfFile)
            {
                return builder.ToImmutable();
            }
        }
    }

    private char Current => Peek(0);

    private char Lookahead => Peek(1);

    private bool AtEnd => _position >= _source.Length;

    private char Peek(int offset)
    {
        var index = _position + offset;
        return index < _source.Length ? _source[index] : '\0';
    }

    private Token NextToken()
    {
        SkipTrivia();

        var start = _position;

        if (AtEnd)
        {
            return new Token(TokenKind.EndOfFile, new SourceSpan(_source.Length, 0), string.Empty);
        }

        var c = Current;

        if (char.IsAsciiDigit(c))
        {
            return ReadNumber();
        }

        if (char.IsAsciiLetter(c) || c == '_')
        {
            return ReadIdentifierOrKeyword();
        }

        if (c == '"')
        {
            return ReadString();
        }

        return ReadOperator(start);
    }

    private void SkipTrivia()
    {
        while (!AtEnd)
        {
            if (char.IsWhiteSpace(Current))
            {
                _position++;
                continue;
            }

            if (Current == '/' && Lookahead == '/')
            {
                while (!AtEnd && Current != '\n')
                {
                    _position++;
                }

                continue;
            }

            if (Current == '/' && Lookahead == '*')
            {
                SkipBlockComment();
                continue;
            }

            return;
        }
    }

    /// <summary>Comentários de bloco não aninham: fecham no primeiro <c>*/</c>.</summary>
    private void SkipBlockComment()
    {
        var start = _position;
        _position += 2;

        while (true)
        {
            if (AtEnd)
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.UnterminatedBlockComment,
                    SourceSpan.FromBounds(start, _position),
                    "comentário de bloco não terminado");
                return;
            }

            if (Current == '*' && Lookahead == '/')
            {
                _position += 2;
                return;
            }

            _position++;
        }
    }

    /// <summary>
    /// <c>[A-Za-z_][A-Za-z0-9_]*</c>, mais o sufixo de higiene <c>@dígitos</c>.
    ///
    /// O sufixo existe porque a expansão de macro renomeia o que introduz
    /// (<c>temp</c> → <c>temp@1</c>, spec de macros §9) e o resultado precisa
    /// continuar sendo <b>reparseável</b>: o printer da Core imprime programas que
    /// voltam a ser lidos, e é disso que o <c>lapis pe</c> depende.
    ///
    /// <c>@</c> só continua um identificador quando já se leu ao menos um
    /// caractere e o próximo é dígito — <c>@log</c> em posição inicial continua
    /// sendo a invocação de macro, sem ambiguidade.
    /// </summary>
    private Token ReadIdentifierOrKeyword()
    {
        var start = _position;

        while (!AtEnd && (char.IsAsciiLetterOrDigit(Current) || Current == '_'))
        {
            _position++;
        }

        if (!AtEnd && Current == '@' && char.IsAsciiDigit(Lookahead) && _position > start)
        {
            _position++;

            while (!AtEnd && char.IsAsciiDigit(Current))
            {
                _position++;
            }
        }

        var span = SourceSpan.FromBounds(start, _position);
        var text = _source.GetText(span);

        if (text == "_")
        {
            return new Token(TokenKind.Underscore, span, text);
        }

        return Keywords.TryGetValue(text, out var keyword)
            ? new Token(keyword, span, text)
            : new Token(TokenKind.Identifier, span, text);
    }

    private Token ReadNumber()
    {
        var start = _position;

        while (!AtEnd && char.IsAsciiDigit(Current))
        {
            _position++;
        }

        // O ponto só forma um float se houver dígito depois: `1.foo` é `1` `.` `foo`.
        var isFloat = !AtEnd && Current == '.' && char.IsAsciiDigit(Lookahead);

        if (isFloat)
        {
            _position++;

            while (!AtEnd && char.IsAsciiDigit(Current))
            {
                _position++;
            }
        }

        var span = SourceSpan.FromBounds(start, _position);
        var text = _source.GetText(span);

        if (isFloat)
        {
            // `TryParse` devolve `true` com ±Infinity quando o literal estoura o
            // Float, e um literal que vira Infinity em silêncio é surpresa dupla:
            // o valor não é o que está escrito, e `Infinity` não é escrevível na
            // linguagem — imprimi-lo produz texto que não reparseia.
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || double.IsInfinity(value))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.MalformedFloatLiteral,
                    span,
                    $"literal float fora do intervalo de Float: '{text}'");
                value = 0;
            }

            return new Token(TokenKind.FloatLiteral, span, text, value);
        }

        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.IntegerLiteralOutOfRange,
                span,
                $"literal inteiro fora do intervalo de Int: '{text}'");
            integer = 0;
        }

        return new Token(TokenKind.IntegerLiteral, span, text, integer);
    }

    private Token ReadString()
    {
        var start = _position;
        _position++; // aspas de abertura

        var value = new StringBuilder();

        while (true)
        {
            if (AtEnd || Current == '\n' || Current == '\r')
            {
                var span = SourceSpan.FromBounds(start, _position);
                _diagnostics.ReportError(
                    DiagnosticCodes.UnterminatedString, span, "literal de string não terminado");
                return new Token(TokenKind.StringLiteral, span, _source.GetText(span), value.ToString());
            }

            if (Current == '"')
            {
                _position++;
                break;
            }

            if (Current == '\\')
            {
                ReadEscape(value);
                continue;
            }

            value.Append(Current);
            _position++;
        }

        var fullSpan = SourceSpan.FromBounds(start, _position);
        return new Token(TokenKind.StringLiteral, fullSpan, _source.GetText(fullSpan), value.ToString());
    }

    private void ReadEscape(StringBuilder value)
    {
        var escapeStart = _position;
        _position++; // a barra

        if (AtEnd)
        {
            return;
        }

        var escape = Current;
        _position++;

        switch (escape)
        {
            case '"': value.Append('"'); break;
            case '\\': value.Append('\\'); break;
            case 'n': value.Append('\n'); break;
            case 't': value.Append('\t'); break;
            case 'r': value.Append('\r'); break;
            case '0': value.Append('\0'); break;
            default:
                _diagnostics.ReportError(
                    DiagnosticCodes.UnknownEscapeSequence,
                    SourceSpan.FromBounds(escapeStart, _position),
                    $"sequência de escape desconhecida: '\\{escape}'");
                value.Append(escape);
                break;
        }
    }

    private Token ReadOperator(int start)
    {
        var (kind, length) = Current switch
        {
            '(' => (TokenKind.OpenParen, 1),
            ')' => (TokenKind.CloseParen, 1),
            '{' => (TokenKind.OpenBrace, 1),
            '}' => (TokenKind.CloseBrace, 1),
            '[' => (TokenKind.OpenBracket, 1),
            ']' => (TokenKind.CloseBracket, 1),
            ':' => (TokenKind.Colon, 1),
            ',' => (TokenKind.Comma, 1),
            ';' => (TokenKind.Semicolon, 1),
            '.' => (TokenKind.Dot, 1),
            '@' => (TokenKind.At, 1),

            // Só aparece como tamanho de span: `[Int;?]` (plano 24).
            '?' => (TokenKind.Question, 1),
            '+' => (TokenKind.Plus, 1),
            '-' => (TokenKind.Minus, 1),
            '*' => (TokenKind.Star, 1),
            '/' => (TokenKind.Slash, 1),
            '=' when Lookahead == '=' => (TokenKind.EqualsEquals, 2),
            '=' when Lookahead == '>' => (TokenKind.FatArrow, 2),
            '=' => (TokenKind.Equals, 1),
            '!' when Lookahead == '=' => (TokenKind.BangEquals, 2),
            '!' => (TokenKind.Bang, 1),
            '<' when Lookahead == '=' => (TokenKind.LessEquals, 2),
            '<' => (TokenKind.Less, 1),
            '>' when Lookahead == '=' => (TokenKind.GreaterEquals, 2),
            '>' => (TokenKind.Greater, 1),
            '&' when Lookahead == '&' => (TokenKind.AmpersandAmpersand, 2),
            '|' when Lookahead == '|' => (TokenKind.PipePipe, 2),
            _ => (TokenKind.Bad, 1),
        };

        _position += length;
        var span = SourceSpan.FromBounds(start, _position);
        var text = _source.GetText(span);

        if (kind == TokenKind.Bad)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnexpectedCharacter, span, $"caractere inesperado: '{text}'");
        }

        return new Token(kind, span, text);
    }
}

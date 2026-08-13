using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Surface;
using Lapis.Lexer;
using Lapis.Parser;

namespace Lapis.Macros;

/// <summary>
/// O que uma captura ligou.
/// </summary>
/// <param name="Tokens">
/// Os tokens que a captura consumiu. A árvore serve para substituir em posição
/// normal; os tokens servem quando a captura aparece dentro de <b>outra</b>
/// invocação de macro no <c>expand</c>, onde ainda não há árvore para substituir.
/// </param>
public abstract record MacroBinding(ImmutableArray<Token> Tokens);

public sealed record SingleBinding(SurfaceNode Node, ImmutableArray<Token> Tokens) : MacroBinding(Tokens);

/// <summary>Uma captura sob repetição liga uma lista; listas de um mesmo grupo são paralelas.</summary>
public sealed record RepeatBinding(ImmutableArray<SurfaceNode> Nodes, ImmutableArray<Token> Tokens)
    : MacroBinding(Tokens);

public sealed record MatchResult(ImmutableDictionary<string, MacroBinding> Bindings);

/// <summary>
/// Casa o padrão de uma regra contra os tokens de uma invocação.
///
/// Nada é reportado durante a tentativa: falhar é o resultado normal de uma regra
/// que não se aplica, e quem decide se isso é erro é a resolução de regras
/// (<c>LAP0501</c> quando nenhuma casa, <c>LAP0502</c> quando mais de uma casa).
/// </summary>
public static class MacroMatcher
{
    public static MatchResult? Match(MacroPattern pattern, ImmutableArray<Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var reader = new SyntaxFragmentReader(tokens);
        var bindings = ImmutableDictionary.CreateBuilder<string, MacroBinding>(StringComparer.Ordinal);

        if (!MatchItem(pattern, reader, tokens, bindings))
        {
            return null;
        }

        // Sobra de tokens é falha: a macro tem de explicar a invocação inteira.
        return reader.AtEnd ? new MatchResult(bindings.ToImmutable()) : null;
    }

    private static bool MatchItem(
        MacroPattern pattern,
        SyntaxFragmentReader reader,
        ImmutableArray<Token> tokens,
        ImmutableDictionary<string, MacroBinding>.Builder bindings)
    {
        switch (pattern)
        {
            case PatternSequence sequence:
                foreach (var item in sequence.Items)
                {
                    if (!MatchItem(item, reader, tokens, bindings))
                    {
                        return false;
                    }
                }

                return true;

            case PatternLiteral literal:
                if (reader.AtEnd || !string.Equals(reader.Current.Text, literal.Text, StringComparison.Ordinal))
                {
                    return false;
                }

                reader.Advance();
                return true;

            case PatternCapture capture:
                return MatchCapture(capture, reader, tokens, bindings);

            case PatternRepeat repeat:
                return MatchRepeat(repeat, reader, tokens, bindings);

            default:
                return false;
        }
    }

    private static bool MatchCapture(
        PatternCapture capture,
        SyntaxFragmentReader reader,
        ImmutableArray<Token> tokens,
        ImmutableDictionary<string, MacroBinding>.Builder bindings)
    {
        var start = reader.Position;

        if (!TryRead(capture.Category, reader, out var node))
        {
            return false;
        }

        // Uma leitura que reportou diagnóstico não casou: o parser recuperou, mas
        // o texto não era daquela categoria.
        if (reader.Failed)
        {
            return false;
        }

        var consumed = Slice(tokens, start, reader.Position);
        bindings[capture.Name] = new SingleBinding(node, consumed);
        return true;
    }

    private static bool MatchRepeat(
        PatternRepeat repeat,
        SyntaxFragmentReader reader,
        ImmutableArray<Token> tokens,
        ImmutableDictionary<string, MacroBinding>.Builder bindings)
    {
        var collected = new Dictionary<string, List<SurfaceNode>>(StringComparer.Ordinal);
        var consumed = new Dictionary<string, List<Token>>(StringComparer.Ordinal);

        foreach (var name in CaptureNames(repeat.Item))
        {
            collected[name] = [];
            consumed[name] = [];
        }

        while (!reader.AtEnd)
        {
            var round = ImmutableDictionary.CreateBuilder<string, MacroBinding>(StringComparer.Ordinal);

            if (!MatchItem(repeat.Item, reader, tokens, round))
            {
                return false;
            }

            foreach (var (name, binding) in round)
            {
                collected[name].Add(((SingleBinding)binding).Node);
                consumed[name].AddRange(binding.Tokens);
            }

            if (reader.AtEnd || !string.Equals(reader.Current.Text, repeat.Separator, StringComparison.Ordinal))
            {
                break;
            }

            reader.Advance();
        }

        foreach (var (name, nodes) in collected)
        {
            bindings[name] = new RepeatBinding([.. nodes], [.. consumed[name]]);
        }

        return true;
    }

    private static bool TryRead(SyntaxCategory category, SyntaxFragmentReader reader, out SurfaceNode node)
    {
        node = null!;

        if (reader.AtEnd)
        {
            return false;
        }

        switch (category)
        {
            case SyntaxCategory.Expression:
                node = reader.ReadExpression();
                return true;

            case SyntaxCategory.Block:
                if (reader.Current.Kind != TokenKind.OpenBrace)
                {
                    return false;
                }

                node = reader.ReadBlock();
                return true;

            case SyntaxCategory.Statement:
                var statement = reader.ReadStatement();

                if (statement is null)
                {
                    return false;
                }

                node = statement;
                return true;

            case SyntaxCategory.Type:
                node = reader.ReadType();
                return true;

            case SyntaxCategory.Identifier:
                if (reader.Current.Kind != TokenKind.Identifier)
                {
                    return false;
                }

                var identifier = reader.Advance();
                node = new IdentifierExpression(identifier.Text) { Span = identifier.Span };
                return true;

            default:
                return TryReadLiteral(category, reader, out node);
        }
    }

    private static bool TryReadLiteral(SyntaxCategory category, SyntaxFragmentReader reader, out SurfaceNode node)
    {
        node = null!;
        var token = reader.Current;

        var accepted = category switch
        {
            SyntaxCategory.Int => token.Kind == TokenKind.IntegerLiteral,
            SyntaxCategory.Float => token.Kind == TokenKind.FloatLiteral,
            SyntaxCategory.Str => token.Kind == TokenKind.StringLiteral,
            SyntaxCategory.Bool => token.Kind is TokenKind.TrueKeyword or TokenKind.FalseKeyword,
            // Um token de aspas simples que não seja um caractere só é uma
            // pseudo-palavra-chave (Q38), não um `Char`: o padrão simplesmente
            // não casa, e cabe a outra regra tratá-lo.
            SyntaxCategory.Char => token.Kind == TokenKind.CharLiteral
                && ConstChar.TrySingleCodepoint(token.StringValue, out _),
            SyntaxCategory.Literal => token.Kind is TokenKind.IntegerLiteral or TokenKind.FloatLiteral
                or TokenKind.StringLiteral or TokenKind.TrueKeyword or TokenKind.FalseKeyword
                || (token.Kind == TokenKind.CharLiteral
                    && ConstChar.TrySingleCodepoint(token.StringValue, out _)),
            _ => false,
        };

        if (!accepted)
        {
            return false;
        }

        reader.Advance();

        node = token.Kind switch
        {
            TokenKind.IntegerLiteral => new IntLiteral(token.IntegerValue, token.Text) { Span = token.Span },
            TokenKind.FloatLiteral => new FloatLiteral(token.FloatValue, token.Text) { Span = token.Span },
            TokenKind.StringLiteral => new StrLiteral(token.StringValue) { Span = token.Span },
            TokenKind.CharLiteral => new CharLiteral(
                ConstChar.TrySingleCodepoint(token.StringValue, out var rune) ? rune : default)
            { Span = token.Span },
            _ => new BoolLiteral(token.Kind == TokenKind.TrueKeyword) { Span = token.Span },
        };

        return true;
    }

    /// <summary>Nomes ligados por um padrão — usado para montar as listas paralelas.</summary>
    public static IEnumerable<string> CaptureNames(MacroPattern pattern) => pattern switch
    {
        PatternCapture p => [p.Name],
        PatternSequence p => p.Items.SelectMany(CaptureNames),
        PatternRepeat p => CaptureNames(p.Item),
        _ => [],
    };

    private static ImmutableArray<Token> Slice(ImmutableArray<Token> tokens, int start, int end)
    {
        var last = Math.Min(end, tokens.Length);

        return start >= last ? [] : [.. tokens[start..last]];
    }
}

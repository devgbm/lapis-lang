using System.Collections.Immutable;
using Lapis.Diagnostics;

namespace Lapis.Lexer;

/// <summary>
/// Cursor sobre a lista de tokens, com <see cref="Mark"/>/<see cref="Reset"/>
/// para o backtracking limitado do parser (plano 04 §4.4). Como os tokens já
/// estão num array, marcar é apenas guardar um índice.
/// </summary>
public sealed class TokenStream
{
    private readonly ImmutableArray<Token> _tokens;
    private int _index;

    public TokenStream(ImmutableArray<Token> tokens)
    {
        if (tokens.IsDefaultOrEmpty || tokens[^1].Kind != TokenKind.EndOfFile)
        {
            throw new InternalCompilerException("a lista de tokens precisa terminar em EndOfFile");
        }

        _tokens = tokens;
    }

    public Token Current => _tokens[_index];

    public bool AtEnd => Current.Kind == TokenKind.EndOfFile;

    public Token Peek(int offset)
    {
        var index = _index + offset;

        if (index < 0)
        {
            index = 0;
        }

        return index < _tokens.Length ? _tokens[index] : _tokens[^1];
    }

    public Token Advance()
    {
        var token = Current;

        if (_index < _tokens.Length - 1)
        {
            _index++;
        }

        return token;
    }

    public bool Match(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        Advance();
        return true;
    }

    public int Mark() => _index;

    public void Reset(int mark) => _index = mark;
}

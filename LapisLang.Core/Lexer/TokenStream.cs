namespace LapisLang.Core;

public class TokenStream
{
    protected Token[] _tokens;

    protected int _position = 0;
    public TokenStream(Token[] tokens)
    {
        _tokens = tokens;
    }


    public Token Peek(int offset)
    {
        var index = _position + offset;
        if(index >= _tokens.Length) return _tokens[_tokens.Length - 1];

        return _tokens[index];
    }
    public Token NextToken()
    {
        var current = Current;
        _position++;
        return current;
    }

    public Token Current => Peek(0);
}

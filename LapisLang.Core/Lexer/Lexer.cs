
using System;
using System.Collections.Generic;
using System.Linq;

namespace LapisLang.Core;

public class Lexer
{
    private readonly string text;
    private readonly SourceText source;
    private int position;

    public Lexer(SourceText text)
    {
        source = text;
        position = 0;
        this.text = source.Text;
    }
    public Lexer(string text) : this(new SourceText(text))
    {
    }


    public IEnumerable<Token> Lex()
    {
        while(position < text.Length) yield return NextToken();
        yield return new Token(TokenKind.EoFToken, source.CreateSpan(position, 0));
    }

    public TokenStream TokenStream()
    {
        return new TokenStream(Lex().ToArray());
    }

    public Token NextToken()
    {
        if(char.IsWhiteSpace(Current))
        {
            while(char.IsWhiteSpace(Current)) Next();
        }

        if(char.IsLetter(Current) || Current == '@')
        {
            var start = position;
            if (Current == '@') Next();
            while(char.IsLetterOrDigit(Current)) Next();
            var length = position - start;
            ReadOnlySpan<char> textualValue = text.AsSpan(start, length);
            var kind = GetDefinedKeywords(textualValue);
            return new Token(kind, source.CreateSpan(start, length));
        }

        if(char.IsDigit(Current))
        {
            var start = position;
            while(char.IsDigit(Current)) Next();

            if(Current == '.' && position + 1 < text.Length && char.IsDigit(text[position + 1]))
            {
                Next();
                while(char.IsDigit(Current)) Next();
                var length = position - start;
                var span = source.CreateSpan(start, length);
                return new Token(TokenKind.DecimalNumber, span);
            }
            else
            {
                var length = position - start;
                var span = source.CreateSpan(start, length);
                return new Token(TokenKind.IntNumber, span);
            }
        }

        if(Current == '\'')
        {
            Next();
            var start = position;
            while(Current != '\'') Next();
            var length = position - start;
            Next();
            var span = source.CreateSpan(start, length);
            return new Token(TokenKind.SingleQuoteString, span);
        }

        if(Current == '\"')
        {
            var start = position;
            Next();
            while(Current != '\"') Next();
            Next();
            var length = position - start;
            var span = source.CreateSpan(start, length);
            return new Token(TokenKind.DoubleQuoteString, span);
        }


        if(Current == '\'')
        {
            Next();
            var start = position;
            while(!(Current == '\'' && Peek(-1) != '\\')) Next();
            var length = position - start;
            Next();

            return new Token(TokenKind.SingleQuoteString, source.CreateSpan(start, length));
        }

        if(Current == '/' && Peek(1) == '*')
        {
            var start = position;
            while(!(Current == '*' && Peek(1) == '/')) Next();
            Next();
            Next();
            var length = position - start;
            return new Token(TokenKind.MultilineComentary, source.CreateSpan(start, length));
        }

        if(Current == '/' && Peek(1) == '/')
        {
            var start = position;
            while(Current != '\n') Next();
            var length = position - start;
            return new Token(TokenKind.SingleLineComentary, source.CreateSpan(start, length));
        }

        switch(Current)
        {
            case '+':
                Next();
                return new Token(TokenKind.Plus, source.CreateSpan(position - 1, 1));
            case '-':
                Next();
                return new Token(TokenKind.Minus, source.CreateSpan(position - 1, 1));
            case '(':
                Next();
                return new Token(TokenKind.OpenParenthesis, source.CreateSpan(position - 1, 1));
            case ')':
                Next();
                return new Token(TokenKind.CloseParenthesis, source.CreateSpan(position - 1, 1));
            case '=' when Peek(1) == '=':
                Next();
                Next();
                return new Token(TokenKind.DoubleEquals, source.CreateSpan(position - 2, 2));
            case '=':
                Next();
                return new Token(TokenKind.Equal, source.CreateSpan(position - 1, 1));
            case '!' when Peek(1) == '=':
                Next();
                Next();
                return new Token(TokenKind.BangEquals, source.CreateSpan(position - 2, 2));
            case '!' when Peek(1) == '&':
                Next();
                Next();
                return new Token(TokenKind.BangAnd, source.CreateSpan(position - 2, 2));
            case '!':
                Next();
                return new Token(TokenKind.Bang, source.CreateSpan(position - 1, 1));
            case '&':
                Next();
                return new Token(TokenKind.And, source.CreateSpan(position - 1, 1));
            case '<' when Peek(1) == '=':
                Next();
                Next();
                return new Token(TokenKind.LeftArrowEquals, source.CreateSpan(position - 2, 2));
            case '>' when Peek(1) == '=':
                Next();
                Next();
                return new Token(TokenKind.RightArrowEquals, source.CreateSpan(position - 2, 2));
            case '>':
                Next();
                return new Token(TokenKind.RightArrow, source.CreateSpan(position - 1, 1));
            case '<':
                Next();
                return new Token(TokenKind.LeftArrow, source.CreateSpan(position - 1, 1));
            case '{':
                Next();
                return new Token(TokenKind.OpenCurlyBrace, source.CreateSpan(position - 1, 1));
            case '[':
                Next();
                return new Token(TokenKind.OpenBracket, source.CreateSpan(position - 1, 1));
            case ']':
                Next();
                return new Token(TokenKind.CloseBracket, source.CreateSpan(position - 1, 1));
            case '}':
                Next();
                return new Token(TokenKind.CloseCurlyBrace, source.CreateSpan(position - 1, 1));
            case ';':
                Next();
                return new Token(TokenKind.SemiCollon, source.CreateSpan(position - 1, 1));
            case ':':
                Next();
                return new Token(TokenKind.Collon, source.CreateSpan(position - 1, 1));
            case ',':
                Next();
                return new Token(TokenKind.Comma, source.CreateSpan(position - 1, 1));
            case '.':
                Next();
                return new Token(TokenKind.Dot, source.CreateSpan(position - 1, 1));
            case '*':
                Next();
                return new Token(TokenKind.Star, source.CreateSpan(position - 1, 1));
            case '/':
                Next();
                return new Token(TokenKind.Slash, source.CreateSpan(position - 1, 1));
            case '@':
                Next();
                return new Token(TokenKind.At, source.CreateSpan(position - 1, 1));
            case '%':
                Next();
                return new Token(TokenKind.Percent, source.CreateSpan(position - 1, 1));
            default:
                Next();
                return new Token(TokenKind.BadToken, source.CreateSpan(position - 1, 1));
        }
    }
    private TokenKind GetDefinedKeywords(ReadOnlySpan<char> readOnlySpan)
    {
        return readOnlySpan switch {
            "true" => TokenKind.True,
            "false" => TokenKind.False,
            "and" => TokenKind.AndKeyword,
            "or" => TokenKind.OrKeyword,
            "not" => TokenKind.NotKeyword,
            "def" => TokenKind.DefineKeyword,
            "type" => TokenKind.TypeKeyword,
            "func" => TokenKind.FuncKeyword,
            "return" => TokenKind.ReturnKeyword,
            "match" => TokenKind.MatchKeyword,
            "if" => TokenKind.IfKeyword,
            "else" => TokenKind.ElseKeyword,
            "var" => TokenKind.VarKeyword,
            "typeof" => TokenKind.TypeofKeyword,
            "self" => TokenKind.SelfKeyword,
            _ => TokenKind.Identifier
        };
    }
    private void Next() {
        position++;
    }
    private char Peek(int offset)
    {
        var index = position + offset;
        if(index >= text.Length) return '\0';
        return text[index];
    }
    private char Current { get => Peek(0); }
}

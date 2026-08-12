namespace Lapis.Lexer;

/// <summary>
/// Tokens da spec §44, mais as adições justificadas no plano 03 §3.1:
/// <c>.</c>, <c>=&gt;</c>, <c>_</c>, <c>!</c>, <c>&amp;&amp;</c>, <c>||</c> e <c>Bad</c>.
///
/// Não existem <c>&gt;&gt;</c>, <c>&lt;&lt;</c>, <c>-&gt;</c> nem <c>::</c>. A ausência de
/// <c>&gt;&gt;</c> é intencional: elimina o problema de fechar dois generics aninhados.
/// </summary>
public enum TokenKind
{
    // literais
    Identifier,
    IntegerLiteral,
    FloatLiteral,
    StringLiteral,

    // palavras-chave
    DefKeyword,
    VarKeyword,
    FnKeyword,
    TypeKeyword,
    EnumKeyword,
    ReturnKeyword,
    TrueKeyword,
    FalseKeyword,
    IfKeyword,
    ElseKeyword,
    MatchKeyword,
    LoopKeyword,
    BreakKeyword,
    ContinueKeyword,
    MacroKeyword,
    ExpandKeyword,
    ConstraintKeyword,
    ThrowKeyword,
    IsKeyword,

    // delimitadores
    OpenParen,
    CloseParen,
    OpenBrace,
    CloseBrace,
    OpenBracket,
    CloseBracket,

    // pontuação
    Colon,
    Comma,
    Semicolon,
    Equals,
    Dot,
    Underscore,
    FatArrow,
    At,
    Question,

    // operadores
    Plus,
    Minus,
    Star,
    Slash,
    EqualsEquals,
    BangEquals,
    Less,
    Greater,
    LessEquals,
    GreaterEquals,
    Bang,
    AmpersandAmpersand,
    PipePipe,

    // controle
    EndOfFile,
    Bad,
}

public static class TokenKindExtensions
{
    /// <summary>
    /// Este token pode <b>iniciar</b> uma expressão? (Apêndice A §A.4.)
    ///
    /// É o que permite ao parser desempatar <c>&lt;</c> sem inventar regras: se
    /// depois do <c>&gt;</c> vem algo que não começa expressão, a leitura
    /// relacional está quebrada de qualquer forma e a genérica é a única possível
    /// (Q5).
    /// </summary>
    public static bool CanBeginExpression(this TokenKind kind) => kind is
        TokenKind.Identifier
        or TokenKind.IntegerLiteral
        or TokenKind.FloatLiteral
        or TokenKind.StringLiteral
        or TokenKind.TrueKeyword
        or TokenKind.FalseKeyword
        or TokenKind.FnKeyword
        or TokenKind.TypeKeyword
        or TokenKind.EnumKeyword
        or TokenKind.MatchKeyword
        or TokenKind.IfKeyword
        or TokenKind.LoopKeyword
        or TokenKind.BreakKeyword
        or TokenKind.ContinueKeyword
        or TokenKind.ReturnKeyword
        or TokenKind.ThrowKeyword
        or TokenKind.OpenParen
        or TokenKind.OpenBrace
        or TokenKind.OpenBracket
        or TokenKind.Dot
        or TokenKind.At
        or TokenKind.Minus
        or TokenKind.Bang;

    /// <summary>Texto para mensagens de diagnóstico.</summary>
    public static string Describe(this TokenKind kind) => kind switch
    {
        TokenKind.Identifier => "identificador",
        TokenKind.IntegerLiteral => "literal inteiro",
        TokenKind.FloatLiteral => "literal float",
        TokenKind.StringLiteral => "literal de string",
        TokenKind.DefKeyword => "'def'",
        TokenKind.VarKeyword => "'var'",
        TokenKind.FnKeyword => "'fn'",
        TokenKind.TypeKeyword => "'type'",
        TokenKind.EnumKeyword => "'enum'",
        TokenKind.ReturnKeyword => "'return'",
        TokenKind.TrueKeyword => "'true'",
        TokenKind.FalseKeyword => "'false'",
        TokenKind.IfKeyword => "'if'",
        TokenKind.ElseKeyword => "'else'",
        TokenKind.MatchKeyword => "'match'",
        TokenKind.LoopKeyword => "'loop'",
        TokenKind.BreakKeyword => "'break'",
        TokenKind.ContinueKeyword => "'continue'",
        TokenKind.MacroKeyword => "'macro'",
        TokenKind.ExpandKeyword => "'expand'",
        TokenKind.ConstraintKeyword => "'constraint'",
        TokenKind.ThrowKeyword => "'throw'",
        TokenKind.IsKeyword => "'is'",
        TokenKind.At => "'@'",
        TokenKind.Question => "'?'",
        TokenKind.OpenParen => "'('",
        TokenKind.CloseParen => "')'",
        TokenKind.OpenBrace => "'{'",
        TokenKind.CloseBrace => "'}'",
        TokenKind.OpenBracket => "'['",
        TokenKind.CloseBracket => "']'",
        TokenKind.Colon => "':'",
        TokenKind.Comma => "','",
        TokenKind.Semicolon => "';'",
        TokenKind.Equals => "'='",
        TokenKind.Dot => "'.'",
        TokenKind.Underscore => "'_'",
        TokenKind.FatArrow => "'=>'",
        TokenKind.Plus => "'+'",
        TokenKind.Minus => "'-'",
        TokenKind.Star => "'*'",
        TokenKind.Slash => "'/'",
        TokenKind.EqualsEquals => "'=='",
        TokenKind.BangEquals => "'!='",
        TokenKind.Less => "'<'",
        TokenKind.Greater => "'>'",
        TokenKind.LessEquals => "'<='",
        TokenKind.GreaterEquals => "'>='",
        TokenKind.Bang => "'!'",
        TokenKind.AmpersandAmpersand => "'&&'",
        TokenKind.PipePipe => "'||'",
        TokenKind.EndOfFile => "fim do arquivo",
        TokenKind.Bad => "token inválido",
        _ => kind.ToString(),
    };
}

namespace LapisLang.Core;
public enum TokenKind {
    // Control Tokens
    BadToken = -2,
    EoFToken = -1,

    //Whitespace 
    WhiteSpace = 1,
    MultilineComentary,
    SingleLineComentary,


    // Literas
    Identifier,
    IntNumber,
    DecimalNumber,
    True,
    False,
    SingleQuoteString,
    DoubleQuoteString,

    // Operators
    Plus,
    Minus,
    Star,
    Slash,
    AndKeyword,
    Or,
    DoubleEquals,
    BangEquals,
    RightArrow,
    RightArrowEquals,
    LeftArrowEquals,
    LeftArrow,
    Equal,
    Bang,


    OpenParenthesis,
    CloseParenthesis,
    OpenCurlyBrace,
    CloseCurlyBrace,
    OpenBracket,
    CloseBracket,
    SemiCollon,
    Collon,
    Comma,
    Dot,
    At,

    // Keywords
    NamespaceKeyword,
    ImportKeyword,
    TypeKeyword,
    FuncKeyword,
    RunKeyword,
    ExternalKeyword,
    CompilerSymbol,
    ImplementKeyword,
    ReturnKeyword,
    IfKeyword,
    ElseKeyword,
    VarKeyword,
    LoopKeyword,
    BreakKeyword,
    ConstKeyword,
    OrKeyword,
    NotKeyword,
    Percent,
    DefineKeyword,
    MatchKeyword,
    BangAnd,
    And,
    TypeofKeyword,
    SelfKeyword,
}

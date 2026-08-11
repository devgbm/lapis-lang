using System.Collections.Immutable;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;
using Lapis.Lexer;

namespace Lapis.Parser;

/// <summary>
/// Lê fragmentos de sintaxe a partir de uma lista de tokens — o que o matcher de
/// macros precisa para casar um padrão (plano 17 §17.5).
///
/// Uma invocação como <c>@foreach user in users { }</c> não parseia com a
/// gramática da linguagem: <c>user in users</c> não é expressão. Quem sabe a forma
/// é o padrão da macro, e é ele que dirige as leituras aqui — um
/// <c>Expression:e</c> vira <see cref="ReadExpression"/>, um <c>Block:b</c> vira
/// <see cref="ReadBlock"/>, e um literal sintático vira uma comparação de token.
///
/// Os diagnósticos vão para um saco descartável: uma regra que não casa não é
/// erro, é a próxima regra (spec de macros §7.1).
/// </summary>
public sealed class SyntaxFragmentReader
{
    private readonly Parser _parser;
    private readonly DiagnosticBag _attempt = new();

    /// <param name="tokens">
    /// Os tokens da invocação. O <c>EndOfFile</c> é acrescentado aqui, porque o
    /// <see cref="TokenStream"/> exige um e a invocação não traz.
    /// </param>
    public SyntaxFragmentReader(ImmutableArray<Token> tokens)
    {
        var end = tokens.IsDefaultOrEmpty
            ? new Token(TokenKind.EndOfFile, SourceSpan.Synthetic, string.Empty)
            : new Token(TokenKind.EndOfFile, new SourceSpan(tokens[^1].Span.End, 0), string.Empty);

        _parser = Parser.OverFragment([.. tokens, end], _attempt);
    }

    /// <summary>Índice do próximo token, para recortar o que uma captura consumiu.</summary>
    public int Position => _parser.Tokens.Mark();

    public bool AtEnd => _parser.Tokens.AtEnd;

    public Token Current => _parser.Tokens.Current;

    /// <summary>Alguma leitura falhou desde a última verificação?</summary>
    public bool Failed => _attempt.HasErrors;

    public Token Advance() => _parser.Tokens.Advance();

    public Expression ReadExpression() => _parser.ParseExpression();

    public BlockExpression ReadBlock() => _parser.ParseBlock();

    public Statement? ReadStatement() => _parser.ParseStatement();

    public TypeSyntax ReadType() => _parser.ParseType();
}

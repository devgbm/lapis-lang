using Lapis.Diagnostics;

namespace Lapis.Lexer;

/// <summary>
/// Um token com posição de origem. <see cref="Text"/> é o lexema bruto,
/// preservado para mensagens de erro e impressão; <see cref="Value"/> é o valor
/// decodificado (<c>long</c>, <c>double</c> ou <c>string</c>) quando aplicável.
/// </summary>
public readonly record struct Token(TokenKind Kind, SourceSpan Span, string Text, object? Value = null)
{
    public long IntegerValue => Value is long v
        ? v
        : throw new InternalCompilerException($"token {Kind} não carrega um inteiro", Span);

    public double FloatValue => Value is double v
        ? v
        : throw new InternalCompilerException($"token {Kind} não carrega um float", Span);

    public string StringValue => Value is string v
        ? v
        : throw new InternalCompilerException($"token {Kind} não carrega uma string", Span);

    public override string ToString() => $"{Kind} {Span} '{Text}'";
}

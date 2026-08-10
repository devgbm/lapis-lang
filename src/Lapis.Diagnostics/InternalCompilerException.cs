namespace Lapis.Diagnostics;

/// <summary>
/// Estado impossível na implementação: nó de Core AST inválido, tipo ausente numa
/// Typed Core AST, variante desconhecida na construção.
///
/// Nunca deve ser lançada por um programa <c>.ls</c> — nem bem-formado, nem
/// mal-formado que tenha passado pelo type checker. Se escapar, é bug nosso
/// (spec §30).
/// </summary>
public sealed class InternalCompilerException : Exception
{
    public InternalCompilerException(string message, SourceSpan? span = null)
        : base(span is { } s ? $"{message} (em {s})" : message)
    {
        Span = span;
    }

    public SourceSpan? Span { get; }

    /// <summary>Atalho para braços de <c>switch</c> que deveriam ser inalcançáveis.</summary>
    public static InternalCompilerException Unreachable(object node, SourceSpan? span = null) =>
        new($"nó inesperado: {node.GetType().Name}", span);
}

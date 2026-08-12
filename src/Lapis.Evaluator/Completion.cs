using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.Evaluator;

public enum CompletionKind
{
    /// <summary>Avaliação normal: o valor é o resultado da expressão.</summary>
    Normal,

    /// <summary>Um <c>return</c> foi executado; propaga até a chamada de função.</summary>
    Return,

    /// <summary>
    /// Um <c>break</c> foi executado; propaga até o <c>loop</c> que o rótulo (ou
    /// a ausência dele) alcança — plano 26, M16. Mesmo mecanismo de
    /// <see cref="Return"/>: nenhuma exceção C#, nenhum caminho novo.
    /// </summary>
    Break,

    /// <summary>
    /// Um <c>continue</c> foi executado; propaga até o <c>loop</c> que reinicia.
    /// </summary>
    Continue,

    /// <summary>Erro de execução da linguagem (divisão inteira por zero); propaga até o topo.</summary>
    Abort,
}

/// <summary>
/// Como <c>return</c> é modelado (spec §28).
///
/// Sem exceções C#, por três motivos (plano 08 §8.2): (1) exceções tornariam
/// funções lentas; (2) o partial evaluator precisa inspecionar o resultado sem
/// <c>try/catch</c>; (3) o registro torna a regra de propagação explícita no
/// código — o que importa numa implementação de referência.
/// </summary>
public readonly record struct Completion(
    CompletionKind Kind,
    Value Value,
    string? Code = null,
    SourceSpan? Span = null,
    string? Label = null)
{
    public bool IsNormal => Kind == CompletionKind.Normal;

    public static Completion Normal(Value value) => new(CompletionKind.Normal, value);

    public static Completion Return(Value value) => new(CompletionKind.Return, value);

    public static Completion Break(string? label, Value value) => new(CompletionKind.Break, value, Label: label);

    public static Completion Continue(string? label) =>
        new(CompletionKind.Continue, VoidValue.Instance, Label: label);

    public static Completion Abort(string code, SourceSpan span, string message) =>
        new(CompletionKind.Abort, new StrValue(message), code, span);
}

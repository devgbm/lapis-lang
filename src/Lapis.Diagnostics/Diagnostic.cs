using System.Collections.Immutable;

namespace Lapis.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>Informação auxiliar anexada a um diagnóstico (sugestão, local anterior, ...).</summary>
public sealed record DiagnosticNote(string Message, SourceSpan? Span = null);

/// <summary>
/// Um erro, aviso ou informação dirigido ao autor do programa <c>.ls</c>.
///
/// Distinto de <see cref="InternalCompilerException"/>, que sinaliza bug da
/// implementação, e dos valores <c>Result</c> da linguagem (spec §30).
/// </summary>
public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    SourceSpan Span,
    ImmutableArray<DiagnosticNote> Notes)
{
    public static Diagnostic Error(string code, SourceSpan span, string message, params DiagnosticNote[] notes) =>
        new(code, DiagnosticSeverity.Error, message, span, [.. notes]);

    public static Diagnostic Warning(string code, SourceSpan span, string message, params DiagnosticNote[] notes) =>
        new(code, DiagnosticSeverity.Warning, message, span, [.. notes]);

    public override string ToString() => $"{Severity.ToString().ToLowerInvariant()} {Code}: {Message}";
}

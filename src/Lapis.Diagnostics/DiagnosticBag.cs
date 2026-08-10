using System.Collections;
using System.Collections.Immutable;

namespace Lapis.Diagnostics;

/// <summary>
/// Acumulador de diagnósticos de uma fase. Cada fase reporta e tenta continuar;
/// o orquestrador decide parar quando <see cref="HasErrors"/> for verdadeiro
/// ao final da fase (plano 00 §5.1).
/// </summary>
public sealed class DiagnosticBag : IEnumerable<Diagnostic>
{
    private readonly List<Diagnostic> _diagnostics = [];

    public bool HasErrors { get; private set; }

    public int Count => _diagnostics.Count;

    public void Report(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        _diagnostics.Add(diagnostic);

        if (diagnostic.Severity == DiagnosticSeverity.Error)
        {
            HasErrors = true;
        }
    }

    public void ReportError(string code, SourceSpan span, string message, params DiagnosticNote[] notes) =>
        Report(Diagnostic.Error(code, span, message, notes));

    public void ReportWarning(string code, SourceSpan span, string message, params DiagnosticNote[] notes) =>
        Report(Diagnostic.Warning(code, span, message, notes));

    public void AddRange(IEnumerable<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Report(diagnostic);
        }
    }

    /// <summary>
    /// Diagnósticos ordenados por offset e, em empate, por código — a ordem em que
    /// são apresentados ao usuário e asseverada pelos testes.
    /// </summary>
    public ImmutableArray<Diagnostic> ToSortedArray() =>
        [.. _diagnostics
            .OrderBy(d => d.Span.Start)
            .ThenBy(d => d.Code, StringComparer.Ordinal)];

    public IEnumerator<Diagnostic> GetEnumerator() => _diagnostics.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

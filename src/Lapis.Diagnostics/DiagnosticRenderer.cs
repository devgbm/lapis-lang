using System.Text;

namespace Lapis.Diagnostics;

/// <summary>
/// Renderiza diagnósticos no formato de compilador clássico (plano 10 §10.4):
///
/// <code>
/// examples/hello.ls(4,9): error LAP0201: variável 'reslt' não existe
///     4 |     print(reslt);
///       |           ^^^^^
///       = nota: você quis dizer 'result'?
/// </code>
/// </summary>
public static class DiagnosticRenderer
{
    private const int MaxCaretWidth = 80;

    public static string Render(Diagnostic diagnostic, SourceText source)
    {
        var builder = new StringBuilder();
        RenderTo(builder, diagnostic, source);
        return builder.ToString();
    }

    public static string RenderAll(IEnumerable<Diagnostic> diagnostics, SourceText source)
    {
        var builder = new StringBuilder();

        foreach (var diagnostic in diagnostics)
        {
            RenderTo(builder, diagnostic, source);
        }

        return builder.ToString();
    }

    private static void RenderTo(StringBuilder builder, Diagnostic diagnostic, SourceText source)
    {
        var position = source.GetPosition(Math.Min(diagnostic.Span.Start, source.Length));
        var severity = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info",
        };

        builder.Append(source.FileName)
               .Append('(').Append(position.Line).Append(',').Append(position.Column).Append(')')
               .Append(": ").Append(severity).Append(' ').Append(diagnostic.Code)
               .Append(": ").AppendLine(diagnostic.Message);

        AppendSnippet(builder, diagnostic.Span, source, position);

        foreach (var note in diagnostic.Notes)
        {
            builder.Append("      = nota: ").AppendLine(note.Message);
        }
    }

    private static void AppendSnippet(StringBuilder builder, SourceSpan span, SourceText source, SourcePosition position)
    {
        var lineText = source.GetLineText(position.Line);
        var lineNumber = position.Line.ToString();
        var gutter = new string(' ', lineNumber.Length);

        builder.Append("  ").Append(lineNumber).Append(" | ").AppendLine(lineText);

        // O cursor não atravessa a linha: spans multilinha são truncados no fim da linha.
        var caretStart = position.Column - 1;
        var available = Math.Max(lineText.Length - caretStart, 0);
        var caretWidth = Math.Clamp(span.Length == 0 ? 1 : Math.Min(span.Length, available), 1, MaxCaretWidth);

        builder.Append("  ").Append(gutter).Append(" | ")
               .Append(new string(' ', caretStart))
               .Append('^', caretWidth)
               .AppendLine();
    }
}

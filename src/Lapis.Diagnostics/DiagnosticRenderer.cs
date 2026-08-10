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

    private const string Reset = "\u001b[0m";
    private const string Red = "\u001b[31m";
    private const string Yellow = "\u001b[33m";
    private const string Cyan = "\u001b[36m";
    private const string Dim = "\u001b[2m";

    public static string Render(Diagnostic diagnostic, SourceText source, bool color = false)
    {
        var builder = new StringBuilder();
        RenderTo(builder, diagnostic, source, color);
        return builder.ToString();
    }

    public static string RenderAll(IEnumerable<Diagnostic> diagnostics, SourceText source, bool color = false)
    {
        var builder = new StringBuilder();

        foreach (var diagnostic in diagnostics)
        {
            RenderTo(builder, diagnostic, source, color);
        }

        return builder.ToString();
    }

    private static void RenderTo(StringBuilder builder, Diagnostic diagnostic, SourceText source, bool color)
    {
        var position = source.GetPosition(Math.Min(diagnostic.Span.Start, source.Length));
        var severity = SeverityText(diagnostic.Severity);
        var accent = diagnostic.Severity == DiagnosticSeverity.Error ? Red : Yellow;

        builder.Append(source.FileName)
               .Append('(').Append(position.Line).Append(',').Append(position.Column).Append(')')
               .Append(": ")
               .Append(Paint(severity + " " + diagnostic.Code, accent, color))
               .Append(": ").AppendLine(diagnostic.Message);

        AppendSnippet(builder, diagnostic.Span, source, position, accent, color);

        foreach (var note in diagnostic.Notes)
        {
            builder.Append("      = ").Append(Paint("nota", Cyan, color)).Append(": ").AppendLine(note.Message);
        }
    }

    private static string SeverityText(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        _ => "info",
    };

    private static void AppendSnippet(
        StringBuilder builder,
        SourceSpan span,
        SourceText source,
        SourcePosition position,
        string accent,
        bool color)
    {
        var lineText = source.GetLineText(position.Line);
        var lineNumber = position.Line.ToString();
        var gutter = new string(' ', lineNumber.Length);

        builder.Append("  ").Append(Paint(lineNumber + " |", Dim, color)).Append(' ').AppendLine(lineText);

        // O cursor não atravessa a linha: spans multilinha são truncados no fim da linha.
        var caretStart = position.Column - 1;
        var available = Math.Max(lineText.Length - caretStart, 0);
        var caretWidth = Math.Clamp(span.Length == 0 ? 1 : Math.Min(span.Length, available), 1, MaxCaretWidth);

        builder.Append("  ").Append(Paint(gutter + " |", Dim, color)).Append(' ')
               .Append(new string(' ', caretStart))
               .Append(Paint(new string('^', caretWidth), accent, color))
               .AppendLine();
    }

    private static string Paint(string text, string code, bool color) => color ? code + text + Reset : text;
}

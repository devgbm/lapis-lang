using System.Text;

namespace Lapis.Diagnostics;

/// <summary>
/// Diagnósticos em JSON, uma linha por diagnóstico (plano 10 §10.3).
///
/// Linha por linha, e não um array: a saída é consumível em streaming e
/// sobrevive a uma execução interrompida, que é o que uma ferramenta editora
/// precisa. Os campos são os do plano — <c>file, line, column, code, severity,
/// message</c> — mais <c>notes</c>, que já existem no modelo.
///
/// Escrito à mão em vez de <c>System.Text.Json</c>: o formato é fixo, os tipos
/// são três, e uma dependência de serialização aqui só acrescentaria superfície.
/// </summary>
public static class DiagnosticJson
{
    public static string Render(Diagnostic diagnostic, SourceText source)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(source);

        var position = source.GetPosition(Math.Min(diagnostic.Span.Start, source.Length));
        var builder = new StringBuilder();

        builder.Append("{\"file\":").Append(Quote(source.FileName))
               .Append(",\"line\":").Append(position.Line)
               .Append(",\"column\":").Append(position.Column)
               .Append(",\"offset\":").Append(diagnostic.Span.Start)
               .Append(",\"length\":").Append(diagnostic.Span.Length)
               .Append(",\"severity\":").Append(Quote(Severity(diagnostic.Severity)))
               .Append(",\"code\":").Append(Quote(diagnostic.Code))
               .Append(",\"message\":").Append(Quote(diagnostic.Message))
               .Append(",\"notes\":[");

        for (var i = 0; i < diagnostic.Notes.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(Quote(diagnostic.Notes[i].Message));
        }

        return builder.Append("]}").ToString();
    }

    public static string RenderAll(IEnumerable<Diagnostic> diagnostics, SourceText source)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var builder = new StringBuilder();

        foreach (var diagnostic in diagnostics)
        {
            builder.AppendLine(Render(diagnostic, source));
        }

        return builder.ToString();
    }

    private static string Severity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        _ => "info",
    };

    private static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('"');

        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    // Acentuação sai literal (a saída é UTF-8); só o que o JSON
                    // proíbe cru é escapado.
                    if (char.IsControl(c))
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}

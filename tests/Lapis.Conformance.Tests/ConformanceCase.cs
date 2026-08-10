using System.Collections.Immutable;
using System.Globalization;

namespace Lapis.Conformance.Tests;

/// <summary>Um diagnóstico esperado, com posição opcional.</summary>
public sealed record ExpectedDiagnostic(string Severity, string Code, int? Line, int? Column)
{
    public override string ToString() =>
        Line is null ? $"{Severity} {Code}" : $"{Severity} {Code} at {Line}:{Column}";
}

/// <summary>
/// Um caso de conformidade: um arquivo <c>.ls</c> com um cabeçalho de expectativa
/// em comentários (plano 11 §11.1).
///
/// Tudo num arquivo só, de propósito: o caso é escrito, lido e executado com
/// <c>lapis</c> sem precisar de arquivo companheiro.
/// </summary>
public sealed record ConformanceCase(
    string RelativePath,
    string Source,
    string? ExpectedOutput,
    ImmutableArray<ExpectedDiagnostic> ExpectedDiagnostics,
    int? ExpectedExitCode,
    bool ExpectsAbort,
    string? SkipReason)
{
    /// <summary>Um caso sem nenhuma diretiva não afirma nada — é erro de autoria.</summary>
    public bool HasExpectations =>
        ExpectedOutput is not null
        || !ExpectedDiagnostics.IsEmpty
        || ExpectedExitCode is not null
        || ExpectsAbort;

    /// <summary>
    /// O exit code que o caso exige. Quando não declarado, decorre do que foi
    /// afirmado: diagnóstico de erro ⇒ 65, aborto ⇒ 1, o resto ⇒ 0.
    /// </summary>
    public int RequiredExitCode =>
        ExpectedExitCode
        ?? (ExpectedDiagnostics.Any(d => d.Severity == "error") ? Cli.ExitCodes.CompilationError
            : ExpectsAbort ? Cli.ExitCodes.RuntimeAbort
            : Cli.ExitCodes.Success);

    public override string ToString() => RelativePath;

    public static ConformanceCase Parse(string relativePath, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var diagnostics = ImmutableArray.CreateBuilder<ExpectedDiagnostic>();

        List<string>? output = null;
        int? exitCode = null;
        var abort = false;
        string? skip = null;
        var collectingOutput = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // O cabeçalho é o bloco de comentários do topo; a primeira linha que
            // não é comentário encerra, mesmo sem `// ---`.
            if (!trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                break;
            }

            var content = trimmed[2..];

            if (content.TrimStart().StartsWith("---", StringComparison.Ordinal))
            {
                break;
            }

            if (collectingOutput)
            {
                // Uma linha de saída é literal: só o `// ` inicial sai, para que
                // espaços à direita e linhas em branco sejam preservados.
                output!.Add(content.StartsWith(' ') ? content[1..] : content);
                continue;
            }

            var directive = content.Trim();

            if (directive.StartsWith("skip:", StringComparison.Ordinal))
            {
                skip = directive[5..].Trim();
                continue;
            }

            if (!directive.StartsWith("expect:", StringComparison.Ordinal))
            {
                continue;
            }

            var body = directive[7..].Trim();

            if (body == "output")
            {
                output = [];
                collectingOutput = true;
            }
            else if (body == "abort")
            {
                abort = true;
            }
            else if (body.StartsWith("exit ", StringComparison.Ordinal))
            {
                exitCode = int.Parse(body[5..].Trim(), CultureInfo.InvariantCulture);
            }
            else if (body.StartsWith("error ", StringComparison.Ordinal))
            {
                diagnostics.Add(ParseDiagnostic("error", body[6..]));
            }
            else if (body.StartsWith("warning ", StringComparison.Ordinal))
            {
                diagnostics.Add(ParseDiagnostic("warning", body[8..]));
            }
            else
            {
                throw new FormatException($"{relativePath}: diretiva desconhecida '{body}'");
            }
        }

        return new ConformanceCase(
            relativePath,
            text,
            output is null ? null : Join(output),
            diagnostics.ToImmutable(),
            exitCode,
            abort,
            skip);
    }

    /// <summary><c>LAP0201</c> ou <c>LAP0201 at 4:9</c>.</summary>
    private static ExpectedDiagnostic ParseDiagnostic(string severity, string body)
    {
        var parts = body.Trim().Split(" at ", StringSplitOptions.TrimEntries);
        var code = parts[0];

        if (parts.Length == 1)
        {
            return new ExpectedDiagnostic(severity, code, null, null);
        }

        var position = parts[1].Split(':');

        return new ExpectedDiagnostic(
            severity,
            code,
            int.Parse(position[0], CultureInfo.InvariantCulture),
            int.Parse(position[1], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Saída esperada com quebra final. Um bloco vazio significa "nada foi
    /// impresso", que é diferente de "não verifiquei a saída".
    /// </summary>
    private static string Join(List<string> lines) =>
        lines.Count == 0 ? string.Empty : string.Join("\n", lines) + "\n";
}

using System.Collections.Immutable;
using System.Text;
using Lapis.Cli;
using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.Conformance.Tests;

public sealed record ConformanceOutcome(string Output, int ExitCode, ImmutableArray<Diagnostic> Diagnostics);

/// <summary>
/// Executa um caso e confronta o resultado com o que ele declara.
///
/// Passa pelo <c>Pipeline</c> e por <c>ExitCodes.For</c> — os mesmos que o
/// executável usa —, e não por uma reimplementação: um teste que reimplementa o
/// que testa não testa nada (plano 10 §10.1).
/// </summary>
public static class ConformanceRunner
{
    public static ConformanceOutcome Execute(ConformanceCase testCase, bool freshPrelude = false)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        var output = new StringOutput();
        var source = SourceText.From(testCase.Source, testCase.RelativePath);

        var result = freshPrelude
            ? Pipeline.Compile(source, PipelineStage.Evaluate, new RuntimeContext(output), PreludeLoader.LoadFresh())
            : Pipeline.Compile(source, PipelineStage.Evaluate, new RuntimeContext(output));

        return new ConformanceOutcome(
            output.Text, ExitCodes.For(result), WithAbort(result));
    }

    /// <summary>
    /// O aborto entra na lista de diagnósticos, como o CLI já faz ao renderizá-lo
    /// (plano 10 §10.2). Sem isso um código LAP03xx não teria como ser afirmado
    /// por um caso — e a cobertura por diagnóstico ficaria com um buraco em
    /// exatamente a faixa que descreve falha em execução.
    /// </summary>
    private static ImmutableArray<Diagnostic> WithAbort(CompilationResult result) =>
        result.Evaluation is { Status: Evaluator.ExecutionStatus.Aborted } aborted
            ? result.Diagnostics.Add(Diagnostic.Error(
                aborted.Code ?? "LAP0300",
                aborted.Span ?? SourceSpan.Synthetic,
                aborted.Message ?? "execução abortada"))
            : result.Diagnostics;

    /// <summary>
    /// Devolve a descrição da primeira divergência, ou <c>null</c> se o caso passa.
    /// Devolver em vez de assertar é o que permite ao <c>skip:</c> checar se um
    /// caso adormecido voltou a passar.
    /// </summary>
    public static string? Verify(ConformanceCase testCase, ConformanceOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(outcome);

        var failures = new StringBuilder();

        if (testCase.ExpectedOutput is { } expected)
        {
            var actual = outcome.Output.ReplaceLineEndings("\n");

            if (actual != expected)
            {
                failures.AppendLine("saída divergente:")
                        .AppendLine("  esperado: " + Show(expected))
                        .AppendLine("  obtido:   " + Show(actual));
            }
        }

        CompareDiagnostics(testCase, outcome, failures);

        if (outcome.ExitCode != testCase.RequiredExitCode)
        {
            failures.AppendLine(
                $"exit code divergente: esperado {testCase.RequiredExitCode}, obtido {outcome.ExitCode}");
        }

        return failures.Length == 0 ? null : failures.ToString();
    }

    private static void CompareDiagnostics(
        ConformanceCase testCase,
        ConformanceOutcome outcome,
        StringBuilder failures)
    {
        var pending = outcome.Diagnostics.ToList();

        foreach (var expected in testCase.ExpectedDiagnostics)
        {
            var index = pending.FindIndex(d => Matches(expected, d, testCase.Source));

            if (index < 0)
            {
                failures.AppendLine($"diagnóstico esperado e ausente: {expected}");
                continue;
            }

            pending.RemoveAt(index);
        }

        // Um diagnóstico a mais reprova: silenciar o inesperado é como um teste
        // deixa de notar uma regressão.
        foreach (var unexpected in pending)
        {
            failures.AppendLine($"diagnóstico inesperado: {unexpected.Severity} {unexpected.Code}");
        }
    }

    private static bool Matches(ExpectedDiagnostic expected, Diagnostic actual, string source)
    {
        if (!string.Equals(actual.Code, expected.Code, StringComparison.Ordinal)
            || !string.Equals(actual.Severity.ToString(), expected.Severity, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (expected.Line is null)
        {
            return true;
        }

        var position = SourceText.From(source).GetPosition(actual.Span.Start);

        return position.Line == expected.Line && position.Column == expected.Column;
    }

    /// <summary>Torna visíveis quebras e espaços à direita, que são a metade dos enganos.</summary>
    private static string Show(string text) =>
        "\"" + text.Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
}

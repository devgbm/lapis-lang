using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.Evaluator.Tests;

public abstract class EvaluatorTestBase
{
    protected sealed record RunOutcome(EvaluationResult Result, string Output)
    {
        public ExecutionStatus Status => Result.Status;
    }

    protected static RunOutcome Run(string source)
    {
        var diagnostics = new DiagnosticBag();
        var file = Parser.Parser.Parse(SourceText.From(source), diagnostics);
        var core = Desugar.Desugarer.Desugar(file, diagnostics, Cli.Pipeline.KnownVariantsOf(PreludeFixture.Scope));
        var typed = TypeChecker.TypeChecker.Check(core, PreludeFixture.Scope, diagnostics);

        diagnostics.HasErrors.ShouldBeFalse(
            $"erros de compilação: {string.Join(", ", diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        var output = new StringOutput();
        var result = Evaluator.Run(typed, PreludeFixture.Scope, new RuntimeContext(output));

        return new RunOutcome(result, output.Text);
    }

    /// <summary>Saída completa do programa (o único observável da linguagem).</summary>
    protected static string Output(string source)
    {
        var outcome = Run(source);

        outcome.Status.ShouldBe(
            ExecutionStatus.Completed,
            $"execução abortada: {outcome.Result.Message}");

        return outcome.Output;
    }

    /// <summary>Imprime uma expressão e devolve o texto, sem a quebra de linha final.</summary>
    protected static string Eval(string expression) =>
        Output($"print({expression});").TrimEnd('\n');

    /// <summary>Executa esperando um aborto e devolve o código do erro.</summary>
    protected static string ExpectAbort(string source)
    {
        var outcome = Run(source);

        outcome.Status.ShouldBe(ExecutionStatus.Aborted);
        return outcome.Result.Code.ShouldNotBeNull();
    }
}

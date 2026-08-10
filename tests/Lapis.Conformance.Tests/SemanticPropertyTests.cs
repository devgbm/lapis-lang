using Lapis.Ast.Printing;
using Lapis.Cli;
using Lapis.Diagnostics;
using Lapis.Evaluator;
using Lapis.Runtime;

namespace Lapis.Conformance.Tests;

/// <summary>
/// As propriedades da semântica (plano 11 §11.6), rodadas sobre o corpus inteiro.
///
/// Não são testes de caso: são afirmações que precisam valer para **todo**
/// programa. Um caso novo no corpus passa a exercitá-las de graça.
/// </summary>
public sealed class SemanticPropertyTests
{
    private static IEnumerable<ConformanceCase> Corpus =>
        ConformanceCorpus.All.Where(c => c.SkipReason is null);

    /// <summary>
    /// Se o checker aceitou, o evaluator não lança: exceção de C# significa bug da
    /// implementação, nunca erro do programa (spec §30, §58.3).
    /// </summary>
    [Fact]
    public void WellTyped_ProgramsDoNotThrow()
    {
        foreach (var testCase in Corpus)
        {
            var source = SourceText.From(testCase.Source, testCase.RelativePath);
            var checkOnly = Pipeline.Compile(source, PipelineStage.TypeCheck);

            if (checkOnly.HasErrors)
            {
                continue;
            }

            Should.NotThrow(
                () => Pipeline.Compile(source, PipelineStage.Evaluate, new RuntimeContext(new StringOutput())),
                testCase.RelativePath);
        }
    }

    /// <summary>Mesma fonte, mesmo stdout e mesmo desfecho — sempre.</summary>
    [Fact]
    public void Evaluation_IsDeterministic()
    {
        foreach (var testCase in Corpus)
        {
            var first = ConformanceRunner.Execute(testCase);
            var second = ConformanceRunner.Execute(testCase);

            second.Output.ShouldBe(first.Output, testCase.RelativePath);
            second.ExitCode.ShouldBe(first.ExitCode, testCase.RelativePath);
            second.Diagnostics.Select(d => d.Code)
                  .ShouldBe(first.Diagnostics.Select(d => d.Code), testCase.RelativePath);
        }
    }

    /// <summary>
    /// Rodar o pipeline duas vezes no mesmo processo dá o mesmo resultado: nenhum
    /// estado atravessa compilações. O prelude é compartilhado justamente por ser
    /// imutável — se não fosse, esta propriedade cairia.
    /// </summary>
    [Fact]
    public void Pipeline_IsPure()
    {
        foreach (var testCase in Corpus)
        {
            var source = SourceText.From(testCase.Source, testCase.RelativePath);

            var first = Pipeline.Compile(source, PipelineStage.Evaluate, new RuntimeContext(new StringOutput()));
            var second = Pipeline.Compile(source, PipelineStage.Evaluate, new RuntimeContext(new StringOutput()));

            second.Diagnostics.Select(d => $"{d.Code}@{d.Span.Start}")
                  .ShouldBe(first.Diagnostics.Select(d => $"{d.Code}@{d.Span.Start}"), testCase.RelativePath);
        }
    }

    /// <summary>
    /// Spec §21/§30: indexar produz um `Result`, sempre — dentro ou fora dos
    /// limites, com índice negativo, em array vazio. Nunca lança, nunca aborta.
    /// </summary>
    [Theory]
    [InlineData("[]", "0")]
    [InlineData("[1]", "0")]
    [InlineData("[1]", "1")]
    [InlineData("[1]", "-1")]
    [InlineData("[1, 2, 3]", "2")]
    [InlineData("[1, 2, 3]", "9223372036854775807")]
    public void Indexing_AlwaysProducesResult(string array, string index)
    {
        // O array vazio precisa de anotação (spec §18), então o caso dele é escrito
        // com uma; os demais dispensam.
        var declaration = array == "[]" ? "def a: Int[] = [];" : $"def a = {array};";
        var source = SourceText.From($"{declaration}\nprint(a[{index}]);\n", "propriedade.ls");

        var output = new StringOutput();
        var result = Pipeline.Compile(source, PipelineStage.Evaluate, new RuntimeContext(output));

        result.HasErrors.ShouldBeFalse();
        result.Evaluation!.Status.ShouldBe(ExecutionStatus.Completed);
        output.Text.ShouldSatisfyAllConditions(
            () => output.Text.ShouldStartWith("Result."),
            () => output.Text.ShouldNotContain("Exception"));
    }

    /// <summary>
    /// <c>desugar(parse(print(core))) ≡ core</c> — o round-trip do plano 02 §2.8,
    /// aqui sobre o corpus inteiro em vez de uma lista escolhida a dedo.
    ///
    /// É o que habilita <c>lapis pe</c> a emitir programas executáveis: sem ele, o
    /// residual do partial evaluator não teria como ser reexecutado.
    /// </summary>
    [Fact]
    public void Printer_Roundtrips()
    {
        foreach (var testCase in Corpus)
        {
            var source = SourceText.From(testCase.Source, testCase.RelativePath);
            var original = Pipeline.Compile(source, PipelineStage.Desugar);

            // Só faz sentido em programas que chegaram à Core.
            if (original.HasErrors || original.Core is null)
            {
                continue;
            }

            var printed = CoreSourcePrinter.Print(original.Core);
            var reparsed = Pipeline.Compile(SourceText.From(printed, testCase.RelativePath), PipelineStage.Desugar);

            reparsed.HasErrors.ShouldBeFalse(
                $"{testCase.RelativePath}: o código impresso não reparseia:\n{printed}");

            CoreSExprPrinter.Print(reparsed.Core!)
                .ShouldBe(CoreSExprPrinter.Print(original.Core), $"{testCase.RelativePath}\n{printed}");
        }
    }
}

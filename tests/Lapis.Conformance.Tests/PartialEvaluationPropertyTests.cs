using Lapis.Ast.Printing;
using Lapis.Cli;
using Lapis.Diagnostics;
using Lapis.Evaluator;
using Lapis.PartialEvaluator;
using Lapis.Runtime;

namespace Lapis.Conformance.Tests;

/// <summary>
/// As invariantes do partial evaluator, rodadas sobre o corpus inteiro
/// (spec §40, §54; plano 12).
///
/// É a diferença entre "o PE funciona nos exemplos que escrevi" e "o PE funciona":
/// cada caso novo de conformidade passa a exercitá-lo de graça, incluindo os que
/// foram escritos para pegar bug de outra fase.
/// </summary>
public sealed class PartialEvaluationPropertyTests
{
    /// <summary>Casos que compilam — o PE roda sobre a Core, não sobre texto quebrado.</summary>
    private static IEnumerable<(ConformanceCase Case, CompilationResult Compiled)> Compilable =>
        ConformanceCorpus.All
            .Where(c => c.SkipReason is null)
            .Select(c => (c, Pipeline.Compile(SourceText.From(c.Source, c.RelativePath), PipelineStage.TypeCheck)))
            .Where(pair => !pair.Item2.HasErrors);

    /// <summary>
    /// <c>evaluate(P) ≡ evaluate(PE(P))</c> — a propriedade que define o partial
    /// evaluator (spec §40). Valor, saída e desfecho: se qualquer um divergir, o
    /// PE não está otimizando, está quebrando o programa.
    /// </summary>
    [Fact]
    public void Equivalence_OverTheWholeCorpus()
    {
        foreach (var (testCase, compiled) in Compilable)
        {
            var residual = CoreSourcePrinter.Print(
                PartialEvaluator.PartialEvaluator.Specialize(
                    compiled.Core!, types: compiled.Typed).Residual);

            var original = Execute(testCase.Source, testCase.RelativePath);
            var specialized = Execute(residual, testCase.RelativePath);

            specialized.Output.ShouldBe(original.Output, $"{testCase.RelativePath}\nresidual:\n{residual}");
            specialized.Status.ShouldBe(original.Status, testCase.RelativePath);
            specialized.Code.ShouldBe(original.Code, testCase.RelativePath);
        }
    }

    /// <summary>
    /// O residual continua sendo um programa: reparseia e passa no type checker.
    /// Sem isto, <c>lapis pe</c> emitiria texto que não é LapisLang.
    /// </summary>
    [Fact]
    public void Residual_IsReparseableAndWellTyped()
    {
        foreach (var (testCase, compiled) in Compilable)
        {
            var residual = CoreSourcePrinter.Print(
                PartialEvaluator.PartialEvaluator.Specialize(
                    compiled.Core!, types: compiled.Typed).Residual);

            var recompiled = Pipeline.Compile(
                SourceText.From(residual, testCase.RelativePath), PipelineStage.TypeCheck);

            recompiled.HasErrors.ShouldBeFalse(
                $"{testCase.RelativePath}: o residual não compila:\n{residual}\n"
                + string.Join(", ", recompiled.Diagnostics.Select(d => $"{d.Code} {d.Message}")));
        }
    }

    /// <summary><c>PE(PE(P)) == PE(P)</c>: a segunda passada não acha mais nada.</summary>
    [Fact]
    public void Idempotent_OverTheWholeCorpus()
    {
        foreach (var (testCase, compiled) in Compilable)
        {
            var once = CoreSourcePrinter.Print(
                PartialEvaluator.PartialEvaluator.Specialize(
                    compiled.Core!, types: compiled.Typed).Residual);

            var recompiled = Pipeline.Compile(
                SourceText.From(once, testCase.RelativePath), PipelineStage.TypeCheck);

            if (recompiled.HasErrors)
            {
                continue;
            }

            var twice = CoreSourcePrinter.Print(
                PartialEvaluator.PartialEvaluator.Specialize(
                    recompiled.Core!, types: recompiled.Typed).Residual);

            twice.ShouldBe(once, $"{testCase.RelativePath}: o PE não é idempotente");
        }
    }

    /// <summary>
    /// O PE nunca lança sobre programa bem-tipado: um defeito dele tem que virar
    /// residual conservador, nunca exceção.
    /// </summary>
    [Fact]
    public void NeverThrows_OverTheWholeCorpus()
    {
        foreach (var (testCase, compiled) in Compilable)
        {
            Should.NotThrow(
                () => PartialEvaluator.PartialEvaluator.Specialize(compiled.Core!, types: compiled.Typed),
                testCase.RelativePath);
        }
    }

    /// <summary>
    /// Sem inlining o residual não cresce — e o fator de crescimento existe para
    /// que o dia em que crescer (plano 13) seja um teste que falha, não uma
    /// surpresa.
    /// </summary>
    [Fact]
    public void Residual_DoesNotGrow()
    {
        var options = new PEOptions();

        foreach (var (testCase, compiled) in Compilable)
        {
            var statistics = PartialEvaluator.PartialEvaluator
                .Specialize(compiled.Core!, options: options, types: compiled.Typed)
                .Statistics;

            statistics.NodesAfter.ShouldBeLessThanOrEqualTo(
                statistics.NodesBefore * options.MaxResidualGrowthFactor, testCase.RelativePath);
        }
    }

    /// <summary>
    /// Num programa sem nada conhecido o PE não dobra nem elimina ramo: não há o
    /// que decidir. O que ele ainda faz — apagar o que vem depois de um
    /// <c>return</c> — é eliminação de código inalcançável, não especialização.
    /// </summary>
    [Fact]
    public void FullyDynamicProgram_HasNothingToFold()
    {
        const string Source = """
            def f = fn(a: Int, b: Int) Int {
                if a < b { return a; }
                return b;
            };

            def g = fn(v: Int) Int { return f(v, v + 1); };

            print(g(2));
            """;

        var compiled = Pipeline.Compile(SourceText.From(Source, "dinamico.ls"), PipelineStage.TypeCheck);
        var residual = PartialEvaluator.PartialEvaluator.Specialize(compiled.Core!, types: compiled.Typed);

        residual.Statistics.ConstantsFolded.ShouldBe(0);
        residual.Statistics.BranchesEliminated.ShouldBe(0);

        Execute(CoreSourcePrinter.Print(residual.Residual), "residual.ls")
            .Output.ShouldBe(Execute(Source, "dinamico.ls").Output);
    }

    private static Run Execute(string source, string name)
    {
        var output = new StringOutput();
        var result = Pipeline.Compile(
            SourceText.From(source, name), PipelineStage.Evaluate, new RuntimeContext(output));

        result.HasErrors.ShouldBeFalse(
            $"{name}: {string.Join(", ", result.Diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        return new Run(output.Text, result.Evaluation!.Status, result.Evaluation.Code);
    }

    private sealed record Run(string Output, ExecutionStatus Status, string? Code);
}

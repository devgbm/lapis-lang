using Lapis.Ast.Core;
using Lapis.Ast.Printing;
using Lapis.Ast.Types;
using Lapis.Cli;
using Lapis.Diagnostics;
using Lapis.Evaluator;
using Lapis.Runtime;

namespace Lapis.PartialEvaluator.Tests;

/// <summary>
/// O prelude carregado uma vez para toda a suíte.
/// </summary>
public static class PreludeFixture
{
    public static PreludeScope Scope { get; } = PreludeLoader.Load();
}

/// <summary>
/// O protocolo de teste da spec §53: não basta o residual ser o esperado — os dois
/// programas precisam <b>rodar igual</b>.
///
/// Um PE que produz o residual bonito e muda o comportamento não está otimizando,
/// está quebrando o programa; é a diferença que a spec §40 chama de equivalência,
/// e é o que estas asserções travam.
/// </summary>
public abstract class PETestBase
{
    protected sealed record Run(string Output, ExecutionStatus Status, string? Code);

    /// <summary>
    /// Só até a Core, sem type checker. É o que permite examinar expressões com
    /// nomes que não existem — o PE roda sobre a Core, e a tabela de tipos é
    /// opcional para ele.
    /// </summary>
    protected static CoreProgram CompileToCore(string source)
    {
        var result = Pipeline.Compile(SourceText.From(source, "pe.ls"), PipelineStage.Desugar);

        result.HasErrors.ShouldBeFalse(
            $"erro de parse: {string.Join(", ", result.Diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        return result.Core!;
    }

    /// <summary>Compila até a Core, com a tabela de tipos.</summary>
    protected static (CoreProgram Core, Ast.Typed.TypedProgram? Types) Compile(string source)
    {
        var result = Pipeline.Compile(SourceText.From(source, "pe.ls"), PipelineStage.TypeCheck);

        result.HasErrors.ShouldBeFalse(
            $"erro de compilação: {string.Join(", ", result.Diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        return (result.Core!, result.Typed);
    }

    protected static PartialEvaluationResult Evaluate(
        string source,
        StaticEnvironment? environment = null,
        PEOptions? options = null)
    {
        var (core, types) = Compile(source);
        return PartialEvaluator.Specialize(core, environment, options, types);
    }

    /// <summary>O residual impresso como código <c>.ls</c>, sem a quebra final.</summary>
    protected static string Residual(string source, StaticEnvironment? environment = null) =>
        CoreSourcePrinter.Print(Evaluate(source, environment).Residual).TrimEnd('\n');

    /// <summary>
    /// A bateria completa: residual esperado, equivalência de execução,
    /// idempotência, e residual re-tipável e reparseável.
    /// </summary>
    protected static void ShouldSpecializeTo(string source, string expectedResidual)
    {
        var residual = Residual(source);

        Normalize(residual).ShouldBe(Normalize(expectedResidual), $"residual obtido:\n{residual}");

        ShouldPreserveBehaviour(source);
        ShouldBeIdempotent(source);
        ShouldStillTypeCheck(source);
    }

    /// <summary><c>evaluate(P) ≡ evaluate(PE(P))</c> — valor, saída e desfecho (spec §40).</summary>
    protected static void ShouldPreserveBehaviour(string source)
    {
        var original = Execute(source);
        var residual = Execute(Residual(source));

        residual.Output.ShouldBe(original.Output, "a saída do residual diverge do original");
        residual.Status.ShouldBe(original.Status);
        residual.Code.ShouldBe(original.Code);
    }

    /// <summary><c>PE(PE(P)) == PE(P)</c>: a segunda passada não acha mais nada.</summary>
    protected static void ShouldBeIdempotent(string source)
    {
        var once = Residual(source);
        var twice = Residual(once);

        twice.ShouldBe(once, "o PE não é idempotente");
    }

    /// <summary>O residual continua sendo um programa válido — reparseável e bem-tipado.</summary>
    protected static void ShouldStillTypeCheck(string source)
    {
        var residual = Residual(source);
        var result = Pipeline.Compile(SourceText.From(residual, "residual.ls"), PipelineStage.TypeCheck);

        result.HasErrors.ShouldBeFalse(
            $"o residual não compila:\n{residual}\n"
            + string.Join(", ", result.Diagnostics.Select(d => $"{d.Code} {d.Message}")));
    }

    protected static Run Execute(string source)
    {
        var output = new StringOutput();
        var result = Pipeline.Compile(
            SourceText.From(source, "exec.ls"), PipelineStage.Evaluate, new RuntimeContext(output));

        result.HasErrors.ShouldBeFalse(
            $"não executou: {string.Join(", ", result.Diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        return new Run(output.Text, result.Evaluation!.Status, result.Evaluation.Code);
    }

    /// <summary>Um ambiente estático com nomes declarados como desconhecidos.</summary>
    protected static StaticEnvironment WithDynamic(params string[] names)
    {
        var environment = StaticEnvironment.Root();

        foreach (var name in names)
        {
            environment.Declare(name, new PEBinding(new Unknown(AnyType.Instance)));
        }

        return environment;
    }

    /// <summary>Colapsa espaço em branco, para que os testes possam ser escritos numa linha.</summary>
    private static string Normalize(string text) =>
        string.Join(" ", text.Split((char[])['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries));
}

namespace Lapis.PartialEvaluator.Tests;

/// <summary>
/// <c>xs[i] = v</c> no PE (Q36).
///
/// A atribuição sobrevive à especialização, como toda atribuição: um <c>var</c>
/// já é desconhecido para o PE de hoje (plano 12). O que <b>é</b> novo é o
/// índice — expressão dentro do caminho, que precisa ser especializada e contada
/// como uso, sob pena de o PE eliminar a definição que ela cita.
/// </summary>
public sealed class SpanAssignmentFoldingTests : PETestBase
{
    [Fact]
    public void Write_SurvivesSpecialization() =>
        ShouldSpecializeTo("var xs = .[0, 1, 2];\nxs[0] = 9;\nprint(xs);",
            "var xs = .[0, 1, 2]; xs[0] = 9; print(xs);");

    /// <summary>O índice é especializado como qualquer expressão.</summary>
    [Fact]
    public void Write_SpecializesTheIndex() =>
        ShouldSpecializeTo("var xs = .[0, 1, 2];\ndef i = 1;\nxs[i + 1] = 9;\nprint(xs);",
            "var xs = .[0, 1, 2]; xs[2] = 9; print(xs);");

    /// <summary>
    /// Regressão do erro mais provável desta mudança: se o índice não contasse
    /// como uso, o <c>Let</c> que o define viraria código morto e o residual não
    /// compilaria.
    /// </summary>
    [Fact]
    public void Write_KeepsTheBindingTheIndexUses() =>
        Residual("var i = 1;\nvar xs = .[0, 1, 2];\nxs[i] = 9;\nprint(xs);").ShouldContain("var i = 1");

    [Fact]
    public void Write_PreservesBehaviour() =>
        ShouldPreserveBehaviour("var xs = .[0, 1, 2];\ndef i = 1;\nxs[i] = 9;\nxs[99] = 7;\nprint(xs);");

    [Fact]
    public void Write_IsIdempotent() =>
        ShouldBeIdempotent("var xs = .[0, 1, 2];\nxs[1] = 9;\nprint(xs);");

    [Fact]
    public void Write_StillTypeChecks() =>
        ShouldStillTypeCheck("var xs = .[0, 1, 2];\ndef i = 1;\nxs[i] = 9;\nprint(xs);");

    [Fact]
    public void Write_ThroughAFieldPath_RoundTrips() =>
        ShouldPreserveBehaviour(
            """
            def Caixa = type {
                xs: [Int;?];
            };

            var c = .Caixa { xs: .[1, 2, 3] };
            c.xs[1] = 20;
            print(c);
            """);
}

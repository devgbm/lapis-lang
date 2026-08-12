namespace Lapis.PartialEvaluator.Tests;

/// <summary>
/// <c>is</c> atravessa o PE intacto (plano 25, M16), e por um motivo que não é
/// dele: o especializador nunca chega a <b>conhecer</b> um valor de enum —
/// construir uma variante é chamada, e chamada é impura por conservadorismo. É a
/// mesma parede que <c>match</c> encontra, e derrubá-la é trabalho do M17–M19.
///
/// O que estes casos travam é o que <b>é</b> garantido hoje, pelo protocolo da
/// spec §53: o residual roda igual, continua tipando e é ponto fixo do PE.
/// </summary>
public sealed class IsFoldingTests : PETestBase
{
    [Fact]
    public void Bind_SurvivesSpecialization() =>
        ShouldSpecializeTo(
            "def e = Option<Int>.Some(1);\nif e is Some(v) { print(v); } else { print(0); }",
            """
            def e = Option<Int>.Some(1);
            if e is Some(v) {
                print(v);
            } else {
                print(0);
            };
            """);

    /// <summary>A forma sem ligação volta na forma curta — é ela que reparseia para o mesmo nó.</summary>
    [Fact]
    public void NoBinding_SurvivesSpecialization() =>
        ShouldSpecializeTo(
            "def e = Option<Int>.Some(1);\nprint(e is Some);",
            "def e = Option<Int>.Some(1); print(e is Some);");

    /// <summary>
    /// O escrutinado <b>é</b> especializado, ainda que o teste sobreviva: o que
    /// dá para decidir dentro dele é decidido.
    /// </summary>
    [Fact]
    public void Scrutinee_IsStillSpecialized() =>
        Residual("def f = fn(o: Option<Int>) Bool { return o is Some; };\nprint(f(Option<Int>.None));")
            .ShouldContain("o is Some");
}

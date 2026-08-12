namespace Lapis.PartialEvaluator.Tests;

/// <summary>
/// <c>is</c> não precisa de nada novo no PE (plano 25, M16 — fecha Q23): ele já
/// é <c>match</c> quando chega na Core (§25.2), então o especializador o vê
/// exatamente como vê qualquer outro <c>match</c> — inclusive a limitação atual
/// de não dobrar o braço escolhido mesmo com escrutinado conhecido (<c>Match</c>
/// segue primitivo na Core; ver <c>CoreNodes.cs</c>). O que este arquivo prova
/// é o que <b>é</b> garantido: o residual continua tipando, rodando igual e
/// sendo ponto fixo do PE — o protocolo da spec §53 sobre a forma que <c>is</c>
/// produz.
/// </summary>
public sealed class IsFoldingTests : PETestBase
{
    [Fact]
    public void Bind_SurvivesSpecialization() =>
        ShouldSpecializeTo(
            "def e = Option<Int>.Some(1);\nif e is Some(v) { print(v); } else { print(0); }",
            """
            def e = Option<Int>.Some(1);
            match e {
                Option.Some(v) => { print(v); },
                _ => { print(0); }
            };
            """);

    [Fact]
    public void NoBinding_SurvivesSpecialization() =>
        ShouldSpecializeTo(
            "def e = Option<Int>.Some(1);\nprint(e is Some);",
            """
            def e = Option<Int>.Some(1);
            print(match e { Option.Some(_) => true, _ => false });
            """);
}

namespace Lapis.Conformance.Tests;

/// <summary>
/// A suíte de conformidade: cada arquivo em <c>tests/conformance/</c> é um teste.
///
/// É a especificação executável do projeto — cada afirmação testável da spec vira
/// um arquivo, e o nome do teste que falha já é o arquivo a abrir (plano 11).
/// </summary>
public sealed class ConformanceTests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public void Case(ConformanceCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        var failure = ConformanceRunner.Verify(testCase, ConformanceRunner.Execute(testCase));

        if (testCase.SkipReason is null)
        {
            failure.ShouldBeNull($"{testCase.RelativePath}\n{failure}");
            return;
        }

        // Um `skip:` que voltou a passar é dívida esquecida: falhar aqui é o que
        // força a reativação em vez de deixar o caso adormecido para sempre.
        failure.ShouldNotBeNull(
            $"{testCase.RelativePath}: o caso passou, mas está marcado "
            + $"'skip: {testCase.SkipReason}'. Remova a diretiva.");
    }

    public static TheoryData<ConformanceCase> Cases() => ConformanceCorpus.Cases();

    /// <summary>Um caso sem diretiva nenhuma não afirma nada e passaria em silêncio.</summary>
    [Fact]
    public void EveryCase_DeclaresAnExpectation()
    {
        foreach (var testCase in ConformanceCorpus.All)
        {
            testCase.HasExpectations.ShouldBeTrue(
                $"{testCase.RelativePath} não declara nenhum '// expect:'");
        }
    }

    [Fact]
    public void Corpus_IsNotEmpty() => ConformanceCorpus.All.ShouldNotBeEmpty();

    /// <summary>
    /// O prelude é compartilhado entre casos por velocidade. Este teste refaz o
    /// corpus inteiro com um prelude fresco: se algum estado atravessasse
    /// compilações, os resultados divergiriam.
    /// </summary>
    [Fact]
    public void SharedPrelude_MatchesFreshPrelude()
    {
        foreach (var testCase in ConformanceCorpus.All.Where(c => c.SkipReason is null))
        {
            var shared = ConformanceRunner.Execute(testCase);
            var fresh = ConformanceRunner.Execute(testCase, freshPrelude: true);

            fresh.Output.ShouldBe(shared.Output, testCase.RelativePath);
            fresh.ExitCode.ShouldBe(shared.ExitCode, testCase.RelativePath);
        }
    }
}

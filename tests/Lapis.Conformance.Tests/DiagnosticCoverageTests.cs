using System.Reflection;
using Lapis.Diagnostics;

namespace Lapis.Conformance.Tests;

/// <summary>
/// Todo código publicado precisa de um caso que o produza (plano 11 §11.3).
///
/// Sem isto, um código pode ser declarado e nunca emitido — ou deixar de ser
/// emitido numa refatoração — sem que nada quebre. As exceções são poucas e
/// nomeadas, para que "não tem caso" seja sempre uma decisão, nunca um descuido.
/// </summary>
public sealed class DiagnosticCoverageTests
{
    /// <summary>
    /// Códigos que existem no catálogo mas que nenhum caso pode produzir, com o
    /// motivo. Um código só entra aqui com justificativa verificável.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        [DiagnosticCodes.UnusedBinding] = "reservado: warning ainda não emitido",
        [DiagnosticCodes.CallDepthExceeded] =
            "inalcançável sem recursão (spec §8) — ver eval/functions/call_depth_limit.ls",
    };

    /// <summary>Códigos que os casos afirmam esperar, ignorando os `skip:`.</summary>
    private static HashSet<string> Covered =>
        ConformanceCorpus.All
            .Where(c => c.SkipReason is null)
            .SelectMany(c => c.ExpectedDiagnostics)
            .Select(d => d.Code)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> Catalogue =>
        typeof(DiagnosticCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void EveryDiagnosticCode_HasACase()
    {
        var covered = Covered;
        var missing = Catalogue.Where(c => !covered.Contains(c) && !Exempt.ContainsKey(c)).Order().ToList();

        missing.ShouldBeEmpty(
            "códigos sem caso de conformidade: " + string.Join(", ", missing)
            + ". Escreva um caso ou registre a exceção em DiagnosticCoverageTests.Exempt.");
    }

    /// <summary>
    /// A isenção é dívida: se o código passou a ter caso, ela precisa sair, senão
    /// a lista vira ficção que ninguém revisa.
    /// </summary>
    [Fact]
    public void ExemptCodes_AreStillUncovered()
    {
        var covered = Covered;

        foreach (var (code, reason) in Exempt)
        {
            covered.ShouldNotContain(
                code, $"{code} já tem caso ('{reason}' deixou de valer): remova a isenção.");
        }
    }

    /// <summary>Uma isenção de código que nem existe no catálogo é lixo acumulado.</summary>
    [Fact]
    public void ExemptCodes_ExistInTheCatalogue()
    {
        var catalogue = Catalogue.ToHashSet(StringComparer.Ordinal);

        foreach (var code in Exempt.Keys)
        {
            catalogue.ShouldContain(code);
        }
    }

    /// <summary>
    /// Nenhum caso espera um código que o catálogo não conhece: erro de digitação
    /// numa diretiva faria o caso exigir algo impossível — e ele passaria a
    /// falhar por um motivo diferente do que o autor escreveu.
    /// </summary>
    [Fact]
    public void EveryExpectedCode_IsInTheCatalogue()
    {
        var catalogue = Catalogue.ToHashSet(StringComparer.Ordinal);

        foreach (var testCase in ConformanceCorpus.All)
        {
            foreach (var expected in testCase.ExpectedDiagnostics)
            {
                catalogue.ShouldContain(expected.Code, testCase.RelativePath);
            }
        }
    }
}

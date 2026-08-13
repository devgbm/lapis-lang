namespace Lapis.PartialEvaluator.Tests;

/// <summary>
/// <c>Str.length</c> e <c>s[i]</c> no PE (Q35/A2a, plano 27 §A2).
///
/// <c>length</c> dobra com a string em mãos, pela mesma regra de
/// <c>[T;N].length</c> — a diferença é que aqui o tipo nunca ajuda: <c>Str</c>
/// não carrega tamanho, então o valor é a única fonte.
///
/// A indexação <b>não</b> dobra, e não por falta de vontade: o resultado é um
/// <c>Option&lt;Char&gt;</c>, e nem enum construído nem <c>Char</c> têm forma
/// escrita para voltar ao residual. Emitir seria produzir programa que não
/// reparseia; deixar passar é sempre correto.
/// </summary>
public sealed class StrIntrinsicFoldingTests : PETestBase
{
    [Fact]
    public void Length_OfKnownString_Folds() =>
        ShouldSpecializeTo("def s = \"abc\";\nprint(s.length);", "print(3);");

    /// <summary>A contagem do PE é a do evaluator: pontos de código, não unidades UTF-16.</summary>
    [Fact]
    public void Length_OfAstral_FoldsToCodepointCount() =>
        ShouldSpecializeTo("def s = \"a𝕏b\";\nprint(s.length);", "print(3);");

    [Fact]
    public void Length_OfDynamicString_Survives() =>
        Residual("var s = \"abc\";\nprint(s.length);").ShouldContain("s.length");

    /// <summary>
    /// Sem o valor não há contagem: o alvo pode ter efeito, e descartá-lo para
    /// ler o tamanho descartaria o efeito junto.
    /// </summary>
    [Fact]
    public void Index_DoesNotFold() =>
        Residual("def s = \"abc\";\nprint(s[0]);").ShouldContain("[0]");

    [Fact]
    public void Length_PreservesBehaviour() =>
        ShouldPreserveBehaviour("def s = \"a𝕏b\";\nprint(s.length);\nprint(s[1]);");

    [Fact]
    public void Length_IsIdempotent() => ShouldBeIdempotent("def s = \"abc\";\nprint(s.length);");

    [Fact]
    public void Index_StillTypeChecks() =>
        ShouldStillTypeCheck("def s = \"abc\";\nif s[0] is Some(c) { print(c); }");
}

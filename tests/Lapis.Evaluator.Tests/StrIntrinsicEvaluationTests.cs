namespace Lapis.Evaluator.Tests;

/// <summary>
/// <c>Str.length</c> e <c>s[i]</c> em execução (Q35/A2a, plano 27 §A2).
///
/// A unidade é o <b>ponto de código</b>. Não é detalhe de implementação: com
/// unidade UTF-16 estes mesmos programas dariam respostas diferentes para texto
/// fora do plano básico, e sem erro nenhum — é a razão pela qual a A2a fixou a
/// unidade antes de existir qualquer biblioteca que dependa dela.
/// </summary>
public sealed class StrIntrinsicEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void Length_CountsCodepoints() => Eval("\"abc\".length").ShouldBe("3");

    [Fact]
    public void Length_OfEmpty_IsZero() => Eval("\"\".length").ShouldBe("0");

    /// <summary><c>𝕏</c> ocupa duas unidades UTF-16; conta como um.</summary>
    [Fact]
    public void Length_OfAstral_CountsOnePerCodepoint() => Eval("\"a𝕏b\".length").ShouldBe("3");

    [Fact]
    public void Index_InBounds_IsSome() => Eval("\"abc\"[1]").ShouldBe("Option.Some(b)");

    [Fact]
    public void Index_PastTheEnd_IsNone() => Eval("\"abc\"[3]").ShouldBe("Option.None");

    [Fact]
    public void Index_Negative_IsNone() => Eval("\"abc\"[0 - 1]").ShouldBe("Option.None");

    [Fact]
    public void Index_OfEmpty_IsNone() => Eval("\"\"[0]").ShouldBe("Option.None");

    /// <summary>
    /// O caractere astral sai <b>inteiro</b>. Com <c>char</c> de C# no lugar de
    /// <c>Rune</c>, esta posição devolveria a primeira metade do par substituto:
    /// um valor que não é caractere nenhum e que nada acusaria.
    /// </summary>
    [Fact]
    public void Index_OfAstral_ReturnsTheWholeCodepoint() =>
        Output("if \"a𝕏b\"[1] is Some(c) { print(c); }").ShouldBe("𝕏\n");

    /// <summary>
    /// Um <c>Char</c> impresso sai nu, mesmo aninhado — é o que o distingue de
    /// um <c>Str</c> de um caractere, que sairia entre aspas.
    /// </summary>
    [Fact]
    public void Char_PrintsWithoutQuotes() =>
        Output("if \"a\"[0] is Some(c) { print(c); }\nprint(\"a\"[0]);\nprint(.[\"a\"]);")
            .ShouldBe("a\nOption.Some(a)\n[\"a\"]\n");

    [Fact]
    public void Char_EqualityComparesCodepoints() =>
        Output(
            """
            def iguais = fn(a: Str, b: Str) Bool {
                if a[0] is Some(x) {
                    if b[0] is Some(y) {
                        return x == y;
                    }
                }

                return false;
            };

            print(iguais("abc", "axc"));
            print(iguais("abc", "xbc"));
            """)
            .ShouldBe("true\nfalse\n");

    // ------------------------------------------------- literal de Char (Q35)

    [Fact]
    public void CharLiteral_Evaluates() => Eval("'a'").ShouldBe("a");

    [Fact]
    public void CharLiteral_Astral() => Eval("'𝕏'").ShouldBe("𝕏");

    /// <summary>
    /// Escapa-se a aspa que delimita, e só ela — <c>'"'</c> dispensa barra pelo
    /// mesmo motivo que <c>"'"</c> dispensa.
    /// </summary>
    [Fact]
    public void CharLiteral_Quotes()
    {
        Eval("'\\''").ShouldBe("'");
        Eval("'\"'").ShouldBe("\"");
    }

    [Fact]
    public void CharLiteral_EqualsAnIndexedChar() =>
        Output("if \"abc\"[1] is Some(c) { print(c == 'b'); print(c == 'z'); }")
            .ShouldBe("true\nfalse\n");

    /// <summary>
    /// O caractere literal e o indexado são o <b>mesmo</b> valor mesmo fora do
    /// plano básico: se o literal fosse lido por unidade UTF-16 e a indexação
    /// por ponto de código, isto daria <c>false</c> sem nada acusar.
    /// </summary>
    [Fact]
    public void CharLiteral_AgreesWithIndexingOnAstralText() =>
        Output("if \"a𝕏b\"[1] is Some(c) { print(c == '𝕏'); }").ShouldBe("true\n");

    /// <summary>
    /// <c>length</c> e indexação falam da mesma unidade: percorrer
    /// <c>0..length</c> visita todo caractere e nenhuma posição inválida. Se as
    /// duas divergissem, este laço acabaria em <c>None</c> — é a propriedade que
    /// amarra as duas leituras uma à outra.
    /// </summary>
    [Fact]
    public void LengthAndIndex_AgreeOnTheUnit() =>
        Output(
            """
            def texto = "a𝕏b";
            var i = 0;

            loop {
                if i >= texto.length { break; }

                if texto[i] is Some(c) { print(c); } else { print("!"); }

                i = i + 1;
            }
            """)
            .ShouldBe("a\n𝕏\nb\n");
}

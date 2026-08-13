using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

/// <summary>
/// <c>Str.length</c> e <c>s[i]</c> (Q35/A2a, plano 27 §A2).
///
/// <c>Str</c> não virou <c>[Char;N]</c>: ganhou as duas leituras que um span
/// oferece, com a mesma forma de resposta, sem mudar de representação. O que
/// separa os dois regimes é o tamanho no tipo — um <c>[T;N]</c> tem N e por isso
/// admite indexação total; <c>Str</c> não tem, então a indexação é sempre
/// <c>Option&lt;Char&gt;</c>, como em <c>[T;?]</c>.
/// </summary>
public sealed class StrIntrinsicTests : TypeCheckerTestBase
{
    [Fact]
    public void Length_IsInt() =>
        TypeOfDef("def n = \"abc\".length;", "n").ShouldBe(PrimitiveType.Int);

    /// <summary>
    /// Nunca constante, ao contrário de <c>[T;N].length</c>: o tipo <c>Str</c>
    /// não carrega tamanho. Dobrar isso é possível só com a string em mãos, e é
    /// o partial evaluator que faz — não o checker.
    /// </summary>
    [Fact]
    public void Length_OfVariable_IsAlsoInt() =>
        TypeOfDef("var s = \"abc\";\ndef n = s.length;", "n").ShouldBe(PrimitiveType.Int);

    [Fact]
    public void Index_IsOptionChar()
    {
        var type = TypeOfDef("def c = \"abc\"[0];", "c");

        type.ToDisplayString().ShouldBe("Option<Char>");
    }

    /// <summary>
    /// Índice literal dentro do texto <b>não</b> torna a leitura total: não
    /// existe N no tipo contra o qual provar, então nem <c>LAP0244</c> aparece
    /// para um índice grande — ele é apenas <c>None</c> em execução.
    /// </summary>
    [Fact]
    public void Index_BeyondTheText_IsNotACompileError() =>
        ShouldPass("def c = \"abc\"[99];");

    [Fact]
    public void Index_MustBeInt() =>
        ShouldFailWith("def c = \"abc\"[\"x\"];", DiagnosticCodes.IndexMustBeInt);

    /// <summary><c>Char</c> é nome de tipo escrevível, como qualquer primitivo.</summary>
    [Fact]
    public void Char_IsAWritableTypeName() =>
        TypeOfDef("def c: Option<Char> = \"a\"[0];", "c").ToDisplayString().ShouldBe("Option<Char>");

    /// <summary>
    /// O que <c>is</c> desembrulha de um <c>Option&lt;Char&gt;</c> é um
    /// <c>Char</c> — o caminho pelo qual um caractere chega a uma função, já que
    /// não há literal de <c>Char</c> (Q38 reserva as aspas simples).
    /// </summary>
    [Fact]
    public void Char_UnwrappedByIs_IsChar() =>
        ShouldPass(
            """
            def usa = fn(c: Char) Int { return 1; };

            if "a"[0] is Some(x) { print(usa(x)); }
            """);

    /// <summary>
    /// <c>Char</c> e <c>Str</c> são tipos distintos: um caractere não é uma
    /// string de um caractere. Confundi-los é justamente o que a A2b evitaria de
    /// outro jeito, e o que a A2a evita mantendo os dois separados.
    /// </summary>
    [Fact]
    public void Char_IsNotStr() =>
        ShouldFailWith(
            """
            def usa = fn(s: Str) Int { return 1; };

            if "a"[0] is Some(x) { print(usa(x)); }
            """,
            DiagnosticCodes.ArgumentTypeMismatch);

    [Fact]
    public void Str_HasNoOtherMember() =>
        Codes("def x = \"abc\".size;").ShouldNotBeEmpty();
}

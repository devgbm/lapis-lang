using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

/// <summary>
/// O literal de <c>Char</c> (Q35) e a regra que só o parser pode cobrar.
///
/// A lexer aceita qualquer conteúdo entre aspas simples porque as mesmas aspas
/// servirão à pseudo-palavra-chave de macro (Q38), que carrega um identificador
/// inteiro. É aqui, em posição de expressão, que "exatamente um ponto de código"
/// vira <c>LAP0117</c>.
/// </summary>
public sealed class CharLiteralTests : ParserTestBase
{
    [Fact]
    public void Char_Parses() => PrintExpression("'a';").ShouldBe("(char 'a')");

    [Fact]
    public void Char_Astral_IsOneCodepoint() => PrintExpression("'𝕏';").ShouldBe("(char '𝕏')");

    /// <summary>A aspa que delimita volta escapada; a outra, não.</summary>
    [Fact]
    public void Char_QuotesRoundTripThroughThePrinter()
    {
        PrintExpression("'\\'';").ShouldBe("(char '\\'')");
        PrintExpression("'\"';").ShouldBe("(char '\"')");
    }

    [Fact]
    public void Char_EscapesRoundTrip()
    {
        PrintExpression("'\\n';").ShouldBe("(char '\\n')");
        PrintExpression("'\\\\';").ShouldBe("(char '\\\\')");
    }

    [Fact]
    public void Char_MoreThanOneCodepoint_ReportsLap0117() =>
        Codes("def c = 'ab';").ShouldContain(DiagnosticCodes.CharLiteralMustBeOneCodepoint);

    [Fact]
    public void Char_Empty_ReportsLap0117() =>
        Codes("def c = '';").ShouldContain(DiagnosticCodes.CharLiteralMustBeOneCodepoint);

    /// <summary>
    /// Um literal malformado não cascateia: o parser devolve um <c>Char</c>
    /// qualquer e segue, então o erro seguinte no arquivo continua sendo visto.
    /// </summary>
    [Fact]
    public void Char_Malformed_DoesNotCascade() =>
        Codes("def c = 'ab';\ndef d = 'cd';")
            .ShouldBe([
                DiagnosticCodes.CharLiteralMustBeOneCodepoint,
                DiagnosticCodes.CharLiteralMustBeOneCodepoint,
            ]);

    [Fact]
    public void Char_IsUsableWhereverAnExpressionIs() =>
        Codes("def f = fn(c: Char) Bool { return c == 'a'; };\nprint(f('a'));").ShouldBeEmpty();
}

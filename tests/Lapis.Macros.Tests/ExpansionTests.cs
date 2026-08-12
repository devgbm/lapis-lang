using Lapis.Diagnostics;

namespace Lapis.Macros.Tests;

/// <summary>Matching, expansão e higiene (plano 17 §17.5–§17.8).</summary>
public sealed class ExpansionTests : MacroTestBase
{
    private const string Log = "macro log match Expression:e expand { print(e); };\n";
    private const string Square = "macro square match Expression:e expand { (e * e) };\n";

    // ------------------------------------------------------------ matching

    /// <summary>
    /// A captura usa a gramática de expressão normal, que já para nos lugares
    /// certos: <c>+</c> continua a expressão, então <c>x + 1</c> vem inteiro.
    /// </summary>
    [Fact]
    public void Match_ExpressionCapture_TakesTheWholeExpression() =>
        ShouldExpandTo(Log + "@log 1 + 2;", "print(1 + 2);");

    [Fact]
    public void Match_BlockCapture() =>
        ShouldExpandTo(
            "macro run match Expression:c Block:b expand { if c { b; } };\n@run true { print(1); }",
            "if true { print(1); }");

    [Fact]
    public void Match_LiteralToken() =>
        ShouldExpandTo(
            "macro each match Identifier:i in Expression:xs Block:b expand { def i = xs; b; };\n"
            + "@each item in 1 { print(item); }",
            "def item = 1;\nprint(item);");

    [Fact]
    public void Match_LiteralToken_Wrong() =>
        ShouldFailWith(
            "macro each match Identifier:i in Expression:xs Block:b expand { b; };\n"
            + "@each item from 1 { print(1); }",
            DiagnosticCodes.NoMacroRuleMatches);

    [Fact]
    public void Match_WrongCategory() =>
        ShouldFailWith(
            "macro inc match Identifier:n expand { print(n); };\n@inc 10;",
            DiagnosticCodes.NoMacroRuleMatches);

    /// <summary>Sobra de tokens é falha: a macro tem de explicar a invocação inteira.</summary>
    [Fact]
    public void Match_LeftoverTokens() =>
        ShouldFailWith(
            "macro one match Identifier:a expand { print(a); };\n@one x y;",
            DiagnosticCodes.NoMacroRuleMatches);

    /// <summary>
    /// Todas as regras são testadas, e o resultado tem de ser exatamente uma.
    /// Falhar ruidosamente é o que a 0.2 já faz com <c>a &lt; b &lt; c</c>.
    /// </summary>
    [Fact]
    public void Match_Ambiguous() =>
        ShouldFailWith(
            """
            macro foo
                match Expression:e expand { print(e); }
                match Identifier:x expand { print(x); };

            @foo x;
            """,
            DiagnosticCodes.AmbiguousMacroRules);

    /// <summary>Uma regra que não casa não reporta nada: ela é só a próxima regra.</summary>
    [Fact]
    public void Match_FailedAttempt_ReportsNothing() =>
        ExpansionCodes(
            """
            macro foo
                match Identifier:x Block:b expand { b; }
                match Expression:e expand { print(e); };

            @foo 1 + 2;
            """).ShouldBeEmpty();

    [Fact]
    public void Match_SelectsTheRuleThatFits() =>
        ShouldExpandTo(
            """
            macro foo
                match Identifier:x Block:b expand { def x = 1; b; }
                match Expression:e expand { print(e); };

            @foo 1 + 2;
            """,
            "print(1 + 2);");

    [Theory]
    [InlineData("Int:v", "1", "1")]
    [InlineData("Str:v", "\"a\"", "\"a\"")]
    [InlineData("Bool:v", "true", "true")]
    [InlineData("Literal:v", "1.5", "1.5")]
    public void Match_TypedLiteralCaptures(string pattern, string argument, string expected) =>
        ShouldExpandTo(
            $"macro lit match {pattern} expand {{ print(v); }};\n@lit {argument};",
            $"print({expected});");

    [Fact]
    public void Match_TypedLiteral_Wrong() =>
        ShouldFailWith(
            "macro lit match Str:v expand { print(v); };\n@lit 1;",
            DiagnosticCodes.NoMacroRuleMatches);

    // ------------------------------------------------------------ expansão

    [Fact]
    public void Expand_InStatementPosition() =>
        ShouldExpandTo(Log + "@log 30;", "print(30);");

    /// <summary>
    /// Os parênteses do <c>expand</c> são responsabilidade de quem escreve a
    /// macro: a árvore capturada entra como árvore, e <c>(e * e)</c> já é a árvore
    /// certa.
    /// </summary>
    [Fact]
    public void Expand_InExpressionPosition() =>
        ShouldExpandTo(Square + "def r = @square x + 1;", "def r = ((x + 1) * (x + 1));");

    [Fact]
    public void Expand_SeveralStatements() =>
        ShouldExpandTo(
            "macro two match Expression:e expand { print(e); print(e); };\n@two 1;",
            "print(1);\nprint(1);");

    /// <summary>Um `expand` que não produz valor não serve em posição de expressão.</summary>
    [Fact]
    public void Expand_WrongContext() =>
        ShouldFailWith(
            "macro noop match Expression:e expand { print(e); };\ndef r = @noop 1;",
            DiagnosticCodes.MacroExpansionWrongContext);

    /// <summary>
    /// Uma macro pode expandir para código que usa outras macros — e a captura
    /// atravessa a invocação aninhada, porque cada captura guarda também os
    /// tokens que consumiu.
    ///
    /// Os parênteses em volta de cada <c>@square</c> não são estilo: a invocação é
    /// **gulosa** até o delimitador de nível 0, então <c>@square e * @square e</c>
    /// seria uma invocação só, com <c>e * @square e</c> de argumento.
    /// </summary>
    [Fact]
    public void Expand_Nested() =>
        ShouldExpandTo(
            Square + "macro quad match Expression:e expand { ((@square e) * (@square e)) };\n"
            + "def r = @quad 2;",
            "def r = ((2 * 2) * (2 * 2));");

    /// <summary>
    /// A invocação vai até o delimitador, não até o próximo operador: é o que faz
    /// <c>@square x + 1</c> capturar <c>x + 1</c> inteiro, como a spec §4.1 quer —
    /// e o que obriga a parentizar quando se quer o contrário.
    /// </summary>
    [Fact]
    public void Invocation_IsGreedyUpToTheDelimiter() =>
        ShouldExpandTo(Square + "def r = @square x + 1;", "def r = ((x + 1) * (x + 1));");

    [Fact]
    public void Expand_DepthLimit() =>
        ShouldFailWith(
            """
            macro ping match Expression:e expand { @pong e };
            macro pong match Expression:e expand { @ping e };

            def r = @ping 1;
            """,
            DiagnosticCodes.MacroExpansionTooDeep);

    [Fact]
    public void Expand_UnknownMacro() =>
        ShouldFailWith("@inexistente 1;", DiagnosticCodes.UnknownMacro);

    [Fact]
    public void Expand_DuplicateMacro() =>
        ShouldFailWith(
            "macro m match Expression:e expand { e };\nmacro m match Identifier:i expand { i };",
            DiagnosticCodes.DuplicateMacro);

    /// <summary>A declaração some do programa: ela é sintaxe, não código.</summary>
    [Fact]
    public void Expand_DeclarationDisappears() =>
        ExpandWithDiagnostics(Log + "print(1);").File.Statements.Length.ShouldBe(1);

    /// <summary>
    /// Um programa sem <c>@</c> sai idêntico da expansão. É o que garante que esta
    /// fase não pode quebrar nada do que já existia.
    /// </summary>
    [Theory]
    [InlineData("def x = 1;\nprint(x);")]
    [InlineData("def f = fn(a: Int) Int { return a * 2; };\nprint(f(3));")]
    [InlineData("def C = enum { A, B };\nprint(match C.A { C.A => 1, C.B => 2 });")]
    [InlineData("var i = 0;\nloop :r { i = i + 1;\nif i < 3 { continue :r; } else { break; } }")]
    public void NoMacros_IsIdentity(string source) => ShouldExpandTo(source, source);

    // ------------------------------------------------------------- higiene

    /// <summary>
    /// Um identificador introduzido pela macro não captura o do programa: o
    /// <c>temp</c> da macro vira <c>temp@1</c>, que não é escrevível em fonte.
    /// </summary>
    [Fact]
    public void Hygiene_IntroducedNameDoesNotCapture() =>
        Expand("""
            macro example match Block:body expand { def temp = 10; body; };

            def temp = 1;
            @example { print(temp); }
            """).ShouldContain("temp@1");

    /// <summary>E o nome capturado mantém o contexto léxico de quem invocou.</summary>
    [Fact]
    public void Hygiene_CapturedNameKeepsMeaning() =>
        ShouldExpandTo(
            "macro show match Identifier:x expand { print(x); };\ndef value = 10;\n@show value;",
            "def value = 10;\nprint(value);");

    /// <summary>Duas expansões da mesma macro recebem marcas distintas.</summary>
    [Fact]
    public void Hygiene_TwoExpansions_DoNotCollide()
    {
        var expanded = Expand("""
            macro example match Expression:e expand { def temp = e; print(temp); };

            @example 1;
            @example 2;
            """);

        expanded.ShouldContain("temp@1");
        expanded.ShouldContain("temp@2");
    }

    /// <summary>
    /// E é isso que faz uma macro com `def` poder ser usada duas vezes no mesmo
    /// bloco sem `LAP0202` — a redefinição é detectada depois, sobre nomes já
    /// higienizados.
    /// </summary>
    [Fact]
    public void Hygiene_NoDuplicateDefinition() =>
        ExpansionCodes("""
            macro example match Expression:e expand { def temp = e; print(temp); };

            @example 1;
            @example 2;
            """).ShouldNotContain(DiagnosticCodes.DuplicateDefinition);

    /// <summary>O rótulo de um `loop` introduzido também é higienizado — `@unless` duas vezes no mesmo bloco.</summary>
    [Fact]
    public void Hygiene_LabelsAreRenamed()
    {
        var expanded = Expand("""
            macro unless
                match Expression:c Block:b
                expand { loop :done { if c { break :done; } b; break :done; } };

            @unless true { print(1); }
            @unless false { print(2); }
            """);

        expanded.ShouldContain("done@1");
        expanded.ShouldContain("done@2");
    }

    /// <summary>Nomes apenas referenciados não são tocados: `print` continua `print`.</summary>
    [Fact]
    public void Hygiene_FreeReferencesAreNotRenamed() =>
        Expand(Log + "@log 1;").ShouldNotContain("print@");

    /// <summary>
    /// A ligação de um `is` introduzido pela macro também é higienizada (plano
    /// 25) — duas invocações não colidem, do mesmo jeito que o rótulo de `loop`.
    /// </summary>
    [Fact]
    public void Hygiene_IsBindingsAreRenamed()
    {
        var expanded = Expand("""
            macro unwrap
                match Expression:e
                expand { if e is Some(value) { value } else { 0 } };

            @unwrap a;
            @unwrap b;
            """);

        expanded.ShouldContain("value@1");
        expanded.ShouldContain("value@2");
    }

    // -------------------------------------------------------- compile time

    /// <summary>
    /// Rodar um <c>constraint</c> exige o pipeline inteiro, que <c>Lapis.Macros</c>
    /// não tem: quem o executa é o orquestrador, através de
    /// <c>IConstraintRunner</c> (plano 18 §18.2).
    ///
    /// Expandir sem executor é legítimo — é o que estes testes fazem — mas
    /// encontrar uma macro <b>com</b> constraint aí é erro interno, não silêncio:
    /// ignorá-la faria a validação sumir sem que ninguém percebesse.
    /// </summary>
    [Fact]
    public void Constraint_WithoutARunner_IsAnInternalError() =>
        Should.Throw<InternalCompilerException>(() => ExpandWithDiagnostics(
            "macro m match Expression:e constraint { true } expand { e };\ndef r = @m 1;"));

    /// <summary>E uma macro sem constraint não precisa de executor nenhum.</summary>
    [Fact]
    public void NoConstraint_NeedsNoRunner() => ShouldExpandTo(Log + "@log 1;", "print(1);");
}

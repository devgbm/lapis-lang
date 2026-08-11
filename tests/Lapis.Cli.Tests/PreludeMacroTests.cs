using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.Cli.Tests;

/// <summary>
/// <c>@unless</c> e <c>@while</c>, escritas no <c>prelude.ls</c> na própria
/// linguagem (plano 20, spec de macros §12).
///
/// Nenhum destes programas declara as macros: elas vêm do prelude, como
/// <c>Result</c>.
/// </summary>
public sealed class PreludeMacroTests : ConstraintTestBase
{
    // ------------------------------------------------------------ @unless

    [Fact]
    public void Unless_False_RunsBody() =>
        Output("@unless false { print(\"executou\"); }").ShouldBe("executou\n");

    [Fact]
    public void Unless_True_SkipsBody() =>
        Output("@unless true { print(\"não\"); }\nprint(\"depois\");").ShouldBe("depois\n");

    /// <summary>
    /// <c>@unless c { b }</c> ≡ <c>if !c { b }</c>. A macro não inventa semântica:
    /// ela produz a sintaxe que alguém escreveria à mão.
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("1 < 2")]
    [InlineData("1 > 2")]
    public void Unless_EquivalentToNegatedIf(string condition) =>
        Output($"@unless {condition} {{ print(\"corpo\"); }}\nprint(\"fim\");")
            .ShouldBe(Output($"if !({condition}) {{ print(\"corpo\"); }}\nprint(\"fim\");"));

    /// <summary>Duas invocações no mesmo bloco não colidem: o rótulo é higienizado.</summary>
    [Fact]
    public void Unless_TwiceInTheSameBlock() =>
        Output("""
            @unless false { print(1); }
            @unless false { print(2); }
            """).ShouldBe("1\n2\n");

    // ------------------------------------------------------------- @while

    [Fact]
    public void While_Iterates() =>
        Output("""
            var i = 0;

            @while i < 10 {
                i = i + 1;
            }

            print(i);
            """).ShouldBe("10\n");

    [Fact]
    public void While_ZeroIterations() =>
        Output("""
            var i = 10;

            @while i < 0 {
                print("nunca");
            }

            print("fim");
            """).ShouldBe("fim\n");

    [Fact]
    public void While_CountsUp() =>
        Output("""
            var i = 0;

            @while i < 3 {
                i = i + 1;
                print(i);
            }
            """).ShouldBe("1\n2\n3\n");

    /// <summary>
    /// O primeiro programa LapisLang capaz de não terminar. O orçamento global de
    /// saltos é o que torna "todo programa termina ou reporta <c>LAP0303</c>" uma
    /// propriedade verificável, em vez de uma esperança.
    /// </summary>
    [Fact]
    public void While_Infinite_Aborts()
    {
        var result = Compile("@while true { def x = 1; }");

        result.Evaluation.ShouldNotBeNull();
        result.Evaluation.Status.ShouldBe(Evaluator.ExecutionStatus.Aborted);
        result.Evaluation.Code.ShouldBe(DiagnosticCodes.JumpLimitExceeded);
    }

    /// <summary>
    /// Um <c>var</c> declarado <b>depois</b> de um <c>label</c> vive dentro daquele
    /// join. É por isso que as declarações que o laço lê ficam antes da invocação —
    /// e é o que faz o estado sobreviver de uma volta para a outra.
    /// </summary>
    [Fact]
    public void While_DeclarationsBeforeLabel_SurviveEveryIteration() =>
        Output("""
            var total = 0;
            var i = 0;

            @while i < 4 {
                total = total + i;
                i = i + 1;
            }

            print(total);
            """).ShouldBe("6\n");

    [Fact]
    public void While_BodySeesOuterBindings() =>
        Output("""
            def passo = 5;
            var i = 0;

            @while i < 20 {
                i = i + passo;
            }

            print(i);
            """).ShouldBe("20\n");

    /// <summary>
    /// Salto para trás é mais uma volta do <c>while</c> do evaluator, não uma
    /// chamada: a pilha de C# não cresce. Sem isto, um laço de verdade seria
    /// impossível — e é o que separa join point de salto arbitrário.
    /// </summary>
    [Fact]
    public void While_DoesNotGrowStack() =>
        Output("""
            var i = 0;

            @while i < 100000 {
                i = i + 1;
            }

            print(i);
            """).ShouldBe("100000\n");

    [Fact]
    public void While_TwiceInTheSameBlock() =>
        Output("""
            var i = 0;
            var j = 0;

            @while i < 2 { i = i + 1; }
            @while j < 3 { j = j + 1; }

            print(i);
            print(j);
            """).ShouldBe("2\n3\n");

    /// <summary>Um laço dentro do outro: os rótulos de cada expansão são distintos.</summary>
    [Fact]
    public void While_Nested()
    {
        // O corpo interno precisa reiniciar seu contador a cada volta externa, e a
        // declaração dele fica fora do laço pela regra de escopo dos joins.
        Output("""
            var i = 0;
            var j = 0;
            var total = 0;

            @while i < 3 {
                j = 0;

                @while j < 2 {
                    total = total + 1;
                    j = j + 1;
                }

                i = i + 1;
            }

            print(total);
            """).ShouldBe("6\n");
    }

    // ---------------------------------------------- a restrição de escrita

    /// <summary>
    /// <b>Todo <c>var</c> que um laço usa se declara antes do primeiro
    /// <c>@while</c> do bloco.</b>
    ///
    /// Cada <c>label</c> de um bloco abre um join, e joins são <b>irmãos</b>, não
    /// aninhados: um não enxerga os bindings declarados no outro, porque um salto
    /// pode ter pulado a declaração. Um <c>var</c> escrito entre dois
    /// <c>@while</c> fica preso no join que o primeiro deixou aberto.
    ///
    /// A regra é semanticamente correta e vem do M6 — o que o M11 muda é que
    /// agora os rótulos são <b>invisíveis</b>: quem escreve <c>@while</c> não tem
    /// por que saber que um <c>label</c> foi introduzido. Por isso ela está
    /// travada aqui: se um dia deixar de valer (join com parâmetros), é uma
    /// melhoria, e este teste é onde ela aparece.
    /// </summary>
    [Fact]
    public void While_VarDeclaredBetweenTwoLoops_IsNotVisible() =>
        Codes("""
            var i = 0;

            @while i < 2 { i = i + 1; }

            var k = 0;

            @while k < 3 { k = k + 1; }
            """).ShouldContain(DiagnosticCodes.UnknownVariable);

    /// <summary>E declarados antes do primeiro laço, os dois funcionam.</summary>
    [Fact]
    public void While_AllVarsBeforeTheFirstLoop_Works() =>
        Output("""
            var i = 0;
            var k = 0;

            @while i < 2 { i = i + 1; }
            @while k < 3 { k = k + 1; }

            print(i);
            print(k);
            """).ShouldBe("2\n3\n");

    /// <summary>
    /// Um <c>@unless</c> aninhado no corpo de um <c>@while</c> enxerga o que está
    /// fora: o corpo do laço é um join só, e a macro interna não abre um irmão
    /// dele.
    /// </summary>
    [Fact]
    public void Unless_NestedInsideWhile_SeesOuterVars() =>
        Output("""
            var k = 0;

            @while k < 3 {
                k = k + 1;
                @unless false { print(k); }
            }
            """).ShouldBe("1\n2\n3\n");

    // ------------------------------------------------------- sombreamento

    /// <summary>
    /// Uma macro do arquivo com o mesmo nome <b>vence</b>, sem diagnóstico — a
    /// mesma regra que já vale para <c>def Result = ...</c>. Quem escreve
    /// <c>macro while</c> no seu arquivo quis o seu.
    /// </summary>
    [Fact]
    public void PreludeMacro_IsShadowedByTheFile() =>
        Output("""
            macro while
                match Expression:e
                expand { print(e); };

            @while 42;
            """).ShouldBe("42\n");

    /// <summary>E duas declarações no mesmo arquivo continuam colidindo.</summary>
    [Fact]
    public void TwoFileMacros_WithTheSameName_StillCollide() =>
        Codes("""
            macro repetida match Expression:e expand { print(e); };
            macro repetida match Identifier:i expand { print(i); };
            """).ShouldContain(DiagnosticCodes.DuplicateMacro);

    [Fact]
    public void PreludeMacros_AreDeclared()
    {
        var names = PreludeLoader.Load().Macros.Select(m => m.Name);

        names.ShouldBe(["unless", "while"], ignoreOrder: true);
    }

    /// <summary>
    /// As macros do prelude <b>somem</b> do programa antes do desugar: elas são
    /// sintaxe, não código. Nenhum binding chamado `while` ou `unless` existe.
    /// </summary>
    [Fact]
    public void PreludeMacros_AreNotBindings()
    {
        var names = PreludeLoader.Load().Bindings.Select(b => b.Name).ToList();

        names.ShouldNotContain("while");
        names.ShouldNotContain("unless");
    }
}

/// <summary>
/// O critério que mais importa no plano 20: <b>nada foi retirado da Core</b>.
/// Um <c>@while</c> quebrado não pode regredir nenhum programa que já funcionava.
/// </summary>
public sealed class ControlFlowIsIntactTests : ConstraintTestBase
{
    [Fact]
    public void CoreIf_StillExists()
    {
        Output("if 1 < 2 { print(\"sim\"); } else { print(\"não\"); }").ShouldBe("sim\n");

        typeof(Ast.Core.CoreIf).ShouldNotBeNull();
    }

    /// <summary>
    /// <c>match</c> continua no compilador <b>com</b> desestruturação de carga —
    /// que é justamente o que nenhuma macro conseguiria fazer com segurança, e a
    /// razão de `@match` ter ficado fora do escopo (§20.3, Q23).
    /// </summary>
    [Fact]
    public void CoreMatch_StillExists_WithPayloadDestructuring()
    {
        Output("""
            def numeros = [10, 20, 30];

            match numeros[1] {
                Result.Ok(valor) => print(valor),
                Result.Err(erro) => print("erro")
            }
            """).ShouldBe("20\n");

        typeof(Ast.Core.CoreMatch).ShouldNotBeNull();
    }

    [Fact]
    public void IfAndMatch_AreStillKeywords()
    {
        Codes("def if = 1;").ShouldNotBeEmpty();
        Codes("def match = 1;").ShouldNotBeEmpty();
    }

    /// <summary>
    /// E `while` e `unless` **não** são palavras reservadas: são nomes de macro, e
    /// o espaço de nomes de macro é separado (Q19). `def while = 1;` continua um
    /// programa válido.
    /// </summary>
    [Fact]
    public void WhileAndUnless_AreNotKeywords() =>
        Output("def while = 1;\ndef unless = 2;\nprint(while + unless);").ShouldBe("3\n");

    [Fact]
    public void ResultExample_Unchanged()
    {
        var path = CliRunner.ExamplePath("result.ls");
        var (exit, _, stderr) = CliRunner.Run(path);

        exit.ShouldBe(ExitCodes.Success, stderr);
    }
}

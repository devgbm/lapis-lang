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
    /// Um <c>var</c> declarado <b>entre</b> dois laços é visível no segundo.
    ///
    /// Foi a restrição que o M11 expôs e que o regrouping do desugar removeu: os
    /// rótulos do bloco não formam mais um grupo só. Dois <c>@while</c>
    /// independentes não têm salto entre si, então o segundo vira um grupo
    /// <b>aninhado</b> no corpo do primeiro, e a declaração o envolve como um
    /// <c>Let</c> comum.
    ///
    /// Este teste já afirmou o contrário. A troca é a melhoria.
    /// </summary>
    [Fact]
    public void While_VarDeclaredBetweenTwoLoops_IsVisible() =>
        Output("""
            var i = 0;

            @while i < 2 { i = i + 1; }

            var k = 0;

            @while k < 3 { k = k + 1; }

            print(i);
            print(k);
            """).ShouldBe("2\n3\n");

    [Fact]
    public void While_AllVarsBeforeTheFirstLoop_AlsoWorks() =>
        Output("""
            var i = 0;
            var k = 0;

            @while i < 2 { i = i + 1; }
            @while k < 3 { k = k + 1; }

            print(i);
            print(k);
            """).ShouldBe("2\n3\n");

    /// <summary>
    /// Três laços seguidos, cada um declarando o seu contador logo antes: a forma
    /// que qualquer um escreveria, e que antes não compilava.
    /// </summary>
    [Fact]
    public void While_SeveralLoops_EachWithItsOwnVar() =>
        Output("""
            var a = 0;
            @while a < 1 { a = a + 1; }

            var b = 0;
            @while b < 2 { b = b + 1; }

            var c = 0;
            @while c < 3 { c = c + 1; }

            print(a + b + c);
            """).ShouldBe("6\n");

    /// <summary>
    /// O corpo de um laço é um <b>escopo</b>, como o de um <c>if</c>: o que ele
    /// declara não escapa.
    ///
    /// Vem de o <c>expand</c> escrever <c>{ body; }</c> e não <c>body;</c> — uma
    /// captura de bloco nua é colada no lugar, entre chaves é um escopo. Sem isso,
    /// <c>@while</c> se comportaria diferente de <c>if</c> sem nenhuma razão.
    /// </summary>
    [Fact]
    public void While_BodyIsAScope() =>
        Codes("""
            var i = 0;

            @while i < 2 {
                def x = 1;
                i = i + 1;
            }

            print(x);
            """).ShouldContain(DiagnosticCodes.UnknownVariable);

    [Fact]
    public void Unless_BodyIsAScope() =>
        Codes("@unless false { def x = 1; }\nprint(x);")
            .ShouldContain(DiagnosticCodes.UnknownVariable);

    /// <summary>E o corpo continua enxergando o que está fora dele.</summary>
    [Fact]
    public void While_BodyStillSeesTheOutside() =>
        Output("""
            def passo = 2;
            var i = 0;

            @while i < 4 {
                def dobro = passo;
                i = i + dobro;
            }

            print(i);
            """).ShouldBe("4\n");

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
            var numeros = .[10, 20, 30];

            match numeros[1] {
                Option.Some(valor) => print(valor),
                Option.None => print("erro")
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

namespace Lapis.Evaluator.Tests;

/// <summary>
/// <c>xs[i] = v</c> em execução (Q36).
///
/// Span é <b>valor</b>: a escrita reconstrói o span e religa o slot, como
/// <c>u.a.b = 1</c> já fazia. Nenhum valor existente muda, e por isso a premissa
/// da Q25 — nenhuma closure captura <c>var</c>, logo não há aliasing — continua
/// valendo, junto com a do partial evaluator que depende dela.
/// </summary>
public sealed class SpanAssignmentEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void Write_ReplacesTheElement() =>
        Output("var xs = .[0, 1, 2];\nxs[1] = 9;\nprint(xs);").ShouldBe("[0, 9, 2]\n");

    /// <summary>Semântica de valor, e é observável: a cópia não acompanha.</summary>
    [Fact]
    public void Write_DoesNotReachACopy() =>
        Output("var xs = .[0, 1, 2];\ndef copia = xs;\nxs[0] = 9;\nprint(copia);").ShouldBe("[0, 1, 2]\n");

    [Fact]
    public void Write_OutOfBounds_HasNoEffect() =>
        Output("var xs = .[0, 1, 2];\nxs[99] = 9;\nprint(xs);").ShouldBe("[0, 1, 2]\n");

    [Fact]
    public void Write_NegativeIndex_HasNoEffect() =>
        Output("var xs = .[0, 1, 2];\nxs[0 - 1] = 9;\nprint(xs);").ShouldBe("[0, 1, 2]\n");

    /// <summary>
    /// Fora dos limites em <b>qualquer</b> profundidade não escreve nada — não é
    /// "não escreve naquele elemento". A reconstrução aborta inteira.
    /// </summary>
    [Fact]
    public void Write_OutOfBoundsDeepInThePath_HasNoEffectAtAll() =>
        Output(
            """
            def Caixa = type {
                xs: [Int;?];
            };

            var c = .Caixa { xs: .[1, 2, 3] };

            c.xs[99] = 9;
            print(c);
            """)
            .ShouldBe("Caixa { xs: [1, 2, 3] }\n");

    [Fact]
    public void Write_ThroughAFieldPath_Works() =>
        Output(
            """
            def Caixa = type {
                xs: [Int;?];
            };

            var c = .Caixa { xs: .[1, 2, 3] };

            c.xs[1] = 20;
            print(c);
            """)
            .ShouldBe("Caixa { xs: [1, 20, 3] }\n");

    /// <summary>
    /// O índice roda <b>antes</b> do valor, que é a ordem em que estão escritos
    /// (plano 08 §8.3). Enquanto o caminho só tinha campos isto não era
    /// observável — não havia nada a avaliar nele.
    /// </summary>
    [Fact]
    public void Write_EvaluatesTheIndexBeforeTheValue() =>
        Output(
            """
            def rastreia = fn(nome: Str, valor: Int) Int {
                print(nome);
                return valor;
            };

            var xs = .[0, 1, 2];

            xs[rastreia("indice", 1)] = rastreia("valor", 9);
            print(xs);
            """)
            .ShouldBe("indice\nvalor\n[0, 9, 2]\n");

    /// <summary>
    /// O laço que a fase A existe para destravar: um span de elementos
    /// computados e distintos, que nenhuma expressão da linguagem produzia.
    /// </summary>
    [Fact]
    public void Write_InALoop_BuildsAComputedSpan() =>
        Output(
            """
            var out = .[Int; 0; 4];
            var i = 0;

            loop {
                if i >= out.length { break; }

                out[i] = i * i;

                i = i + 1;
            }

            print(out);
            """)
            .ShouldBe("[0, 1, 4, 9]\n");
}

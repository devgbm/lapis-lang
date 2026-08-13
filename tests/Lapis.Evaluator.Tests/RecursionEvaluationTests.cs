namespace Lapis.Evaluator.Tests;

/// <summary>
/// Recursão em execução (Q34, plano 27 §A1).
///
/// O ambiente é imutável, então a closure não pode conter a si mesma: ela guarda
/// o <b>nome</b>, e a chamada é quem religa. Estes casos travam essa mecânica —
/// que o nome exista dentro do corpo, que ele valha em cada quadro, e que
/// qualquer ligação interna de mesmo nome ganhe dele.
/// </summary>
public sealed class RecursionEvaluationTests : EvaluatorTestBase
{
    [Fact]
    public void DirectRecursion_Computes() =>
        Output(
            """
            def fatorial = fn(n: Int) Int {
                if n <= 1 { return 1; }

                return n * fatorial(n - 1);
            };

            print(fatorial(10));
            """)
            .ShouldBe("3628800\n");

    /// <summary>Dois ramos recursivos: cada quadro reencontra o nome, não só o primeiro.</summary>
    [Fact]
    public void TreeRecursion_Computes() =>
        Output(
            """
            def fib = fn(n: Int) Int {
                if n < 2 { return n; }

                return fib(n - 1) + fib(n - 2);
            };

            print(fib(15));
            """)
            .ShouldBe("610\n");

    /// <summary>
    /// O nome religado não escapa da closure: fora dela, <c>f</c> é o que o
    /// escopo léxico diz, não o que a recursão precisou.
    /// </summary>
    [Fact]
    public void SelfName_DoesNotLeakIntoTheCaller() =>
        Output(
            """
            def f = fn(n: Int) Int { return n; };
            def g = fn() Int {
                def f = 7;

                return f;
            };

            print(f(1));
            print(g());
            """)
            .ShouldBe("1\n7\n");

    /// <summary>
    /// Um <c>def</c> interno de mesmo nome sombreia a própria função: o religar
    /// acontece na entrada do corpo, e tudo que é declarado depois vem por cima.
    /// </summary>
    [Fact]
    public void InnerBinding_ShadowsTheSelfName() =>
        Output(
            """
            def f = fn(n: Int) Int {
                def f = 42;

                return f;
            };

            print(f(0));
            """)
            .ShouldBe("42\n");

    /// <summary>
    /// Recursão e captura convivem: o quadro enxerga o ambiente do ponto de
    /// definição <b>mais</b> o próprio nome, e não um no lugar do outro.
    /// </summary>
    [Fact]
    public void Recursion_CoexistsWithCapture() =>
        Output(
            """
            def base = 100;

            def soma = fn(n: Int) Int {
                if n <= 0 { return base; }

                return soma(n - 1) + 1;
            };

            print(soma(5));
            """)
            .ShouldBe("105\n");

    /// <summary>
    /// Uma recursão funda de verdade termina. É o que a pilha própria do
    /// evaluator compra: na pilha padrão do host o processo cairia bem antes
    /// disto, e cairia sem diagnóstico nenhum.
    /// </summary>
    [Fact]
    public void DeepRecursion_DoesNotOverflowTheHostStack() =>
        Output(
            """
            def conta = fn(n: Int) Int {
                if n <= 0 { return 0; }

                return conta(n - 1) + 1;
            };

            print(conta(5000));
            """)
            .ShouldBe("5000\n");

    /// <summary>
    /// Sem caso base, o limite da <b>linguagem</b> responde: <c>LAP0302</c>, e
    /// não uma queda do processo.
    /// </summary>
    [Fact]
    public void UnboundedRecursion_AbortsWithCallDepthLimit() =>
        ExpectAbort("def f = fn(n: Int) Int { return f(n); };\nprint(f(0));")
            .ShouldBe("LAP0302");
}

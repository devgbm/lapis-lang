using Lapis.Ast.Core;

namespace Lapis.PartialEvaluator.Tests;

/// <summary>
/// A bateria que separa um partial evaluator correto de um que "otimiza"
/// quebrando o programa (plano 12 §12.4).
///
/// Um erro aqui não aparece como residual feio: aparece como um programa que
/// imprime coisa diferente. É por isso que todo caso confere a saída dos dois.
/// </summary>
public sealed class EffectTests : PETestBase
{
    /// <summary>
    /// Nada de <c>print</c> em tempo de PE, nem com argumento conhecido. Executá-lo
    /// moveria a saída do programa para o tempo de compilação — violação direta da
    /// spec §40.
    /// </summary>
    [Fact]
    public void Print_IsNeverExecutedAtPeTime()
    {
        var before = Console.Out;

        // O PE não recebe RuntimeContext nenhum: não teria para onde escrever.
        // O que este teste trava é que ele também não tenta.
        Evaluate("print(30);\nprint(\"texto\");");

        Console.Out.ShouldBeSameAs(before);
    }

    [Fact]
    public void Print_IsResidualizedWithTheFoldedArgument() =>
        ShouldSpecializeTo("print(10 + 20);", "print(30);");

    [Fact]
    public void Effects_KeepTheirOrder() =>
        ShouldSpecializeTo("print(1);\nprint(2);\nprint(3);", "print(1); print(2); print(3);");

    /// <summary>
    /// Um valor descartado não autoriza descartar o efeito: o binding sobrevive
    /// inteiro, mesmo sem ninguém ler o nome.
    /// </summary>
    [Fact]
    public void Effects_AreNotEliminated() =>
        ShouldSpecializeTo("def ignorado = print(1);\nprint(2);", "def ignorado = print(1); print(2);");

    /// <summary>E um binding puro e não lido some — é a outra metade da regra.</summary>
    [Fact]
    public void PureUnusedBinding_IsEliminated() =>
        ShouldSpecializeTo("def naoUsado = 1 + 2;\nprint(3);", "print(3);");

    /// <summary>
    /// Um binding dinâmico e impuro não pode ser substituído no corpo: seriam
    /// **dois** efeitos onde havia um.
    /// </summary>
    [Fact]
    public void Effects_AreNotDuplicated()
    {
        var residual = Residual("""
            def contador = fn() Int { return 1; };
            def v = contador();
            print(v + v);
            """);

        // `print` também é chamada: o que se conta aqui é o efeito que poderia ter
        // sido duplicado.
        CountCalls(residual, "contador").ShouldBe(1, $"residual:\n{residual}");
        ShouldPreserveBehaviour("""
            def contador = fn() Int { return 1; };
            def v = contador();
            print(v + v);
            """);
    }

    /// <summary>Chamada é impura por conservadorismo: o PE não sabe o que há do outro lado.</summary>
    [Fact]
    public void UnknownCall_IsTreatedAsImpure()
    {
        Effects.IsPure(Parse("f()")).ShouldBeFalse();
        Effects.IsPure(Parse("print(1)")).ShouldBeFalse();
    }

    /// <summary>
    /// Nenhuma expressão aritmética é impura — é o ganho direto da Q9, que tornou
    /// a divisão total e com ela toda a aritmética dobrável sem análise nenhuma.
    /// </summary>
    [Theory]
    [InlineData("1 + 2")]
    [InlineData("1 / 0")]
    [InlineData("x * y - 3")]
    [InlineData("!(a == b)")]
    [InlineData("-x")]
    [InlineData("[1, 2, 3]")]
    [InlineData("a[0]")]
    public void Arithmetic_IsNeverImpure(string expression) =>
        Effects.IsPure(Parse(expression)).ShouldBeTrue();

    [Theory]
    [InlineData("f(1)")]
    [InlineData("return 1")]
    [InlineData("1 + f()")]
    public void CallsAndControlFlow_AreImpure(string expression) =>
        Effects.IsPure(Parse(expression)).ShouldBeFalse();

    /// <summary>Criar uma closure não tem efeito; chamá-la tem.</summary>
    [Fact]
    public void Lambda_IsPure() => Effects.IsPure(Parse("fn() Int { return print(1); }")).ShouldBeTrue();

    // ------------------------------------------------------------ helpers

    /// <summary>
    /// A expressão de um programa de um statement só, já na Core. Sem type
    /// checker: <see cref="Effects"/> classifica forma sintática, e os casos aqui
    /// usam nomes que de propósito não existem.
    /// </summary>
    private static CoreExpr Parse(string expression) =>
        CompileToCore($"def alvo = {expression};\n").Body.ShouldBeOfType<CoreLet>().Value;

    private static int CountCalls(string source, string callee)
    {
        var counter = new CallCounter(callee);
        counter.Visit(CompileToCore(source).Body);
        return counter.Count;
    }

    private sealed class CallCounter(string callee) : CoreWalker
    {
        public int Count { get; private set; }

        protected override void OnNode(CoreExpr node)
        {
            if (node is CoreCall { Callee: CoreVariable name } && name.Name == callee)
            {
                Count++;
            }
        }
    }
}

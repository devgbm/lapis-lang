namespace Lapis.PartialEvaluator.Tests;

/// <summary>Propagação de variáveis, código morto e condicionais (plano 12 §12.3–§12.5).</summary>
public sealed class PropagationTests : PETestBase
{
    [Fact]
    public void KnownValue_Propagates() =>
        ShouldSpecializeTo("def x = 10;\nprint(x + 5);", "print(15);");

    [Fact]
    public void KnownValue_PropagatesThroughSeveralBindings() =>
        ShouldSpecializeTo("def a = 2;\ndef b = a * 3;\ndef c = b + 4;\nprint(c);", "print(10);");

    [Fact]
    public void UnusedPureBinding_IsEliminated() =>
        ShouldSpecializeTo("def x = 10;\nprint(1);", "print(1);");

    /// <summary>Desligar a eliminação mantém o binding — a flag serve para bisect.</summary>
    [Fact]
    public void DeadCodeElimination_CanBeDisabled()
    {
        var residual = Evaluate(
            "def x = 10;\nprint(1);",
            options: new PEOptions(DeadCodeElimination: false));

        Ast.Printing.CoreSourcePrinter.Print(residual.Residual).ShouldContain("def x");
    }

    /// <summary>
    /// Um binding dinâmico e não trivial sobrevive: substituí-lo duplicaria a
    /// chamada, e com ela o efeito.
    /// </summary>
    [Fact]
    public void DynamicBinding_IsKept() =>
        ShouldSpecializeTo(
            "def f = fn() Int { return 1; };\ndef x = f();\nprint(x + 1);",
            "def f = fn() Int { return 1 }; def x = f(); print(x + 1);");

    /// <summary>
    /// Uma referência trivial some: substituir <c>x</c> por <c>y</c> não duplica
    /// trabalho nenhum.
    /// </summary>
    [Fact]
    public void TrivialReference_IsInlined() =>
        ShouldSpecializeTo(
            "def g = fn(y: Int) Int { def x = y; return x + 1; };\nprint(g(1));",
            "def g = fn(y: Int) Int { return y + 1 }; print(g(1));");

    /// <summary>
    /// Mas não quando o corpo redeclara o nome referenciado: substituir ali
    /// capturaria o binding errado, que é o único jeito de a propagação mudar o
    /// significado do programa.
    /// </summary>
    [Fact]
    public void TrivialReference_IsNotInlinedIntoAShadow() =>
        ShouldPreserveBehaviour("""
            def h = fn(y: Int) Int {
                def x = y;
                def y = 100;
                return x + y;
            };

            print(h(1));
            """);

    /// <summary>Um <c>var</c> nunca propaga: o valor de hoje não é o de amanhã (Q25).</summary>
    [Fact]
    public void Var_DoesNotPropagate() =>
        ShouldSpecializeTo("var x = 1;\nx = 2;\nprint(x);", "var x = 1; x = 2; print(x);");

    // -------------------------------------------------------- condicionais

    [Fact]
    public void If_StaticTrue() => ShouldSpecializeTo("if true { print(1); }", "{ print(1); };");

    [Fact]
    public void If_StaticFalse_LeavesNothing() =>
        ShouldSpecializeTo("if false { print(1); }\nprint(2);", "print(2);");

    /// <summary>
    /// O ramo morto nem chega a ser especializado: um <c>1/0</c> lá dentro
    /// simplesmente não existe no residual, porque era inalcançável no original.
    /// </summary>
    [Fact]
    public void If_DeadBranch_IsNotEvenSpecialized() =>
        ShouldSpecializeTo("def x = if false { 1 / 0 } else { 1 };\nprint(x);", "print(1);");

    [Fact]
    public void If_Dynamic_SpecializesBothBranches() =>
        ShouldSpecializeTo(
            "def f = fn(c: Bool) Int { if c { return 1 + 1; } return 2 + 2; };\nprint(f(true));",
            "def f = fn(c: Bool) Int { if c { return 2 } else { }; return 4 }; print(f(true));");

    // ------------------------------------------------------------- return

    [Fact]
    public void Return_Static_IsFolded() =>
        ShouldSpecializeTo(
            "def f = fn() Int { return 10 + 20; };\nprint(f());",
            "def f = fn() Int { return 30 }; print(f());");

    [Fact]
    public void CodeAfterStaticReturn_IsEliminated() =>
        ShouldSpecializeTo(
            "def f = fn() Int { return 1; print(2); };\nprint(f());",
            "def f = fn() Int { return 1 }; print(f());");

    /// <summary>
    /// Com <c>return</c> sob condição dinâmica, o que vem depois <b>não</b> pode
    /// ser executado estaticamente — é a completion <c>MayReturn</c>, a peça que a
    /// maioria dos partial evaluators de brinquedo esquece.
    /// </summary>
    [Fact]
    public void CodeAfterDynamicReturn_IsPreserved() =>
        ShouldSpecializeTo(
            "def f = fn(c: Bool) Int { if c { return 1; } print(2); return 3; };\nprint(f(true));",
            "def f = fn(c: Bool) Int { if c { return 1 } else { }; print(2); return 3 }; print(f(true));");

    [Fact]
    public void BothReturnsSurvive_UnderDynamicCondition() =>
        ShouldPreserveBehaviour("""
            def f = fn(x: Int) Int {
                if x < 0 { return 0; }
                return 10 + 20;
            };

            print(f(-1));
            print(f(1));
            """);

    // -------------------------------------------------------------- match

    [Fact]
    public void Match_StaticScrutinee_SelectsTheArm() =>
        ShouldSpecializeTo("def x = 2;\nprint(match x { 1 => \"um\", _ => \"outro\" });", "print(\"outro\");");

    [Fact]
    public void Match_DynamicScrutinee_SpecializesEveryArm() =>
        ShouldSpecializeTo(
            "def f = fn(v: Int) Int { return match v { 1 => 2 + 3, _ => 4 + 5 }; };\nprint(f(1));",
            "def f = fn(v: Int) Int { return match v { 1 => 5, _ => 9 } }; print(f(1));");

    // ------------------------------------------------------------- spans

    [Fact]
    public void Span_AllStatic_Folds() =>
        ShouldSpecializeTo("print(.[1 + 1, 2 + 2]);", "print(.[2, 4]);");

    [Fact]
    public void Span_PartlyDynamic_IsKept() =>
        ShouldSpecializeTo(
            "def f = fn(x: Int) [Int;2] { return .[1, x]; };\nprint(f(2));",
            "def f = fn(x: Int) [Int;2] { return .[1, x] }; print(f(2));");

    /// <summary>
    /// Com o tamanho no tipo e o índice conhecido, o checker já resolveu a
    /// indexação — o especializador só precisa ler o elemento, sem envelope
    /// nenhum para desembrulhar (plano 24 §24.7).
    /// </summary>
    [Fact]
    public void Span_TotalIndex_Folds() =>
        ShouldSpecializeTo("def a = .[10, 20, 30];\nprint(a[1]);", "print(20);");

    /// <summary>E o <c>length</c> de um span de tamanho conhecido é uma constante.</summary>
    [Fact]
    public void Span_KnownLength_Folds() =>
        ShouldSpecializeTo("def a = .[10, 20, 30];\nprint(a.length);", "print(3);");

    /// <summary>
    /// A repetição com quantidade constante e inicializador conhecido é um valor:
    /// o residual é a lista.
    /// </summary>
    [Fact]
    public void SpanRepeat_Static_Folds() =>
        ShouldSpecializeTo("print(.[Int; 1 + 1; 3]);", "print(.[2, 2, 2]);");

    /// <summary>
    /// Sem a quantidade constante não há o que construir em compilação: a
    /// construção atravessa intacta.
    /// </summary>
    [Fact]
    public void SpanRepeat_DynamicSize_IsKept() =>
        ShouldSpecializeTo(
            "def f = fn(n: Int) [Int;?] { return .[Int; 0; n]; };\nprint(f(3));",
            "def f = fn(n: Int) [Int;?] { return .[Int; 0; n] }; print(f(3));");

    // ------------------------------------------------ membros de tipo

    /// <summary>
    /// Um membro com valor conhecido dobra como qualquer outro binding: o acesso
    /// vira o valor, e o `Let` do membro some por código morto — junto com o
    /// próprio tipo, que depois disso não é citado por ninguém.
    /// </summary>
    [Fact]
    public void Member_KnownValue_Folds() =>
        ShouldSpecializeTo(
            "def User = type { n: Int; };\ndef User.padrao = 30;\nprint(User.padrao);",
            "print(30);");

    /// <summary>
    /// Regressão. `User.hello` lê o `Let` ligado a `User#hello`, e esse nome não
    /// aparece na árvore — o uso é um `CoreField` cujo `Name` é só `hello`. Sem
    /// contá-lo como uso, o membro virava código morto e o residual citava um
    /// membro que não existia mais.
    ///
    /// O residual também não pode citar o nome sintético: `User#hello` não é
    /// lexável, e o que sobrevive é o próprio acesso.
    /// </summary>
    [Fact]
    public void Member_DynamicValue_SurvivesAndStaysWritable() =>
        ShouldSpecializeTo(
            """
            def User = type { n: Int; };
            def User.grita = fn(s: Str) Str { return s; };
            print(User.grita("a"));
            """,
            "def User = type { n: Int; }; "
            + "def User.grita = fn(s: Str) Str { return s }; print(User.grita(\"a\"));");

    // --------------------------------------------- atribuição a campo

    /// <summary>
    /// Regressão, e o defeito é do M7. `def copia = u;` com `u` sendo `var` é um
    /// **instantâneo**, não um apelido: substituir `copia` por `u` faz uma
    /// atribuição posterior mudar o que `copia` valia.
    ///
    /// O corpus não tinha essa forma até `def b = a; a.campo = e;` (plano 21)
    /// trazê-la, mas o mesmo vale para a atribuição simples — que é como este
    /// teste está escrito.
    /// </summary>
    [Fact]
    public void TrivialReference_ToMutable_IsNotInlined() =>
        ShouldSpecializeTo(
            "var u = 1;\ndef copia = u;\nu = 2;\nprint(copia);",
            "var u = 1; def copia = u; u = 2; print(copia);");

    [Fact]
    public void FieldAssignment_SurvivesSpecialization() =>
        ShouldPreserveBehaviour("""
            def User = type { name: Str; };

            var u = .User { name: "antes" };

            def copia = u;

            u.name = "depois";

            print(u.name);
            print(copia.name);
            """);

    // ------------------------------------------- argumento genérico nu

    /// <summary>
    /// Regressão. Um argumento genérico nu é uma <b>string</b>, não um
    /// <c>CoreVariable</c>: a propagação não passava por ele, mas o <c>Let</c>
    /// sumia mesmo assim, e o residual citava um nome que não existia mais.
    ///
    /// O bug é anterior ao span — o corpus só nunca tinha exercitado a forma.
    /// </summary>
    [Fact]
    public void ConstGenericArgument_ByName_IsPropagated() =>
        ShouldSpecializeTo(
            """
            def escala = fn<N: Int>(x: Int) Int { return x * N; };
            def n = 3;
            print(escala<n>(5));
            """,
            "def escala = fn<N: Int>(x: Int) Int { return x * N }; print(escala<3>(5));");

    /// <summary>
    /// A construção de um `type` genérico tem a mesma posição, e o mesmo
    /// tratamento. O `Let` de `b` sobrevive porque um struct não é
    /// residualizável (M7) — o que importa aqui é que o `n` sumiu do argumento.
    /// </summary>
    [Fact]
    public void ConstGenericArgument_InConstruction_IsPropagated() =>
        ShouldSpecializeTo(
            """
            def Boxed = type<T, N: Int> { value: T; };
            def n = 4;
            def b = .Boxed<Int, n> { value: 9 };
            print(b.value);
            """,
            "def Boxed = type<T, N: Int> { value: T; }; "
            + "def b = .Boxed<Int, 4> { value: 9 }; print(b.value);");

    /// <summary>
    /// E um argumento que nomeia um **tipo** atravessa intacto: definição de tipo
    /// não é valor conhecido no ambiente estático, então nunca cai nesse caminho.
    /// </summary>
    [Fact]
    public void TypeArgument_ByName_IsKept() =>
        ShouldSpecializeTo(
            """
            def Cor = enum { Verde };
            def identidade = fn<T>(v: T) T { return v; };
            def f = fn(c: Cor) Cor { return identidade<Cor>(c); };
            print(f(Cor.Verde));
            """,
            "def Cor = enum { Verde }; def identidade = fn<T>(v: T) T { return v }; "
            + "def f = fn(c: Cor) Cor { return identidade<Cor>(c) }; print(f(Cor.Verde));");
}

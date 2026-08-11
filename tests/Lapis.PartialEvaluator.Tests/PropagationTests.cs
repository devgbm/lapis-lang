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

    // ------------------------------------------------------------ arrays

    [Fact]
    public void Array_AllStatic_Folds() =>
        ShouldSpecializeTo("print([1 + 1, 2 + 2]);", "print([2, 4]);");

    [Fact]
    public void Array_PartlyDynamic_IsKept() =>
        ShouldSpecializeTo(
            "def f = fn(x: Int) Int[] { return [1, x]; };\nprint(f(2));",
            "def f = fn(x: Int) Int[] { return [1, x] }; print(f(2));");
}

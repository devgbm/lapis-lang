namespace Lapis.PartialEvaluator.Tests;

/// <summary>
/// Redução de literais e constant folding (spec §38, plano 12).
///
/// Todo caso passa pelo protocolo da spec §53: além de conferir o residual, roda
/// os dois programas e compara saída e desfecho.
/// </summary>
public sealed class FoldingTests : PETestBase
{
    /// <summary>O exemplo da spec §38.</summary>
    [Fact]
    public void Add_Constants() => ShouldSpecializeTo("print(10 + 20);", "print(30);");

    [Fact]
    public void Literal_Unchanged() => ShouldSpecializeTo("print(1);", "print(1);");

    [Fact]
    public void Nested_Constants() => ShouldSpecializeTo("print((1 + 2) * (3 + 4));", "print(21);");

    /// <summary>
    /// A função é chamada de propósito: um <c>def</c> puro que ninguém usa é
    /// eliminado como código morto, e aí não sobraria residual para conferir.
    /// </summary>
    [Fact]
    public void Partial_Fold() =>
        ShouldSpecializeTo(
            "def f = fn(x: Int) Int { return (1 + 2) + x; };\nprint(f(1));",
            "def f = fn(x: Int) Int { return 3 + x }; print(f(1));");

    /// <summary>O outro exemplo da spec §38: com operando dinâmico, nada muda.</summary>
    [Fact]
    public void Dynamic_Operand_IsNotFolded() =>
        ShouldSpecializeTo(
            "def f = fn(x: Int) Int { return x + 20; };\nprint(f(1));",
            "def f = fn(x: Int) Int { return x + 20 }; print(f(1));");

    [Fact]
    public void Comparison_Folds() => ShouldSpecializeTo("print(1 < 2);", "print(true);");

    [Fact]
    public void Equality_Folds() => ShouldSpecializeTo("print(1 == 1);", "print(true);");

    [Fact]
    public void StrConcat_Folds() => ShouldSpecializeTo("print(\"a\" + \"b\");", "print(\"ab\");");

    [Fact]
    public void Not_Folds() => ShouldSpecializeTo("print(!true);", "print(false);");

    [Fact]
    public void Negate_Folds() => ShouldSpecializeTo("def x = 5;\nprint(-x);", "print(-5);");

    /// <summary>
    /// Com a Q9 a divisão inteira é total: dividir por zero produz o maior <c>Int</c>
    /// em vez de abortar. É o que torna toda aritmética dobrável sem análise de
    /// efeito nenhuma.
    /// </summary>
    [Fact]
    public void DivisionByZero_Folds() =>
        ShouldSpecializeTo("print(1 / 0);", "print(9223372036854775807);");

    [Fact]
    public void Division_Folds() => ShouldSpecializeTo("print(10 / 2);", "print(5);");

    /// <summary>
    /// O PE dobra com as primitivas do runtime, não com aritmética própria: o
    /// resultado tem que ser bit a bit o do evaluator, inclusive em float.
    /// </summary>
    [Fact]
    public void Float_Folds_ExactlyLikeTheEvaluator()
    {
        var folded = Execute(Residual("print(0.1 + 0.2);"));
        var direct = Execute("print(0.1 + 0.2);");

        folded.Output.ShouldBe(direct.Output);
    }

    [Fact]
    public void Logical_And_ShortCircuits_Statically() =>
        ShouldSpecializeTo("print(false && true);", "print(false);");

    [Fact]
    public void Logical_Or_ShortCircuits_Statically() =>
        ShouldSpecializeTo("print(true || false);", "print(true);");

    /// <summary>Estatísticas contam o que foi feito, não o que se esperava fazer.</summary>
    [Fact]
    public void Statistics_CountFolds()
    {
        var result = Evaluate("print(1 + 2 + 3);");

        result.Statistics.ConstantsFolded.ShouldBe(2);
        result.Statistics.NodesAfter.ShouldBeLessThan(result.Statistics.NodesBefore);
    }

    /// <summary>Desligar a dobra desliga a dobra — é o que torna a flag útil num bisect.</summary>
    [Fact]
    public void ConstantFolding_CanBeDisabled()
    {
        var residual = Evaluate("print(10 + 20);", options: new PEOptions(ConstantFolding: false));

        Ast.Printing.CoreSourcePrinter.Print(residual.Residual).ShouldContain("10 + 20");
    }
}

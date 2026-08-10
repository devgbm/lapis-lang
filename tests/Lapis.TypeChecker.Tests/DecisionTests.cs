using Lapis.Ast.Types;
using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.TypeChecker.Tests;

/// <summary>
/// Trava as decisões do apêndice C que já são observáveis no M1. As demais
/// (Q1, Q2, Q3, Q5, Q6) dependem de sintaxe que chega no M3/M4.
/// </summary>
public sealed class DecisionTests : TypeCheckerTestBase
{
    // -------------------------------------------------- Q7: sem inferência

    /// <summary><c>print</c> não é genérico — senão exigiria `print&lt;Int&gt;(x)`.</summary>
    [Fact]
    public void Q7_PrintIsNotGeneric() => Natives.PrintSignature.IsGeneric.ShouldBeFalse();

    [Fact]
    public void Q7_PrintAcceptsAnyType()
    {
        ShouldPass("print(1);");
        ShouldPass("print(\"s\");");
        ShouldPass("print(true);");
        ShouldPass("print(());");
        ShouldPass("def f = fn() Int { return 1; }; print(f);");
    }

    /// <summary><c>Any</c> só existe em assinaturas de nativos, não na sintaxe.</summary>
    [Fact]
    public void Q7_AnyTypeIsNotWritableInSource() =>
        ShouldFailWith("def x: Any = 1;", DiagnosticCodes.UnknownType);

    [Fact]
    public void Q7_AnyAcceptsEverything_ButIsNotAssignableFrom()
    {
        // Um valor cabe em Any…
        TypeRelations.IsAssignableTo(PrimitiveType.Int, AnyType.Instance).ShouldBeTrue();

        // …mas Any não cabe num tipo concreto (nenhum valor tem tipo Any, então
        // isto nunca acontece na prática; a regra existe para não abrir brecha).
        TypeRelations.IsAssignableTo(AnyType.Instance, PrimitiveType.Int).ShouldBeFalse();
    }

    [Fact]
    public void Q7_PrintStillChecksArity() =>
        ShouldFailWith("print(1, 2);", DiagnosticCodes.ArgumentCountMismatch);

    // -------------------------------------------------- Q8: sem recursão

    [Fact]
    public void Q8_DirectRecursion_IsRejected() =>
        ShouldFailWith("def f = fn() Int { return f(); };", DiagnosticCodes.UnknownVariable);

    [Fact]
    public void Q8_MutualRecursion_IsRejected() =>
        ShouldFailWith(
            "def a = fn() Int { return b(); }; def b = fn() Int { return a(); };",
            DiagnosticCodes.UnknownVariable);

    // -------------------------------------------------- Q9: divisão total

    [Fact]
    public void Q9_DivisionByZero_IsNotATypeError() => ShouldPass("def x = 1 / 0;");

    [Fact]
    public void Q9_DivisionStaysInt() => TypeOfFirstDef("def x = 1 / 0;").ToDisplayString().ShouldBe("Int");

    /// <summary>Regressão: o código de divisão por zero foi aposentado, não reciclado.</summary>
    [Fact]
    public void Q9_Lap0301_IsRetired() =>
        typeof(DiagnosticCodes).GetFields()
            .Select(f => f.GetValue(null) as string)
            .ShouldNotContain("LAP0301");

    // -------------------------------------------------- Q4: operadores lógicos

    [Fact]
    public void Q4_LogicalOperatorsExist()
    {
        ShouldPass("def x = true && false;");
        ShouldPass("def x = true || false;");
        ShouldPass("def x = !true;");
    }
}

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

    // ------------------------------- Q34: recursão entra (revoga a Q8)

    [Fact]
    public void Q34_DirectRecursion_IsAllowed() =>
        ShouldPass("def f = fn(n: Int) Int { if n <= 0 { return 0; } return f(n - 1); };");

    /// <summary>
    /// O que a Q8 acertou e continua valendo: sem lambda não há assinatura de
    /// onde tirar o tipo, e o nome segue invisível na própria expressão que o
    /// define. A recursão vale para função, não para binding qualquer.
    /// </summary>
    [Fact]
    public void Q34_SelfReferenceOutsideALambda_IsStillRejected() =>
        ShouldFailWith("def x = x + 1;", DiagnosticCodes.UnknownVariable);

    /// <summary>Um `var` não recursa: o valor de hoje não é o de amanhã (Q25).</summary>
    [Fact]
    public void Q34_MutableBinding_DoesNotRecurse() =>
        ShouldFailWith("var f = fn() Int { return f(); };", DiagnosticCodes.UnknownVariable);

    /// <summary>Recursão **mútua** fica fora do escopo da Q34, e continua recusada.</summary>
    [Fact]
    public void Q34_MutualRecursion_IsStillRejected() =>
        ShouldFailWith(
            "def a = fn() Int { return b(); }; def b = fn() Int { return a(); };",
            DiagnosticCodes.UnknownVariable);

    /// <summary>Parâmetro homônimo sombreia a função, como qualquer binding interno.</summary>
    [Fact]
    public void Q34_ParameterShadowsTheFunction() =>
        TypeOfFirstDef("def f = fn(f: Int) Int { return f; };").ToDisplayString().ShouldBe("fn(Int) Int");

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

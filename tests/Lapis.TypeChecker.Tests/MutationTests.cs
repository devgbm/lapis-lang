using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

/// <summary>
/// <c>var</c> e reatribuição (Q25) — a mutação que destrava laço com progresso.
/// </summary>
public sealed class MutationTests : TypeCheckerTestBase
{
    [Fact]
    public void Var_IsDeclaredAndAssigned() =>
        ShouldPass("var x = 1;\nx = 2;\nprint(x);");

    [Fact]
    public void Var_WithAnnotation() =>
        ShouldPass("var x: Int = 1;\nx = 2;");

    [Fact]
    public void Def_IsNotAssignable() =>
        ShouldFailWith("def x = 1;\nx = 2;", DiagnosticCodes.NotAssignable);

    [Fact]
    public void Parameter_IsNotAssignable() =>
        ShouldFailWith(
            "def f = fn(a: Int) Int { a = 2; return a; };\nprint(f(1));",
            DiagnosticCodes.NotAssignable);

    [Fact]
    public void Assignment_ToUnknownName_IsLap0201() =>
        ShouldFailWith("y = 1;", DiagnosticCodes.UnknownVariable);

    /// <summary>O tipo do <c>var</c> é o da declaração e não muda.</summary>
    [Fact]
    public void Assignment_MustMatchTheDeclaredType() =>
        ShouldFailWith("var x = 1;\nx = \"texto\";", DiagnosticCodes.TypeMismatch);

    /// <summary>
    /// Atribuição é statement: ela não pode ser a cauda de um bloco, e o bloco que
    /// termina nela vale <c>Void</c>.
    /// </summary>
    [Fact]
    public void Assignment_IsVoid() =>
        TypeOfFirstDef("def x = { var a = 1; a = 2; };").ShouldBe(PrimitiveType.Void);

    // ------------------------------------------- não atravessa função

    /// <summary>
    /// A regra que dispensa decidir entre captura por valor e por referência:
    /// nenhuma função captura <c>var</c>. Sem aliasing, a closure continua sendo
    /// (código, ambiente imutável) para o partial evaluator.
    /// </summary>
    [Fact]
    public void Var_CannotBeReadInsideAFunction() =>
        ShouldFailWith(
            "var x = 1;\ndef f = fn() Int { return x; };\nprint(f());",
            DiagnosticCodes.MutableCapturedByFunction);

    [Fact]
    public void Var_CannotBeAssignedInsideAFunction() =>
        ShouldFailWith(
            "var x = 1;\ndef f = fn() Void { x = 2; };\nf();",
            DiagnosticCodes.MutableCapturedByFunction);

    /// <summary>Um <c>var</c> declarado dentro da função é perfeitamente normal.</summary>
    [Fact]
    public void Var_InsideItsOwnFunction_IsFine() =>
        ShouldPass("def f = fn() Int { var x = 1; x = 2; return x; };\nprint(f());");

    /// <summary>E um <c>def</c> de fora continua capturável — nada mudou para ele.</summary>
    [Fact]
    public void Def_IsStillCaptured() =>
        ShouldPass("def x = 1;\ndef f = fn() Int { return x; };\nprint(f());");

    /// <summary>Duas funções aninhadas: o <c>var</c> da de fora não vaza para a de dentro.</summary>
    [Fact]
    public void Var_DoesNotCrossNestedFunctions() =>
        ShouldFailWith(
            """
            def outer = fn() Int {
                var x = 1;
                def inner = fn() Int { return x; };
                return inner();
            };
            """,
            DiagnosticCodes.MutableCapturedByFunction);

    // -------------------------------------------------- const generics

    /// <summary>
    /// Q18 exige que um argumento const seja resolvível em tempo de compilação.
    /// Um <c>var</c> nunca é: o valor de hoje não é o de amanhã.
    /// </summary>
    [Fact]
    public void Var_IsNotAConstGenericArgument() =>
        ShouldFailWith(
            "var n = 3;\ndef f = fn<N: Int>(x: Int) Int { return x * N; };\nprint(f<n>(2));",
            DiagnosticCodes.GenericArgumentNotConstant);

    /// <summary>Um <c>def</c> ligado a literal continua servindo.</summary>
    [Fact]
    public void Def_IsStillAConstGenericArgument() =>
        ShouldPass("def n = 3;\ndef f = fn<N: Int>(x: Int) Int { return x * N; };\nprint(f<n>(2));");

    // ------------------------------------------------------- escopo

    [Fact]
    public void Var_MayBeShadowedInAnInnerBlock() =>
        ShouldPass("var x = 1;\ndef bloco = { var x = 2; x };\nprint(bloco);\nprint(x);");

    [Fact]
    public void Var_DuplicateInSameBlock_IsLap0202() =>
        ShouldFailWith("var x = 1;\nvar x = 2;", DiagnosticCodes.DuplicateDefinition);

    /// <summary>
    /// Um <c>var</c> não vira constante nem quando o valor inicial é literal — é o
    /// que impede o partial evaluator de propagar algo que muda depois.
    /// </summary>
    [Fact]
    public void Var_MayBeAssignedFromAnExpression() =>
        ShouldPass("var x = 1;\ndef y = 2;\nx = y + 1;\nprint(x);");
}

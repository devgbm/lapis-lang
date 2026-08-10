using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

/// <summary>Tipagem e escopo de <c>goto</c>/<c>label</c> (plano 16 §16.5).</summary>
public sealed class GotoTests : TypeCheckerTestBase
{
    /// <summary>
    /// <c>Never</c> se propaga por <c>Let</c> desde o M1, então um salto cabe em
    /// qualquer posição sem regra nova — é o mesmo mecanismo de <c>return</c>.
    /// </summary>
    [Fact]
    public void Goto_IsNever_AndTheBlockStillHasAValue() =>
        TypeOfFirstDef("def x: Int = { goto fim; label fim; 1 };")
            .ShouldBe(PrimitiveType.Int);

    [Fact]
    public void Goto_UnknownLabel() =>
        ShouldFailWith("goto inexistente;", DiagnosticCodes.UnknownLabel);

    [Fact]
    public void Goto_Backward_IsAllowed() =>
        ShouldPass("label a;\nprint(1);\ngoto a if false;");

    /// <summary>Um salto de um join para outro do mesmo grupo é legítimo.</summary>
    [Fact]
    public void Goto_BetweenJoins_IsAllowed() =>
        ShouldPass("goto a;\nlabel a;\ngoto b;\nlabel b;\nprint(1);");

    /// <summary>
    /// Um rótulo é local à função, como <c>return</c>. Saber que ele existe lá
    /// fora é o que separa este diagnóstico de "o rótulo não existe".
    /// </summary>
    [Fact]
    public void Goto_DoesNotCrossFunctionBoundary() =>
        ShouldFailWith(
            "label fora;\ndef f = fn() Void { goto fora; };\nf();",
            DiagnosticCodes.LabelOutOfScope);

    [Fact]
    public void Goto_ToNonexistentLabelInsideFunction_IsUnknown() =>
        ShouldFailWith(
            "def f = fn() Void { goto inexistente; };\nf();",
            DiagnosticCodes.UnknownLabel);

    [Fact]
    public void Label_Duplicate() =>
        ShouldFailWith("goto a;\nlabel a;\nlabel a;", DiagnosticCodes.DuplicateLabel);

    [Fact]
    public void GotoIf_ConditionMustBeBool() =>
        ShouldFailWith("goto a if 1;\nlabel a;", DiagnosticCodes.ConditionMustBeBool);

    /// <summary>
    /// O valor do grupo vem do último join: todo segmento anterior termina em
    /// salto, e salto é <c>Never</c>. É por isso que a junção de tipos aqui nunca
    /// pode falhar — só há um caminho que produz valor.
    /// </summary>
    [Fact]
    public void Labeled_TakesItsTypeFromTheLastJoin() =>
        TypeOfFirstDef("def x = { goto fim; label fim; \"texto\" };")
            .ShouldBe(PrimitiveType.Str);

    // ---------------------------------------------------------- escopo

    /// <summary>O que veio antes do salto continua visível no destino.</summary>
    [Fact]
    public void ScopeAfterLabel_KeepsEarlierDefinitions() =>
        ShouldPass("def x = 1;\ngoto fim;\nlabel fim;\nprint(x);");

    /// <summary>
    /// O que foi declarado entre o salto e o rótulo, não: o salto pode tê-lo
    /// pulado, então usá-lo no destino é a variável não existir.
    /// </summary>
    [Fact]
    public void ScopeAfterLabel_ExcludesSkippedDefinitions() =>
        ShouldFailWith(
            "goto fim;\ndef y = 2;\nlabel fim;\nprint(y);",
            DiagnosticCodes.UnknownVariable);

    /// <summary>Rótulos e valores são espaços de nomes separados.</summary>
    [Fact]
    public void LabelAndVariable_MayShareAName() =>
        ShouldPass("def a = 1;\ngoto a;\nlabel a;\nprint(a);");

    // -------------------------------------------------- análise de return

    /// <summary>
    /// <c>DR(Goto L)</c> é <c>DR(L)</c>: o salto não retorna, quem retorna é o
    /// destino. Sem isso esta função — que sempre retorna — seria rejeitada.
    /// </summary>
    [Fact]
    public void MissingReturn_FollowsTheJump() =>
        ShouldPass("def f = fn() Int { goto fim; label fim; return 1; };");

    /// <summary>E se o destino não retorna, a função não retorna.</summary>
    [Fact]
    public void MissingReturn_SeesJoinsThatDoNotReturn() =>
        ShouldFailWith(
            "def f = fn(n: Int) Int { goto fim if n > 0; return 0; label fim; print(n); };",
            DiagnosticCodes.MissingReturn);

    /// <summary>
    /// Com salto para trás os joins se referenciam em ciclo; a análise assume
    /// <c>true</c> para o que ainda não visitou e itera até estabilizar. Um laço
    /// que nunca sai nunca cai fora da função — ele repete, ou aborta.
    /// </summary>
    [Fact]
    public void MissingReturn_ReachesFixpointOnCycles() =>
        ShouldPass("def f = fn() Int { label a; goto a; };");

    /// <summary>
    /// O salto implícito que fecha um segmento não é código escrito por ninguém:
    /// depois dele vem um <c>label</c>, que é alcançável.
    /// </summary>
    [Fact]
    public void UnreachableAfterReturn_IgnoresTheImplicitJump() =>
        Codes("def f = fn(c: Bool) Int { goto fim if c; return 0; label fim; return 1; };")
            .ShouldNotContain(DiagnosticCodes.UnreachableAfterReturn);

    /// <summary>Mas um salto escrito depois de um <c>return</c> continua sendo aviso.</summary>
    [Fact]
    public void UnreachableAfterReturn_StillWarnsForWrittenCode() =>
        Codes("def f = fn() Int { goto fim; label fim; return 1; print(2); };")
            .ShouldContain(DiagnosticCodes.UnreachableAfterReturn);
}

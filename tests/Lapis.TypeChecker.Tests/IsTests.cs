using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

/// <summary>
/// <c>is</c>: testar variante e desembrulhar carga (plano 25, M16 — fecha Q23).
///
/// <c>is</c> é primitiva da Core (<c>CoreIs</c>, §25.2), e é aqui que a variante
/// é <b>resolvida</b>: o desugar só carregou o que estava escrito, e quem sabe a
/// qual enum ela pertence é o tipo do escrutinado. Por isso <c>LAP0732</c>–<c>LAP0734</c>
/// são diagnósticos deste projeto, e não do desugar.
/// </summary>
public sealed class IsTests : TypeCheckerTestBase
{
    // -------------------------------------------------- forma sem ligação

    [Fact]
    public void Is_ProducesBool() =>
        TypeOfDef("def e = Option<Int>.Some(1);\ndef b = e is Some;", "b").ShouldBe(PrimitiveType.Bool);

    [Fact]
    public void Is_Qualified() =>
        TypeOfDef("def e = Option<Int>.Some(1);\ndef b = e is Option.Some;", "b").ShouldBe(PrimitiveType.Bool);

    /// <summary>
    /// Sem dono escrito, resolvido contra o prelúdio (plano 25 §25.5) — o caso
    /// de uso central da forma sem qualificação: "Some"/"None" já são únicos
    /// entre as variantes que o prelúdio declara.
    /// </summary>
    [Fact]
    public void Is_UnqualifiedResolvesAgainstPrelude() =>
        TypeOfDef("def e = Option<Int>.Some(1);\ndef b = e is Some;", "b").ShouldBe(PrimitiveType.Bool);

    [Fact]
    public void Is_UnknownVariant() =>
        ShouldFailWith("def e = Option<Int>.Some(1);\ndef b = e is Nada;", DiagnosticCodes.UnknownIsVariant);

    [Fact]
    public void Is_QualifiedWithWrongOwner() =>
        ShouldFailWith("def e = Option<Int>.Some(1);\ndef b = e is Result.Some;", DiagnosticCodes.UnknownIsVariant);

    [Fact]
    public void Is_OnNonEnum() =>
        Codes("def b = 1 is Some;").ShouldNotBeEmpty();

    [Fact]
    public void Is_InLoopBody() =>
        ShouldPass("def e = Option<Int>.Some(1);\nloop { if e is Some(v) { print(v); } break; }");

    // `a is P is Q` não encadeia (LAP0114) — é diagnóstico de parser, coberto em
    // Lapis.Parser.Tests.IsParseTests.DoesNotChain_ReportsLap0114; este harness
    // exige parse limpo (CheckWithDiagnostics) e não serve para testá-lo aqui.

    // -------------------------------------------------- ligação e escopo

    [Fact]
    public void Bind_InIfThen() =>
        TypeOfDef("def e = Option<Int>.Some(1);\ndef x = if e is Some(v) { v + 1 } else { 0 };", "x")
            .ShouldBe(PrimitiveType.Int);

    [Fact]
    public void Bind_NotInElse() =>
        ShouldFailWith("def e = Option<Int>.Some(1);\nif e is Some(v) { 1 } else { v };", DiagnosticCodes.UnknownVariable);

    [Fact]
    public void Bind_NotAfterTheIf() =>
        ShouldFailWith("def e = Option<Int>.Some(1);\nif e is Some(v) { 1 };\nprint(v);", DiagnosticCodes.UnknownVariable);

    [Fact]
    public void Bind_InRightOfAnd() =>
        ShouldPass("def e = Option<Int>.Some(1);\ndef b = e is Some(v) && v == 1;");

    [Fact]
    public void Bind_InPlainDef() =>
        ShouldFailWith("def e = Option<Int>.Some(1);\ndef b = e is Some(v);", DiagnosticCodes.IsBindingRequiresIfOrAnd);

    [Fact]
    public void Bind_InLoopViaIf() =>
        ShouldPass("def e = Option<Int>.Some(1);\nloop { if e is Some(v) { print(v); break; } else { break; } }");

    [Fact]
    public void Bind_NullaryVariant() =>
        ShouldFailWith(
            "def e = Option<Int>.None;\nif e is None(v) { print(v); };",
            DiagnosticCodes.IsVariantHasNoPayload);

    [Fact]
    public void Bind_MultiPayload() =>
        ShouldFailWith(
            "def T = enum { Pair(Int, Int) };\ndef e = T.Pair(1, 2);\nif e is Pair(v) { print(v); };",
            DiagnosticCodes.IsVariantHasMultiplePayloads);

    /// <summary>Sombreamento é permitido — sem diagnóstico algum.</summary>
    [Fact]
    public void Bind_Shadows() =>
        ShouldPass("def v = 1;\ndef e = Option<Int>.Some(2);\nif e is Some(v) { print(v); };\nprint(v);");

    // -------------------------------------------------- genéricos e integração

    [Fact]
    public void Is_WithGenericOwner() =>
        TypeOfDef(
                "def r: Result<Int, Str> = Result<Int, Str>.Ok(1);\ndef x = if r is Result<Int, ?>.Ok(v) { v } else { 0 };",
                "x")
            .ShouldBe(PrimitiveType.Int);

    /// <summary>
    /// <c>is</c> e o <c>match</c> equivalente dão o mesmo tipo. Não é mais a
    /// mesma <b>árvore</b> — <c>is</c> é primitiva desde o M16 (Q22/Q23) —, e é
    /// justamente por isso que a equivalência precisa ser afirmada em vez de
    /// decorrer da construção.
    /// </summary>
    [Fact]
    public void Is_AgreesWithTheEquivalentMatch()
    {
        var isForm = TypeOfDef(
            "def e = Option<Int>.Some(1);\ndef x = if e is Option.Some(v) { v } else { 0 };", "x");

        var matchForm = TypeOfDef(
            "def e = Option<Int>.Some(1);\ndef x = match e { Option.Some(v) => v, Option.None => 0 };", "x");

        isForm.ShouldBe(matchForm);
    }

    /// <summary>
    /// <c>is</c> não é exaustivo, e não deve ser: ele testa <b>uma</b> variante e
    /// o resto cai no ramo falso. É a divisa com <c>match</c> (Q22) — e o que
    /// permite a <c>match</c> um dia sair do compilador sobre esta primitiva.
    /// </summary>
    [Fact]
    public void Is_IsNotExhaustive_AndThatIsFine() =>
        ShouldPass("def e = Option<Int>.Some(1);\nif e is Some(v) { print(v); };");
}

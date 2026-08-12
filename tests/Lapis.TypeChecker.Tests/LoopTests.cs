using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

/// <summary>
/// Tipagem e escopo de <c>loop</c>/<c>break</c>/<c>continue</c> (plano 26, M16 —
/// Q32). Substitui <c>GotoTests</c> (plano 16 §16.5, retirado).
/// </summary>
public sealed class LoopTests : TypeCheckerTestBase
{
    /// <summary>
    /// <c>break</c>/<c>continue</c> são <c>Never</c> — mesmo mecanismo de
    /// <c>return</c>/<c>throw</c> (Q13), sem regra de tipo nova.
    /// </summary>
    [Fact]
    public void Break_IsNever_AndTheBlockStillHasAValue() =>
        TypeOfFirstDef("def x: Int = { loop { if true { break; } } 1 };")
            .ShouldBe(PrimitiveType.Int);

    // -------------------------------------------------- tipo do loop

    [Fact]
    public void Loop_WithoutBreak_IsNever() =>
        TypeOfFirstDef("def x = loop { print(1); };").ShouldBeOfType<NeverType>();

    [Fact]
    public void Loop_WithBreak_IsTheJoinOfBreakValues() =>
        TypeOfFirstDef("def x = loop { break 1; };").ShouldBe(PrimitiveType.Int);

    [Fact]
    public void Loop_BareBreak_IsVoid() =>
        TypeOfFirstDef("def x = loop { break; };").ShouldBe(PrimitiveType.Void);

    [Fact]
    public void Loop_MultipleBreaks_Join() =>
        TypeOfFirstDef("def x = loop { if true { break 1; } break 2; };").ShouldBe(PrimitiveType.Int);

    [Fact]
    public void Loop_IncompatibleBreaks_ReportLap0526() =>
        ShouldFailWith(
            "def x = loop { if true { break 1; } break \"a\"; };",
            DiagnosticCodes.IncompatibleBreakValues);

    /// <summary><c>continue</c> não contribui para o tipo do <c>loop</c>.</summary>
    [Fact]
    public void Loop_ContinueDoesNotJoinIntoTheType() =>
        TypeOfFirstDef("def x = loop { continue; break 1; };").ShouldBe(PrimitiveType.Int);

    // -------------------------------------------------- escopo do rótulo

    [Fact]
    public void Break_OutsideLoop_ReportsLap0523() =>
        ShouldFailWith("break;", DiagnosticCodes.BreakOrContinueOutsideLoop);

    [Fact]
    public void Continue_OutsideLoop_ReportsLap0523() =>
        ShouldFailWith("continue;", DiagnosticCodes.BreakOrContinueOutsideLoop);

    [Fact]
    public void Break_UnknownLabel_ReportsLap0524() =>
        ShouldFailWith("loop { break :inexistente; };", DiagnosticCodes.UnknownLoopLabel);

    [Fact]
    public void Break_LabelOfOuterFunction_ReportsLap0525() =>
        ShouldFailWith(
            "loop :fora { def f = fn() Void { break :fora; }; f(); };",
            DiagnosticCodes.LoopLabelOutOfScope);

    /// <summary>Sem rótulo, alcança o laço mais próximo.</summary>
    [Fact]
    public void Break_Unlabeled_TargetsTheNearestLoop() =>
        ShouldPass("loop { loop { break; } break; };");

    /// <summary>Com rótulo, alcança o laço que o declara, por cima de quantos for preciso.</summary>
    [Fact]
    public void Break_Labeled_TargetsNamedLoop() =>
        TypeOfFirstDef("def x = loop :fora { loop { break :fora, 1; } };").ShouldBe(PrimitiveType.Int);

    /// <summary>Sombreamento é permitido — sem diagnóstico de rótulo duplicado.</summary>
    [Fact]
    public void Loop_Shadowing_IsAllowed() =>
        ShouldPass("loop :x { loop :x { break :x; } break; };");

    [Fact]
    public void Continue_Labeled_TargetsNamedLoop() =>
        ShouldPass("loop :fora { loop { continue :fora; } break; };");

    // -------------------------------------------------- análise de return

    /// <summary>
    /// <c>DR(Loop) = true</c> quando nenhum <c>break</c> o alcança: o código
    /// depois é inalcançável, e não precisa de <c>return</c> próprio.
    /// </summary>
    [Fact]
    public void MissingReturn_LoopWithoutBreak_SatisfiesDR() =>
        ShouldPass("def f = fn() Int { loop { print(1); } };");

    [Fact]
    public void MissingReturn_LoopWithBreak_DoesNotSatisfyDR() =>
        ShouldFailWith(
            "def f = fn() Int { loop { if true { break; } } };",
            DiagnosticCodes.MissingReturn);

    [Fact]
    public void MissingReturn_ReturnInsideLoop_Satisfies() =>
        ShouldPass("def f = fn() Int { loop { return 1; } };");

    /// <summary>
    /// Código depois de um <c>loop {}</c> sem <c>break</c> é genuinamente
    /// inalcançável — o laço, se termina, só termina por <c>return</c>/<c>throw</c>
    /// ou por abortar no orçamento (<c>LAP0303</c>); nunca caindo pelo fim. O
    /// aviso aqui é o correto, não um falso positivo.
    /// </summary>
    [Fact]
    public void UnreachableAfterReturn_FiresForCodeAfterLoopWithoutBreak() =>
        Codes("def f = fn() Int { def x = loop { print(1); }; print(2); };")
            .ShouldContain(DiagnosticCodes.UnreachableAfterReturn);

    [Fact]
    public void UnreachableAfterReturn_StillWarnsForCodeAfterReturn() =>
        Codes("def f = fn() Int { return 1; print(2); };")
            .ShouldContain(DiagnosticCodes.UnreachableAfterReturn);
}

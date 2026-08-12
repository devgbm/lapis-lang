using Lapis.Ast;
using Lapis.Ast.Core;
using Lapis.Diagnostics;

namespace Lapis.Desugar.Tests;

/// <summary>
/// <c>is</c>: açúcar sobre <c>match</c> (plano 25, M16 — fecha Q23). Zero nós
/// novos na Core — tudo aqui vira <see cref="CoreMatch"/> com
/// <see cref="CoreVariantPattern"/>, exatamente como um <c>match</c> escrito à
/// mão.
///
/// O dono é declarado no próprio arquivo em vez de vir do prelúdio: o desugar
/// resolve a forma sem qualificação (§25.5) contra o que está sintaticamente
/// visível, e <c>DesugarTestBase.Compile</c> não carrega o prelúdio.
/// </summary>
public sealed class IsDesugarTests : DesugarTestBase
{
    private const string Enum = "def X = enum { A, B(Int) };\n";

    [Fact]
    public void NoBinding_Unqualified_DesugarsToMatch()
    {
        var match = AllNodes(Compile(Enum + "def e = X.A;\ndef r = e is A;")).OfType<CoreMatch>().ShouldHaveSingleItem();

        match.Arms.Length.ShouldBe(2);
        var variant = match.Arms[0].Pattern.ShouldBeOfType<CoreVariantPattern>();
        variant.EnumName.ShouldBe("X");
        variant.VariantName.ShouldBe("A");
        match.Arms[1].Pattern.ShouldBeOfType<CoreWildcardPattern>();
    }

    /// <summary>
    /// Sem ligação, a carga é ignorada — mas o padrão ainda precisa de um
    /// coringa por posição, porque <c>match</c> exige aridade exata (LAP0264).
    /// <c>e is B</c> vira o mesmo que <c>X.B(_)</c>, nunca <c>X.B()</c>.
    /// </summary>
    [Fact]
    public void NoBinding_OnPayloadVariant_UsesWildcardForThePayload()
    {
        var match = AllNodes(Compile(Enum + "def e = X.B(1);\ndef r = e is B;")).OfType<CoreMatch>().ShouldHaveSingleItem();

        var variant = match.Arms[0].Pattern.ShouldBeOfType<CoreVariantPattern>();
        variant.Arguments.Length.ShouldBe(1);
        variant.Arguments[0].ShouldBeOfType<CoreWildcardPattern>();
    }

    [Fact]
    public void Qualified_UsesTheWrittenOwner()
    {
        var match = AllNodes(Compile(Enum + "def e = X.A;\ndef r = e is X.A;")).OfType<CoreMatch>().ShouldHaveSingleItem();

        match.Arms[0].Pattern.ShouldBeOfType<CoreVariantPattern>().EnumName.ShouldBe("X");
    }

    [Fact]
    public void Bind_InIfCondition_ArmBindsTheValue()
    {
        var match = AllNodes(Compile(Enum + "def e = X.B(1);\nif e is B(v) { print(v); };"))
            .OfType<CoreMatch>().ShouldHaveSingleItem();

        var variant = match.Arms[0].Pattern.ShouldBeOfType<CoreVariantPattern>();
        variant.Arguments.ShouldHaveSingleItem().ShouldBeOfType<CoreBindingPattern>().Name.ShouldBe("v");

        // O corpo do braço é o `then` do `if` — é isso que dá escopo a `v`.
        var call = match.Arms[0].Body.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreCall>();
        call.Arguments.ShouldHaveSingleItem().ShouldBeOfType<CoreVariable>().Name.ShouldBe("v");
    }

    /// <summary><c>if</c> sem <c>else</c>: o braço do coringa é <c>()</c>, como todo <c>if</c> sem <c>else</c>.</summary>
    [Fact]
    public void Bind_InIfWithoutElse_WildcardArmIsUnit()
    {
        var match = AllNodes(Compile(Enum + "def e = X.B(1);\nif e is B(v) { print(v); };"))
            .OfType<CoreMatch>().ShouldHaveSingleItem();

        match.Arms[1].Body.ShouldBeOfType<CoreLiteral>().Value.ShouldBeOfType<ConstUnit>();
    }

    [Fact]
    public void Bind_LeftOfAndAlso_ThreadsIntoTheRightOperand()
    {
        var match = AllNodes(Compile(Enum + "def e = X.B(1);\ndef r = e is B(v) && v == 1;"))
            .OfType<CoreMatch>().ShouldHaveSingleItem();

        // O braço da variante é o operando direito desugarado — `v == 1`, com
        // `v` em escopo —, e o braço do coringa é `false`: a mesma forma que
        // `&&` sempre produziu, só que agora escolhida por `is`.
        match.Arms[0].Body.ShouldBeOfType<CoreBinary>().Operator.ShouldBe(BinaryOperator.Equal);
        match.Arms[1].Body.ShouldBeOfType<CoreLiteral>().Value.ShouldBe(ConstBool.False);
    }

    /// <summary>
    /// A composição do §25.3: <c>is</c> seguido de <c>&amp;&amp;</c>, dentro de
    /// um <c>if</c>. A ligação atravessa as duas camadas.
    /// </summary>
    [Fact]
    public void Bind_IfWithAndAlsoCondition_ThreadsAllTheWayIn()
    {
        var match = AllNodes(Compile(Enum + "def e = X.B(1);\nif e is B(v) && v == 1 { print(v); };"))
            .OfType<CoreMatch>().ShouldHaveSingleItem();

        var innerIf = match.Arms[0].Body.ShouldBeOfType<CoreIf>();
        innerIf.Then.ShouldBeOfType<CoreLet>();
    }

    [Fact]
    public void OutsideBindingPosition_ReportsLap0730() =>
        CompileCodes(Enum + "def e = X.B(1);\ndef r = e is B(v);")
            .ShouldContain(DiagnosticCodes.IsBindingRequiresIfOrAnd);

    [Fact]
    public void UnknownVariant_ReportsLap0732() =>
        CompileCodes(Enum + "def e = X.A;\ndef r = e is Nada;").ShouldContain(DiagnosticCodes.UnknownIsVariant);

    [Fact]
    public void UnknownOwner_ReportsLap0732() =>
        CompileCodes(Enum + "def e = X.A;\ndef r = e is Y.A;").ShouldContain(DiagnosticCodes.UnknownIsVariant);

    [Fact]
    public void AmbiguousUnqualifiedVariant_ReportsLap0732() =>
        CompileCodes("def X = enum { A };\ndef Y = enum { A };\ndef e = X.A;\ndef r = e is A;")
            .ShouldContain(DiagnosticCodes.UnknownIsVariant);

    [Fact]
    public void NullaryVariantWithBinding_ReportsLap0733() =>
        CompileCodes(Enum + "def e = X.A;\nif e is A(v) { print(v); };").ShouldContain(DiagnosticCodes.IsVariantHasNoPayload);

    [Fact]
    public void MultiPayloadVariantWithBinding_ReportsLap0734() =>
        CompileCodes("def X = enum { Pair(Int, Int) };\ndef e = X.Pair(1, 2);\nif e is Pair(v) { print(v); };")
            .ShouldContain(DiagnosticCodes.IsVariantHasMultiplePayloads);

    [Fact]
    public void Is_RoundTrips() =>
        PrintSource(Enum + "def e = X.B(1);\nif e is B(v) { print(v); };").ShouldContain("match");
}

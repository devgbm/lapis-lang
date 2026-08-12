using Lapis.Ast;
using Lapis.Ast.Core;
using Lapis.Diagnostics;

namespace Lapis.Desugar.Tests;

/// <summary>
/// <c>is</c> como primitiva da Core (plano 25, M16 — fecha Q23): o desugar
/// produz <see cref="CoreIs"/>, não <see cref="CoreMatch"/>.
///
/// O desugar é <b>puramente sintático</b> aqui — ele carrega o que foi escrito e
/// decide só a <i>posição</i> (§25.3, <c>LAP0730</c>). Quem resolve a variante,
/// confere o dono e a aridade da carga é o checker, porque essas são perguntas
/// sobre o tipo do escrutinado — ver <c>Lapis.TypeChecker.Tests.IsTests</c>.
/// </summary>
public sealed class IsDesugarTests : DesugarTestBase
{
    private const string Enum = "def X = enum { A, B(Int) };\n";

    private static CoreIs SingleIs(string source) =>
        AllNodes(Compile(source)).OfType<CoreIs>().ShouldHaveSingleItem();

    [Fact]
    public void NoBinding_DesugarsToCoreIs()
    {
        var node = SingleIs(Enum + "def e = X.A;\ndef r = e is A;");

        node.VariantName.ShouldBe("A");
        node.OwnerName.ShouldBeNull();
        node.BindingName.ShouldBeNull();
    }

    /// <summary>
    /// A forma sem ligação é o mesmo nó com ramos literais — é o que a torna
    /// <c>Bool</c> em qualquer posição sem regra nova.
    /// </summary>
    [Fact]
    public void NoBinding_BranchesAreTrueAndFalse()
    {
        var node = SingleIs(Enum + "def e = X.A;\ndef r = e is A;");

        node.Then.ShouldBeOfType<CoreLiteral>().Value.ShouldBe(ConstBool.True);
        node.Else.ShouldBeOfType<CoreLiteral>().Value.ShouldBe(ConstBool.False);
    }

    /// <summary>Sem ligação, a carga simplesmente não é olhada — não há coringa a preencher.</summary>
    [Fact]
    public void NoBinding_OnPayloadVariant_HasNoBinding() =>
        SingleIs(Enum + "def e = X.B(1);\ndef r = e is B;").BindingName.ShouldBeNull();

    [Fact]
    public void Qualified_KeepsTheWrittenOwner() =>
        SingleIs(Enum + "def e = X.A;\ndef r = e is X.A;").OwnerName.ShouldBe("X");

    /// <summary>
    /// Numa posição que liga, os ramos do <c>if</c> viram os ramos do nó — é
    /// isso que dá escopo a <c>v</c> sem análise de fluxo nenhuma.
    /// </summary>
    [Fact]
    public void Bind_InIfCondition_ThenBranchIsTheIfBody()
    {
        var node = SingleIs(Enum + "def e = X.B(1);\nif e is B(v) { print(v); };");

        node.BindingName.ShouldBe("v");

        var call = node.Then.ShouldBeOfType<CoreLet>().Value.ShouldBeOfType<CoreCall>();
        call.Arguments.ShouldHaveSingleItem().ShouldBeOfType<CoreVariable>().Name.ShouldBe("v");
    }

    /// <summary><c>if</c> sem <c>else</c>: o ramo falso é <c>()</c>, como todo <c>if</c> sem <c>else</c>.</summary>
    [Fact]
    public void Bind_InIfWithoutElse_ElseBranchIsUnit() =>
        SingleIs(Enum + "def e = X.B(1);\nif e is B(v) { print(v); };")
            .Else.ShouldBeOfType<CoreLiteral>().Value.ShouldBeOfType<ConstUnit>();

    [Fact]
    public void Bind_LeftOfAndAlso_ThreadsIntoTheRightOperand()
    {
        var node = SingleIs(Enum + "def e = X.B(1);\ndef r = e is B(v) && v == 1;");

        // O ramo verdadeiro é o operando direito — com `v` em escopo —, e o
        // falso é `false`: a mesma forma que `&&` sempre produziu, agora
        // escolhida por `is`.
        node.Then.ShouldBeOfType<CoreBinary>().Operator.ShouldBe(BinaryOperator.Equal);
        node.Else.ShouldBeOfType<CoreLiteral>().Value.ShouldBe(ConstBool.False);
    }

    /// <summary>A composição do §25.3: a ligação atravessa o <c>&amp;&amp;</c> e o <c>if</c>.</summary>
    [Fact]
    public void Bind_IfWithAndAlsoCondition_ThreadsAllTheWayIn() =>
        SingleIs(Enum + "def e = X.B(1);\nif e is B(v) && v == 1 { print(v); };")
            .Then.ShouldBeOfType<CoreIf>().Then.ShouldBeOfType<CoreLet>();

    /// <summary>Zero nós de <c>match</c>: <c>is</c> não passa mais por lá.</summary>
    [Fact]
    public void Is_ProducesNoMatchNode() =>
        AllNodes(Compile(Enum + "def e = X.B(1);\nif e is B(v) { print(v); };"))
            .OfType<CoreMatch>().ShouldBeEmpty();

    // -------------------------------------------------- diagnóstico do desugar

    /// <summary>
    /// O único que sai daqui: posição é sintaxe, e o desugar sabe responder.
    /// </summary>
    [Fact]
    public void OutsideBindingPosition_ReportsLap0730() =>
        CompileCodes(Enum + "def e = X.B(1);\ndef r = e is B(v);")
            .ShouldContain(DiagnosticCodes.IsBindingRequiresIfOrAnd);

    /// <summary>
    /// E o desugar <b>não</b> tenta resolver variante: um nome que não existe
    /// atravessa daqui em silêncio e morre no checker (<c>LAP0732</c>).
    /// </summary>
    [Fact]
    public void UnknownVariant_IsNotTheDesugarsProblem() =>
        CompileCodes(Enum + "def e = X.A;\ndef r = e is Nada;")
            .ShouldNotContain(DiagnosticCodes.UnknownIsVariant);

    // -------------------------------------------------- round-trip

    /// <summary>
    /// O teste puro volta como <c>e is V</c>, e não como o <c>if</c> que ele
    /// significa — imprimi-lo com <c>if</c> reparsearia numa árvore diferente
    /// (um <c>If</c> envolvendo um <c>Is</c>).
    /// </summary>
    [Fact]
    public void NoBinding_PrintsAsTheShortForm() =>
        PrintSource(Enum + "def e = X.A;\ndef r = e is A;").ShouldContain("e is A");

    [Fact]
    public void Bind_PrintsAsTheIfForm() =>
        PrintSource(Enum + "def e = X.B(1);\nif e is B(v) { print(v); };")
            .ShouldContain("if e is B(v) {");

    [Theory]
    [InlineData("def r = e is A;")]
    [InlineData("def r = e is X.A;")]
    [InlineData("if e is B(v) { print(v); };")]
    [InlineData("if e is B(v) { print(v); } else { print(0); }")]
    [InlineData("def r = e is B(v) && v == 1;")]
    [InlineData("def r = !(e is A);")]
    public void Is_RoundTrips(string tail)
    {
        var source = Enum + "def e = X.B(1);\n" + tail;

        var original = Print(source);
        var printed = PrintSource(source);

        Print(printed).ShouldBe(original, $"código impresso:\n{printed}");
    }
}

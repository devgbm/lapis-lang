using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

/// <summary>
/// <c>def T.m = e;</c> — plano 21.
///
/// A resolução é <b>type checking</b>, não uma fase de lowering: o checker é onde
/// a informação de tipo existe, e o resultado é uma <c>Resolution</c> como as
/// outras. A Core não ganhou nó nenhum.
/// </summary>
public sealed class MemberDeclarationTests : TypeCheckerTestBase
{
    private const string User = "def User = type { name: Str; age: Int; };\n";

    [Fact]
    public void Member_Value_IsAccessible() =>
        TypeOfDef(User + "def User.defaultAge = 30;\ndef n = User.defaultAge;", "n")
            .ShouldBe(PrimitiveType.Int);

    [Fact]
    public void Member_Function_IsCallable() =>
        ShouldPass(User + """
            def User.create = fn(nome: Str) User {
                return .User { name: nome, age: 0 };
            };

            def u: User = User.create("g");
            """);

    /// <summary>
    /// Um membro e um <c>def</c> comum de mesmo nome são símbolos distintos: o
    /// segundo só é alcançável através do tipo.
    /// </summary>
    [Fact]
    public void Member_DoesNotCollideWithAPlainDef() =>
        ShouldPass(User + "def User.hello = 1;\ndef hello = \"outro\";");

    [Fact]
    public void Member_OnEnum_IsAccessible() =>
        TypeOfDef("def Color = enum { Red };\ndef Color.total = 1;\ndef n = Color.total;", "n")
            .ShouldBe(PrimitiveType.Int);

    /// <summary>Variante continua vencendo — e um membro homônimo nem chega a existir.</summary>
    [Fact]
    public void Enum_Variant_StillResolves() =>
        ShouldPass("def Color = enum { Red };\ndef Color.total = 1;\ndef c: Color = Color.Red;");

    [Fact]
    public void UnknownMember_ReportsLap0701() =>
        ShouldFailWith(User + "def n = User.naoExiste;", DiagnosticCodes.UnknownMember);

    /// <summary>Num enum, o nome que falta é uma **variante** — a mensagem diz isso.</summary>
    [Fact]
    public void UnknownMember_OnEnum_ReportsLap0251() =>
        ShouldFailWith(
            "def Color = enum { Red };\ndef c = Color.naoExiste;",
            DiagnosticCodes.UnknownVariant);

    [Fact]
    public void DuplicateMember_ReportsLap0702() =>
        ShouldFailWith(
            User + "def User.hello = 1;\ndef User.hello = 2;",
            DiagnosticCodes.DuplicateMember);

    /// <summary>
    /// Blocos distintos escapam da checagem sintática do desugar, então o checker
    /// guarda a mesma regra.
    /// </summary>
    [Fact]
    public void DuplicateMember_AcrossBlocks_ReportsLap0702() =>
        ShouldFailWith(
            User + "def User.hello = 1;\ndef x = { def User.hello = 2; 1 };",
            DiagnosticCodes.DuplicateMember);

    [Fact]
    public void MemberShadowingVariant_ReportsLap0703() =>
        ShouldFailWith(
            "def Color = enum { Red };\ndef Color.Red = 1;",
            DiagnosticCodes.MemberShadowsVariant);

    [Fact]
    public void OwnerThatIsNotAType_ReportsLap0704() =>
        ShouldFailWith("def x = 1;\ndef x.m = 1;", DiagnosticCodes.MemberOwnerMustBeAType);

    /// <summary>A ordem do topo é sequencial (Q8): um membro antes do tipo não vê o tipo.</summary>
    [Fact]
    public void MemberBeforeTheType_ReportsLap0704() =>
        ShouldFailWith(
            "def User2.hello = 1;\ndef User2 = type { name: Str; };",
            DiagnosticCodes.MemberOwnerMustBeAType);

    /// <summary>Sombrear um tipo do prelude redireciona os membros dele — por identidade.</summary>
    [Fact]
    public void Member_OnPreludeType_IsAccessible() =>
        TypeOfDef("def Option.vazio = 0;\ndef n = Option.vazio;", "n").ShouldBe(PrimitiveType.Int);
}

public sealed class FieldAssignmentTests : TypeCheckerTestBase
{
    private const string Types = """
        def Endereco = type { rua: Str; };
        def User = type { name: Str; age: Int; };
        def Pessoa = type { endereco: Endereco; };

        """;

    /// <summary>
    /// A mutabilidade segue o <b>binding</b>, não a forma do alvo (plano 21 §21.3b).
    /// </summary>
    [Fact]
    public void Assign_ToFieldOfVar_IsAccepted() =>
        ShouldPass(Types + "var u = .User { name: \"a\", age: 1 };\nu.name = \"b\";");

    [Fact]
    public void Assign_ToFieldOfDef_ReportsLap0206() =>
        ShouldFailWith(
            Types + "def u = .User { name: \"a\", age: 1 };\nu.name = \"b\";",
            DiagnosticCodes.NotAssignable);

    [Fact]
    public void Assign_ToNestedField_IsAccepted() =>
        ShouldPass(Types + """
            var p = .Pessoa { endereco: .Endereco { rua: "A" } };

            p.endereco.rua = "B";
            """);

    [Fact]
    public void Assign_WrongType_ReportsLap0210() =>
        ShouldFailWith(
            Types + "var u = .User { name: \"a\", age: 1 };\nu.name = 12;",
            DiagnosticCodes.TypeMismatch);

    [Fact]
    public void Assign_UnknownField_ReportsLap0250() =>
        ShouldFailWith(
            Types + "var u = .User { name: \"a\", age: 1 };\nu.naoExiste = 1;",
            DiagnosticCodes.UnknownField);

    /// <summary>
    /// O membro <b>existe</b> no tipo, só não é campo da instância — "campo
    /// desconhecido" seria mentira.
    /// </summary>
    [Fact]
    public void Assign_ToMember_ReportsLap0707() =>
        ShouldFailWith(
            Types + """
            def User.hello = 1;

            var u = .User { name: "a", age: 1 };

            u.hello = 2;
            """,
            DiagnosticCodes.AssignToMember);

    [Fact]
    public void Assign_ThroughNonStruct_ReportsLap0250() =>
        ShouldFailWith("var n = 1;\nn.campo = 2;", DiagnosticCodes.UnknownField);

    /// <summary>Um `var` continua não atravessando fronteira de função (Q25).</summary>
    [Fact]
    public void Assign_ToFieldAcrossFunction_ReportsLap0207() =>
        ShouldFailWith(
            Types + """
            var u = .User { name: "a", age: 1 };

            def f = fn() Void { u.name = "b"; };
            """,
            DiagnosticCodes.MutableCapturedByFunction);
}

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

/// <summary>
/// <c>fn(self, ...)</c> e <c>receptor.m(args)</c> — plano 22.
///
/// A separação estático × instância existe para que <c>User.hello</c> e
/// <c>user.hello()</c> não sejam dois caminhos para a mesma coisa.
/// </summary>
public sealed class InstanceMemberTests : TypeCheckerTestBase
{
    private const string User = """
        def User = type { name: Str; age: Int; };

        def User.saudar = fn(self) Str { return self.name; };
        def User.maisVelho = fn(self, anos: Int) Int { return self.age + anos; };
        def User.create = fn(nome: Str) User { return .User { name: nome, age: 0 }; };

        def u = User.create("g");

        """;

    [Fact]
    public void Self_GetsTheOwnerType() =>
        TypeOfDef(User + "def s = u.saudar();", "s").ShouldBe(PrimitiveType.Str);

    [Fact]
    public void Instance_Call_WithArguments() =>
        TypeOfDef(User + "def n = u.maisVelho(5);", "n").ShouldBe(PrimitiveType.Int);

    /// <summary>O receptor entra como argumento 0, então o tipo dele é checado.</summary>
    [Fact]
    public void Instance_Call_ChecksTheArgument() =>
        ShouldFailWith(User + """def n = u.maisVelho("x");""", DiagnosticCodes.ArgumentTypeMismatch);

    /// <summary>E a mensagem de aridade **desconta** o receptor.</summary>
    [Fact]
    public void Instance_ArityMessageDiscountsTheReceiver() =>
        ShouldFailWith(User + "def n = u.maisVelho();", DiagnosticCodes.ArgumentCountMismatch)
            .Message.ShouldBe("esperados 1 argumentos, fornecidos 0");

    [Fact]
    public void Instance_OnType_ReportsLap0710() =>
        ShouldFailWith(User + "def s = User.saudar();", DiagnosticCodes.MemberRequiresInstance);

    [Fact]
    public void Static_OnInstance_ReportsLap0711() =>
        ShouldFailWith(User + """def x = u.create("y");""", DiagnosticCodes.MemberIsStatic);

    /// <summary>
    /// A anotação desliga o gatilho: um <c>self</c> anotado é parâmetro comum, e o
    /// membro volta a ser estático.
    /// </summary>
    [Fact]
    public void Self_Annotated_IsAPlainParameter() =>
        ShouldPass("""
            def User = type { name: Str; };
            def User.of = fn(self: Str) User { return .User { name: self }; };
            def u = User.of("g");
            """);

    [Fact]
    public void Self_NotFirst_ReportsLap0712() =>
        ShouldFailWith(
            "def User = type { n: Int; };\ndef User.m = fn(x: Int, self) Int { return x; };",
            DiagnosticCodes.SelfOutsideMember);

    [Fact]
    public void Self_OutsideMember_ReportsLap0712() =>
        ShouldFailWith("def f = fn(self) Int { return 1; };", DiagnosticCodes.SelfOutsideMember);

    /// <summary>Uma `fn` aninhada no corpo de um membro não é membro.</summary>
    [Fact]
    public void Self_InNestedFunction_ReportsLap0712() =>
        ShouldFailWith(
            """
            def User = type { n: Int; };
            def User.m = fn(self) Int {
                def interna = fn(self) Int { return 1; };
                return 1;
            };
            """,
            DiagnosticCodes.SelfOutsideMember);

    /// <summary><c>self</c> não é palavra reservada.</summary>
    [Fact]
    public void Self_IsNotReserved() => ShouldPass("def self = 1;\ndef x = self + 1;");

    [Fact]
    public void Self_AsPlainParameterName_IsFine() =>
        ShouldPass("def f = fn(self: Int) Int { return self; };");

    /// <summary>O receptor é uma expressão qualquer, não só um nome.</summary>
    [Fact]
    public void Receiver_IsAnyExpression() =>
        ShouldPass(User + "def s = User.create(\"x\").saudar();");

    [Fact]
    public void UnknownMemberOnInstance_ReportsLap0250() =>
        ShouldFailWith(User + "def s = u.naoExiste();", DiagnosticCodes.UnknownField);
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

/// <summary>
/// <c>def Result&lt;?, ?&gt;.isOk</c> — membros sobre tipos genéricos, plano 23.
///
/// <c>?</c> é <b>curinga</b>, não parâmetro: ele não liga nome nenhum, e é isso
/// que dissolve a Q27 — não há o que unificar, e nada a transportar para o corpo.
/// O alcance de cada declaração está escrito, não deduzido.
/// </summary>
public sealed class GenericMemberTests : TypeCheckerTestBase
{
    private const string Caixa = "def Caixa = type<T> { v: T; };\n";

    private const string Par = "def Par = type<A, B> { a: A; b: B; };\n";

    /// <summary>Curinga em todas as posições vale para qualquer instância.</summary>
    [Fact]
    public void Wildcard_AppliesToAnyInstance() =>
        ShouldPass(
            Caixa
            + "def Caixa<?>.tem = fn(self) Bool { return true; };\n"
            + "def b = .Caixa<Int> { v: 1 }.tem();");

    /// <summary>
    /// O curinga é posicional: <c>Par&lt;Int, ?&gt;</c> alcança
    /// <c>Par&lt;Int, Str&gt;</c> e não <c>Par&lt;Bool, Str&gt;</c>.
    /// </summary>
    [Fact]
    public void Wildcard_IsPositional()
    {
        const string Source =
            Par
            + "def Par<Int, ?>.primeiro = fn(self) Int { return self.a; };\n";

        ShouldPass(Source + "def x = .Par<Int, Str> { a: 1, b: \"s\" }.primeiro();");

        ShouldFailWith(
            Source + "def x = .Par<Bool, Str> { a: true, b: \"s\" }.primeiro();",
            DiagnosticCodes.UnknownField);
    }

    /// <summary>Curinga é de posição, nunca de definição: o nome do dono é exato.</summary>
    [Fact]
    public void Wildcard_DoesNotCrossDefinitions() =>
        ShouldFailWith(
            Caixa
            + "def Outra = type<T> { v: T; };\n"
            + "def Caixa<?>.tem = fn(self) Bool { return true; };\n"
            + "def b = .Outra<Int> { v: 1 }.tem();",
            DiagnosticCodes.UnknownField);

    /// <summary>
    /// Dois padrões que se cruzam: existe um <c>Par&lt;Int, Str&gt;</c> que casa
    /// com os dois, e nada diz qual roda. Erro, e não regra de especificidade.
    /// </summary>
    [Fact]
    public void Overlap_IsAnError() =>
        ShouldFailWith(
            Par
            + "def Par<?, ?>.descrever = fn(self) Str { return \"a\"; };\n"
            + "def Par<Int, ?>.descrever = fn(self) Str { return \"b\"; };",
            DiagnosticCodes.OverlappingMember);

    /// <summary>
    /// Padrão <b>idêntico</b> é redeclaração, e continua sendo LAP0702: a
    /// distinção importa porque uma é erro de digitação e a outra é alcance mal
    /// escrito.
    /// </summary>
    [Fact]
    public void SamePattern_IsDuplicate() =>
        ShouldFailWith(
            Par
            + "def Par<Int, ?>.descrever = fn(self) Str { return \"a\"; };\n"
            + "{ def Par<Int, ?>.descrever = fn(self) Str { return \"b\"; }; }",
            DiagnosticCodes.DuplicateMember);

    /// <summary>Padrões disjuntos convivem, e o receptor escolhe.</summary>
    [Fact]
    public void Disjoint_PatternsCoexist() =>
        ShouldPass(
            Par
            + "def Par<Int, ?>.qual = fn(self) Str { return \"int\"; };\n"
            + "def Par<Bool, ?>.qual = fn(self) Str { return \"bool\"; };\n"
            + "def a = .Par<Int, Str> { a: 1, b: \"s\" }.qual();\n"
            + "def b = .Par<Bool, Str> { a: true, b: \"s\" }.qual();");

    /// <summary>
    /// <c>?</c> não é um tipo. Permiti-lo como tipo de valor seria um <c>Any</c>
    /// estrutural pela porta dos fundos.
    /// </summary>
    [Fact]
    public void Wildcard_IsNotAType() =>
        ShouldFailWith(
            Caixa + "def f = fn(c: Caixa<?>) Bool { return true; };",
            DiagnosticCodes.WildcardOutsideOwner);

    [Fact]
    public void Wildcard_InAnnotation_IsNotAType() =>
        ShouldFailWith(
            Caixa + "def c: Caixa<?> = .Caixa<Int> { v: 1 };",
            DiagnosticCodes.WildcardOutsideOwner);

    [Fact]
    public void Wildcard_ArityIsChecked() =>
        ShouldFailWith(
            Par + "def Par<?>.qual = fn(self) Str { return \"a\"; };",
            DiagnosticCodes.MemberOwnerArity);

    /// <summary><c>self</c> recebe o dono com o padrão escrito (§23.7).</summary>
    [Fact]
    public void Self_OnGenericOwner_CarriesThePattern()
    {
        var type = TypeOfDef(
            Par + "def Par<Int, ?>.eu = fn(self) Int { return self.a; };",
            "Par<Int, ?>#eu").ShouldBeOfType<FunctionType>();

        var self = type.Parameters[0].ShouldBeOfType<NamedType>();

        self.Definition.Name.ShouldBe("Par");
        self.Arguments[0].ShouldBe(new TypeArgument(PrimitiveType.Int));
        self.Arguments[1].ShouldBe(WildcardArgument.Instance);
    }

    /// <summary>
    /// O corpo só faz com <c>self</c> o que não depende do argumento curinga: sem
    /// nome, não há como escrever o tipo de <c>self.v</c>. É a limitação declarada
    /// da §23.6, e ela aparece como campo inalcançável.
    /// </summary>
    [Fact]
    public void Self_CannotReadArgumentDependentField() =>
        ShouldFailWith(
            Caixa + "def Caixa<?>.ler = fn(self) Int { return self.v; };",
            DiagnosticCodes.UnknownField);

    /// <summary>Um campo que não depende do curinga continua legível.</summary>
    [Fact]
    public void Self_ReadsArgumentIndependentField() =>
        ShouldPass(
            "def Rotulado = type<T> { nome: Str; v: T; };\n"
            + "def Rotulado<?>.rotulo = fn(self) Str { return self.nome; };");

    /// <summary>
    /// Dono sem <c>&lt;&gt;</c> sobre um genérico é o padrão todo curinga — é o
    /// que faz `def Result.ok = fn&lt;T&gt;(...)` continuar valendo (§23.8).
    /// </summary>
    [Fact]
    public void BareOwner_IsAllWildcards() =>
        ShouldFailWith(
            Par
            + "def Par.descrever = fn(self) Str { return \"a\"; };\n"
            + "def Par<?, ?>.descrever = fn(self) Str { return \"b\"; };",
            DiagnosticCodes.DuplicateMember);

    /// <summary>
    /// O outro lado da regra do autor: <c>&lt;&gt;</c> à direita do <c>=</c> fala
    /// do <b>membro</b>, e isso já funcionava — a cadeia pós-fixa do M4 cobre
    /// <c>Result.ok&lt;Int&gt;(10)</c>.
    /// </summary>
    [Fact]
    public void GenericMember_OnTheRight()
    {
        var type = TypeOfDef(
            "def Res = enum<T, E> { Ok(T), Err(E) };\n"
            + "def Res.ok = fn<T>(v: T) Res<T, Str> { return Res<T, Str>.Ok(v); };\n"
            + "def r = Res.ok<Int>(10);",
            "r").ShouldBeOfType<NamedType>();

        type.Definition.Name.ShouldBe("Res");
        type.Arguments[0].ShouldBe(new TypeArgument(PrimitiveType.Int));
    }
}

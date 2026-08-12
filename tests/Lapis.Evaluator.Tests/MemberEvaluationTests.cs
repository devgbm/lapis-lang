namespace Lapis.Evaluator.Tests;

/// <summary>
/// Membros de tipo e atribuição a campo em execução (plano 21 §21.6).
///
/// Nada aqui exige caminho novo no evaluator: um membro é um <c>Let</c> comum sob
/// nome sintético, e a atribuição a campo é reconstrução de valor.
/// </summary>
public sealed class MemberEvaluationTests : EvaluatorTestBase
{
    private const string User = "def User = type { name: Str; age: Int; };\n";

    [Fact]
    public void Member_Value() =>
        Output(User + "def User.defaultAge = 30;\nprint(User.defaultAge);").ShouldBe("30\n");

    [Fact]
    public void Member_Function() =>
        Output(User + """
            def User.create = fn(nome: Str) User {
                return .User { name: nome, age: 0 };
            };

            print(User.create("g"));
            """).ShouldBe("User { name: \"g\", age: 0 }\n");

    /// <summary>Um membro pode ler outro membro do mesmo tipo, declarado antes.</summary>
    [Fact]
    public void Member_CanReadAnotherMember() =>
        Output(User + """
            def User.defaultAge = 30;

            def User.create = fn(nome: Str) User {
                return .User { name: nome, age: User.defaultAge };
            };

            print(User.create("g").age);
            """).ShouldBe("30\n");

    [Fact]
    public void Member_OnEnum() =>
        Output("def Color = enum { Red };\ndef Color.total = 1;\nprint(Color.total);").ShouldBe("1\n");

    /// <summary>Um membro não sombreia o `def` comum de mesmo nome, e vice-versa.</summary>
    [Fact]
    public void Member_AndPlainDef_Coexist() =>
        Output(User + """
            def User.hello = "do tipo";
            def hello = "solto";

            print(User.hello);
            print(hello);
            """).ShouldBe("do tipo\nsolto\n");
}

public sealed class FieldAssignmentEvaluationTests : EvaluatorTestBase
{
    private const string Types = """
        def Endereco = type { rua: Str; };
        def User = type { name: Str; age: Int; };
        def Pessoa = type { endereco: Endereco; };

        """;

    [Fact]
    public void Assign_ReplacesTheField() =>
        Output(Types + """
            var u = .User { name: "antes", age: 1 };

            u.name = "depois";

            print(u);
            """).ShouldBe("User { name: \"depois\", age: 1 }\n");

    [Fact]
    public void Assign_Nested() =>
        Output(Types + """
            var p = .Pessoa { endereco: .Endereco { rua: "A" } };

            p.endereco.rua = "B";

            print(p.endereco.rua);
            """).ShouldBe("B\n");

    /// <summary>
    /// A atribuição é **atualização funcional**: o struct é reconstruído e o slot
    /// recebe o valor novo. A consequência é semântica de valor, e ela é
    /// observável — um `def` guarda o valor de então, não uma referência.
    ///
    /// Está afirmado aqui porque é comportamento da linguagem, não detalhe: é o
    /// preço de manter todo `Value` imutável e não ter aliasing (Q25).
    /// </summary>
    [Fact]
    public void Assign_HasValueSemantics() =>
        Output(Types + """
            var u = .User { name: "antes", age: 1 };

            def copia = u;

            u.name = "depois";

            print(u.name);
            print(copia.name);
            """).ShouldBe("depois\nantes\n");

    /// <summary>E o campo aninhado que não foi tocado continua o mesmo valor.</summary>
    [Fact]
    public void Assign_LeavesSiblingFieldsAlone() =>
        Output(Types + """
            var u = .User { name: "antes", age: 7 };

            u.name = "depois";

            print(u.age);
            """).ShouldBe("7\n");

    [Fact]
    public void Assign_InsideALoop() =>
        Output(Types + """
            var u = .User { name: "x", age: 0 };
            var i = 0;

            label conta;
            i = i + 1;
            u.age = i;
            goto conta if i < 3;

            print(u.age);
            """).ShouldBe("3\n");
}

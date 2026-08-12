using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

/// <summary>
/// <c>def T.m = e;</c> e <c>x.a.b = e;</c> — plano 21.
///
/// A desambiguação é de um token nos dois casos: depois do primeiro identificador,
/// um <c>.</c> muda a leitura, e nada mais precisa ser decidido.
/// </summary>
public sealed class MemberDeclarationParseTests : ParserTestBase
{
    [Fact]
    public void Member_Declaration() =>
        ShouldPrintStatementAs("def User.hello = 1;", "(def User.hello (int 1))");

    [Fact]
    public void Member_Declaration_WithAnnotation() =>
        ShouldPrintStatementAs("def User.age: Int = 1;", "(def User.age (type Int) (int 1))");

    [Fact]
    public void Member_Declaration_KeepsTheOwner()
    {
        var def = SingleDef("def User.hello = 1;");

        def.Name.ShouldBe("hello");
        def.Owner.ShouldBeOfType<NamedTypeSyntax>().Name.ShouldBe("User");
    }

    /// <summary>Um <c>def</c> comum não ganha dono — a forma continua exatamente a mesma.</summary>
    [Fact]
    public void PlainDef_HasNoOwner() => SingleDef("def hello = 1;").Owner.ShouldBeNull();

    /// <summary>Um membro é definitivo: <c>var T.m</c> não existe (plano 21 §21.2).</summary>
    [Fact]
    public void Member_CannotBeVar_ReportsLap0705() =>
        Codes("var User.hello = 1;").ShouldContain(DiagnosticCodes.MemberCannotBeVar);

    [Fact]
    public void Member_MissingName_ReportsLap0112() =>
        Codes("def User. = 1;").ShouldContain(DiagnosticCodes.ExpectedIdentifier);

    private static DefStatement SingleDef(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>();
}

public sealed class FieldAssignmentParseTests : ParserTestBase
{
    [Fact]
    public void Assign_ToField() => ShouldPrintStatementAs("u.name = 1;", "(assign u.name (int 1))");

    [Fact]
    public void Assign_ToNestedField() =>
        ShouldPrintStatementAs("u.a.b = 1;", "(assign u.a.b (int 1))");

    [Fact]
    public void Assign_ToField_KeepsThePath()
    {
        var assign = Parse("u.a.b = 1;")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<AssignStatement>();

        assign.Name.ShouldBe("u");
        assign.Path.ShouldBe(["a", "b"]);
        assign.PathSpans.Length.ShouldBe(2);
    }

    [Fact]
    public void Assign_Simple_HasEmptyPath() =>
        Parse("u = 1;").Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<AssignStatement>().Path.ShouldBeEmpty();

    /// <summary>
    /// O receptor é um <b>nome</b>, não uma expressão qualquer. <c>f().x = 1</c>
    /// mutaria um temporário que ninguém mais vê, então nem é atribuição: o parser
    /// lê `f()` como expressão e reclama do `=`.
    /// </summary>
    [Fact]
    public void Assign_ToCallResult_IsNotAnAssignment() => Codes("f().x = 1;").ShouldNotBeEmpty();

    /// <summary>Acesso a campo sem `=` continua sendo expressão, não atribuição.</summary>
    [Fact]
    public void FieldAccess_IsStillAnExpression() =>
        ShouldPrintAs("u.a.b;", "(member b (member a (name u)))");
}

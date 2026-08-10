using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

public sealed class TypeDeclarationParseTests : ParserTestBase
{
    [Fact]
    public void Type_Empty() => ShouldPrintAs("type { };", "(type)");

    [Fact]
    public void Type_WithFields() =>
        ShouldPrintAs(
            "type { id: Int; name: Str; };",
            "(type (field id Int) (field name Str))");

    [Fact]
    public void Type_Generic() =>
        ShouldPrintAs("type<T> { value: T; };", "(type<T> (field value T))");

    [Fact]
    public void Type_FieldMissingSemicolon_ReportsLap0106() =>
        Codes("def T = type { a: Int };").ShouldContain(DiagnosticCodes.ExpectedFieldSemicolon);

    [Fact]
    public void Type_FieldMissingType_ReportsError() => Codes("def T = type { a; };").ShouldNotBeEmpty();

    /// <summary>Spec §14: o exemplo `User`.</summary>
    [Fact]
    public void Type_Def_FromSpecSection14()
    {
        var def = Parse("def User = type { id: Int; name: Str; };")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>();

        def.Value.ShouldBeOfType<TypeExpression>().Fields.Length.ShouldBe(2);
    }
}

public sealed class ConstructParseTests : ParserTestBase
{
    [Fact]
    public void Construct_Simple() =>
        ShouldPrintAs(
            ".User { id: 1, name: \"g\" };",
            "(construct User (init id (int 1)) (init name (str \"g\")))");

    [Fact]
    public void Construct_Empty() => ShouldPrintAs(".Unit { };", "(construct Unit)");

    [Fact]
    public void Construct_Generic() =>
        ShouldPrintAs(".Box<Int> { value: 1 };", "(construct Box<Int> (init value (int 1)))");

    [Fact]
    public void Construct_TrailingComma() =>
        ShouldPrintAs(".P { x: 1, };", "(construct P (init x (int 1)))");

    [Fact]
    public void Construct_Nested() =>
        ShouldPrintAs(
            ".A { inner: .B { v: 1 } };",
            "(construct A (init inner (construct B (init v (int 1)))))");

    [Fact]
    public void Construct_ThenFieldAccess() =>
        ShouldPrintAs(".P { x: 1 }.x;", "(member x (construct P (init x (int 1))))");

    /// <summary>
    /// Q2: o ponto inicial dispensa qualquer regra contextual — a construção é
    /// válida na condição de `if` sem parênteses.
    /// </summary>
    [Fact]
    public void Construct_InIfCondition_NeedsNoParentheses()
    {
        var expression = Parse("if .P { valid: true }.valid { };")
            .Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<IfExpression>();

        expression.Condition.ShouldBeOfType<MemberExpression>();
    }

    /// <summary>Sem o ponto, `P { ... }` é `P` seguido de um bloco — não construção.</summary>
    [Fact]
    public void WithoutDot_IsNotAConstruct()
    {
        var expression = Parse("if P { };")
            .Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<IfExpression>();

        expression.Condition.ShouldBeOfType<IdentifierExpression>().Name.ShouldBe("P");
    }

    [Fact]
    public void Construct_MissingTypeName_ReportsLap0112() =>
        Codes(". { a: 1 };").ShouldContain(DiagnosticCodes.ExpectedIdentifier);

    [Fact]
    public void Construct_Unclosed_ReportsError() => Codes(".P { a: 1 ;").ShouldNotBeEmpty();
}

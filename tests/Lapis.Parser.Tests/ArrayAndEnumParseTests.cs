using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

public sealed class ArrayParseTests : ParserTestBase
{
    [Fact]
    public void Array_Literal() => ShouldPrintAs("[1, 2, 3];", "(array (int 1) (int 2) (int 3))");

    [Fact]
    public void Array_Empty() => ShouldPrintAs("[];", "(array)");

    [Fact]
    public void Array_TrailingComma() => ShouldPrintAs("[1, 2,];", "(array (int 1) (int 2))");

    [Fact]
    public void Array_Nested() => ShouldPrintAs("[[1]];", "(array (array (int 1)))");

    [Fact]
    public void Array_OfExpressions() =>
        ShouldPrintAs("[1 + 2];", "(array (binary + (int 1) (int 2)))");

    [Fact]
    public void Array_Unclosed_ReportsError() => Codes("[1, 2;").ShouldNotBeEmpty();
}

public sealed class IndexParseTests : ParserTestBase
{
    [Fact]
    public void Index_Simple() => ShouldPrintAs("a[0];", "(index (name a) (int 0))");

    [Fact]
    public void Index_Chained() => ShouldPrintAs("a[0][1];", "(index (index (name a) (int 0)) (int 1))");

    [Fact]
    public void Index_OnCall() => ShouldPrintAs("f()[0];", "(index (call (name f)) (int 0))");

    [Fact]
    public void Index_OnArrayLiteral() =>
        ShouldPrintAs("[1, 2][0];", "(index (array (int 1) (int 2)) (int 0))");

    [Fact]
    public void Index_Unclosed_ReportsLap0105() =>
        Codes("a[1;").ShouldContain(DiagnosticCodes.ExpectedCloseBracket);
}

public sealed class MemberParseTests : ParserTestBase
{
    [Fact]
    public void Member_Simple() => ShouldPrintAs("a.b;", "(member b (name a))");

    [Fact]
    public void Member_Chained() => ShouldPrintAs("a.b.c;", "(member c (member b (name a)))");

    [Fact]
    public void Member_ThenCall() =>
        ShouldPrintAs("Result.Ok(1);", "(call (member Ok (name Result)) (int 1))");

    /// <summary>Ordem pós-fixa: `a.b[0](x).c` aninha da esquerda para a direita.</summary>
    [Fact]
    public void Postfix_MixedOrder() =>
        ShouldPrintAs(
            "a.b[0](x).c;",
            "(member c (call (index (member b (name a)) (int 0)) (name x)))");

    [Fact]
    public void Member_MissingName_ReportsLap0112() =>
        Codes("a.;").ShouldContain(DiagnosticCodes.ExpectedIdentifier);
}

public sealed class EnumParseTests : ParserTestBase
{
    [Fact]
    public void Enum_Simple() =>
        ShouldPrintAs("enum { Red, Green };", "(enum (variant Red) (variant Green))");

    [Fact]
    public void Enum_Empty() => ShouldPrintAs("enum { };", "(enum)");

    [Fact]
    public void Enum_TrailingComma() => ShouldPrintAs("enum { Red, };", "(enum (variant Red))");

    [Fact]
    public void Enum_WithPayload() =>
        ShouldPrintAs("enum { Wrap(Int) };", "(enum (variant Wrap Int))");

    [Fact]
    public void Enum_WithMultiplePayloads() =>
        ShouldPrintAs("enum { Pair(Int, Str) };", "(enum (variant Pair Int Str))");

    [Fact]
    public void Enum_Generic() =>
        ShouldPrintAs("enum<T, E> { Ok(T), Err(E) };", "(enum<T E> (variant Ok T) (variant Err E))");

    [Fact]
    public void Enum_Def_FromSpecSection16()
    {
        var def = Parse("def Result = enum<T, E> { Ok(T), Err(E) };")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>();

        var enumExpr = def.Value.ShouldBeOfType<EnumExpression>();
        enumExpr.TypeParameters.Length.ShouldBe(2);
        enumExpr.Variants.Length.ShouldBe(2);
    }

    [Fact]
    public void Enum_Unclosed_ReportsError() => Codes("def E = enum { Red").ShouldNotBeEmpty();
}

public sealed class GenericTypeSyntaxTests : ParserTestBase
{
    [Fact]
    public void GenericType_InAnnotation()
    {
        var def = Parse("def r: Result<Int, IndexError> = x;")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>();

        var type = def.Annotation.ShouldBeOfType<NamedTypeSyntax>();
        type.Name.ShouldBe("Result");
        type.Arguments.Length.ShouldBe(2);
    }

    /// <summary>Sem token `>>`: generics aninhados fecham com dois `>` (spec §44).</summary>
    [Fact]
    public void GenericType_Nested()
    {
        var def = Parse("def b: Box<Box<Int>> = x;")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>();

        var outer = def.Annotation.ShouldBeOfType<NamedTypeSyntax>();
        outer.Arguments.ShouldHaveSingleItem().ShouldBeOfType<NamedTypeSyntax>().Name.ShouldBe("Box");
    }

    [Fact]
    public void GenericType_ArrayOfGeneric()
    {
        var def = Parse("def a: Result<Int, IndexError>[] = x;")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>();

        def.Annotation.ShouldBeOfType<ArrayTypeSyntax>()
            .Element.ShouldBeOfType<NamedTypeSyntax>().Name.ShouldBe("Result");
    }

    [Fact]
    public void GenericType_Empty_ReportsLap0116() =>
        Codes("def r: Result<> = x;").ShouldContain(DiagnosticCodes.EmptyGenericArgumentList);
}

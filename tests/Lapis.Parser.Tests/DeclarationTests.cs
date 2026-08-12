using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

public sealed class DefTests : ParserTestBase
{
    [Fact]
    public void Def_Simple()
    {
        var def = SingleDef("def x = 10;");

        def.Name.ShouldBe("x");
        def.Annotation.ShouldBeNull();
        def.Value.ShouldBeOfType<IntLiteral>();
    }

    [Fact]
    public void Def_WithAnnotation()
    {
        var def = SingleDef("def x: Int = 10;");

        def.Annotation.ShouldBeOfType<NamedTypeSyntax>().Name.ShouldBe("Int");
    }

    [Fact]
    public void Def_WithSpanAnnotation()
    {
        var def = SingleDef("def x: [Int;?] = y;");

        var span = def.Annotation.ShouldBeOfType<SpanTypeSyntax>();
        span.Element.ShouldBeOfType<NamedTypeSyntax>().Name.ShouldBe("Int");
        span.Size.ShouldBeOfType<UnknownSizeSyntax>();
    }

    [Fact]
    public void Def_WithSizedSpanAnnotation()
    {
        var def = SingleDef("def x: [Int;3] = y;");

        def.Annotation.ShouldBeOfType<SpanTypeSyntax>()
            .Size.ShouldBeOfType<FixedSizeSyntax>().Value.ShouldBe(3);
    }

    [Fact]
    public void Def_WithNestedSpanAnnotation()
    {
        var def = SingleDef("def x: [[Int;1];2] = y;");

        def.Annotation.ShouldBeOfType<SpanTypeSyntax>()
            .Element.ShouldBeOfType<SpanTypeSyntax>();
    }

    [Fact]
    public void Def_WithFunctionTypeAnnotation()
    {
        var def = SingleDef("def f: fn(Int, Int) Int = g;");

        var type = def.Annotation.ShouldBeOfType<FunctionTypeSyntax>();
        type.Parameters.Length.ShouldBe(2);
        type.Return.ShouldBeOfType<NamedTypeSyntax>().Name.ShouldBe("Int");
    }

    [Fact]
    public void Def_MissingSemicolon_ReportsLap0102() =>
        Codes("def x = 10").ShouldContain(DiagnosticCodes.ExpectedSemicolon);

    [Fact]
    public void Def_MissingEquals_ReportsLap0103() =>
        Codes("def x 10;").ShouldContain(DiagnosticCodes.ExpectedEquals);

    [Fact]
    public void Def_MissingName_ReportsLap0112() =>
        Codes("def = 10;").ShouldContain(DiagnosticCodes.ExpectedIdentifier);

    [Fact]
    public void Def_MissingValue_ReportsError() =>
        Codes("def x = ;").ShouldNotBeEmpty();

    [Fact]
    public void Def_SpanCoversDefToSemicolon()
    {
        var def = SingleDef("def x = 10;");

        def.Span.Start.ShouldBe(0);
        def.Span.End.ShouldBe(11);
    }

    private static DefStatement SingleDef(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>();
}

public sealed class FunctionTests : ParserTestBase
{
    [Fact]
    public void Fn_NoParams_NoReturnType()
    {
        var fn = SingleFunction("def f = fn() { };");

        fn.Parameters.ShouldBeEmpty();
        fn.ReturnType.ShouldBeNull();
    }

    [Fact]
    public void Fn_WithReturnType()
    {
        var fn = SingleFunction("def f = fn() Int { return 1; };");

        fn.ReturnType.ShouldBeOfType<NamedTypeSyntax>().Name.ShouldBe("Int");
    }

    [Fact]
    public void Fn_WithParameters()
    {
        var fn = SingleFunction("def add = fn(a: Int, b: Int) Int { return a + b; };");

        fn.Parameters.Length.ShouldBe(2);
        fn.Parameters[0].Name.ShouldBe("a");
        fn.Parameters[1].Name.ShouldBe("b");
    }

    [Fact]
    public void Fn_TrailingCommaInParameters_IsAccepted() =>
        SingleFunction("def f = fn(a: Int,) Int { return a; };").Parameters.Length.ShouldBe(1);

    [Fact]
    public void Fn_MissingParameterType_ReportsLap0104() =>
        Codes("def f = fn(a) Int { return a; };").ShouldContain(DiagnosticCodes.ParameterRequiresType);

    [Fact]
    public void Fn_ReturningFunction()
    {
        var fn = SingleFunction("def f = fn() fn(Int) Int { return g; };");

        fn.ReturnType.ShouldBeOfType<FunctionTypeSyntax>();
    }

    [Fact]
    public void Fn_Nested() => Should.NotThrow(() => Parse("def f = fn() Int { return fn() Int { return 1; }(); };"));

    private static FunctionExpression SingleFunction(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<FunctionExpression>();
}

public sealed class CallTests : ParserTestBase
{
    [Fact]
    public void Call_NoArgs() => ShouldPrintAs("main();", "(call (name main))");

    [Fact]
    public void Call_WithArgs() =>
        ShouldPrintAs("add(10, 20);", "(call (name add) (int 10) (int 20))");

    [Fact]
    public void Call_TrailingComma_IsAccepted() =>
        ShouldPrintAs("add(10, 20,);", "(call (name add) (int 10) (int 20))");

    [Fact]
    public void Call_Chained() => ShouldPrintAs("f()();", "(call (call (name f)))");

    [Fact]
    public void Call_NestedArguments() =>
        ShouldPrintAs("f(g(1));", "(call (name f) (call (name g) (int 1)))");

    [Fact]
    public void Call_ArgumentIsExpression() =>
        ShouldPrintAs("f(1 + 2);", "(call (name f) (binary + (int 1) (int 2)))");

    [Fact]
    public void Call_Unclosed_ReportsError() => Codes("f(1;").ShouldNotBeEmpty();
}

public sealed class ReturnTests : ParserTestBase
{
    [Fact]
    public void Return_WithValue()
    {
        var body = FunctionBody("def f = fn() Int { return a + b; };");

        body.Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<ReturnExpression>()
            .Value.ShouldNotBeNull();
    }

    [Fact]
    public void Return_Empty_HasNoValue()
    {
        var body = FunctionBody("def f = fn() { return; };");

        body.Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<ReturnExpression>()
            .Value.ShouldBeNull();
    }

    [Fact]
    public void Return_AsTailWithoutSemicolon()
    {
        var body = FunctionBody("def f = fn() Int { return 1 };");

        body.Tail.ShouldBeOfType<ReturnExpression>();
    }

    [Fact]
    public void Return_InArgumentPosition_IsAllowed() =>
        Should.NotThrow(() => Parse("def f = fn() Int { g(return 1); };"));

    private static BlockExpression FunctionBody(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<FunctionExpression>()
            .Body;
}

/// <summary>
/// <c>throw e</c> (spec de macros §8.2). O parser não sabe se está dentro de um
/// <c>constraint</c> — quem sabe é o checker, e é dele o <c>LAP0507</c>.
/// </summary>
public sealed class ThrowParseTests : ParserTestBase
{
    [Fact]
    public void Throw_TakesTheWholeExpression() =>
        Parse("""throw "erro em " + nome;""")
            .Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<ThrowExpression>()
            .Value.ShouldBeOfType<BinaryExpression>();

    /// <summary>
    /// Ao contrário de <c>return</c>, o valor é obrigatório: a mensagem <b>é</b> o
    /// diagnóstico, e um <c>throw;</c> não teria o que dizer.
    /// </summary>
    [Fact]
    public void Throw_WithoutValue_ReportsError() => Codes("throw;").ShouldNotBeEmpty();

    [Fact]
    public void Throw_InValuePosition_IsAllowed() =>
        Parse("""def x = throw "não";""")
            .Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<ThrowExpression>();

    [Fact]
    public void Throws_IsAnIdentifier() =>
        Parse("def throws = 1;").Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<DefStatement>()
            .Name.ShouldBe("throws");
}

public sealed class IfTests : ParserTestBase
{
    [Fact]
    public void If_NoElse()
    {
        var expression = SingleIf("if x { };");

        expression.Else.ShouldBeNull();
    }

    [Fact]
    public void If_WithElse()
    {
        var expression = SingleIf("if x { } else { };");

        expression.Else.ShouldBeOfType<BlockExpression>();
    }

    [Fact]
    public void If_ElseIf_Nests()
    {
        var expression = SingleIf("if a { } else if b { } else { };");

        var inner = expression.Else.ShouldBeOfType<IfExpression>();
        inner.Else.ShouldBeOfType<BlockExpression>();
    }

    [Fact]
    public void If_ConditionIsExpression()
    {
        var expression = SingleIf("if x < 0 { };");

        expression.Condition.ShouldBeOfType<BinaryExpression>();
    }

    /// <summary>
    /// Não há literal de struct na condição, então <c>if p { }</c> lê <c>p</c> como
    /// identificador e o bloco como o ramo `then` (plano 04 §4.4b).
    /// </summary>
    [Fact]
    public void If_ConditionIsIdentifier_BlockIsThen()
    {
        var expression = SingleIf("if p { };");

        expression.Condition.ShouldBeOfType<IdentifierExpression>().Name.ShouldBe("p");
        expression.Then.ShouldBeOfType<BlockExpression>().Statements.ShouldBeEmpty();
    }

    /// <summary>
    /// <c>Then</c> sem chaves aceita qualquer expressão desde o plano 26 §26.9 —
    /// não é mais erro, é a forma que <c>if c break;</c> usa.
    /// </summary>
    [Fact]
    public void If_BareThen_IsNotAnError() =>
        Codes("if x 1;").ShouldBeEmpty();

    private static IfExpression SingleIf(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<ExpressionStatement>()
            .Expression.ShouldBeOfType<IfExpression>();
}

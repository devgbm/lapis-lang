using System.Diagnostics;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

/// <summary>Fase D do plano 04: parâmetros e argumentos genéricos.</summary>
public sealed class GenericParameterParseTests : ParserTestBase
{
    [Fact]
    public void GenericFn_Declaration()
    {
        var parameters = FunctionOf("def f = fn<T>(v: T) T { return v; };").TypeParameters;

        var parameter = parameters.ShouldHaveSingleItem();
        parameter.Name.ShouldBe("T");
        parameter.ConstType.ShouldBeNull();
    }

    [Fact]
    public void GenericFn_ConstParameter()
    {
        var parameters = FunctionOf("def f = fn<T, N: Int>(v: T) T { return v; };").TypeParameters;

        parameters.Length.ShouldBe(2);
        parameters[0].ConstType.ShouldBeNull();
        parameters[1].Name.ShouldBe("N");
        parameters[1].ConstType.ShouldBeOfType<NamedTypeSyntax>().Name.ShouldBe("Int");
    }

    [Fact]
    public void GenericFn_ConstParameterOfFunctionType()
    {
        var parameters = FunctionOf("def f = fn<Make: fn() Int>() Int { return Make(); };").TypeParameters;

        parameters.ShouldHaveSingleItem().ConstType.ShouldBeOfType<FunctionTypeSyntax>();
    }

    [Fact]
    public void GenericType_Declaration() =>
        ShouldPrintAs("type<T> { value: T; };", "(type<T> (field value T))");

    [Fact]
    public void GenericType_ConstParameter() =>
        ShouldPrintAs("type<T, N: Int> { values: [T;?]; };", "(type<T, N: Int> (field values [T;?]))");

    [Fact]
    public void GenericEnum_Declaration() =>
        ShouldPrintAs("enum<T, E> { Ok(T), Err(E) };", "(enum<T, E> (variant Ok T) (variant Err E))");

    [Fact]
    public void TypeParameter_WithoutName_IsError() =>
        Codes("def f = fn<1>(v: Int) Int { return v; };")
            .ShouldContain(DiagnosticCodes.ExpectedIdentifier);

    private static FunctionExpression FunctionOf(string source) =>
        Parse(source).Statements.ShouldHaveSingleItem()
            .ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<FunctionExpression>();
}

/// <summary>
/// Q5 — a ambiguidade entre <c>f&lt;Int&gt;(x)</c> e <c>a &lt; b</c>, e o
/// backtracking que a resolve.
/// </summary>
public sealed class GenericArgumentParseTests : ParserTestBase
{
    [Fact]
    public void GenericCall_Explicit() =>
        ShouldPrintAs("identity<Int>(10);", "(call (instantiate <Int> (name identity)) (int 10))");

    [Fact]
    public void GenericCall_Multiple() =>
        ShouldPrintAs("f<Int, Str>(a, b);", "(call (instantiate <Int, Str> (name f)) (name a) (name b))");

    /// <summary>Sem argumentos genéricos ainda parseia: o erro é do checker (Q7).</summary>
    [Fact]
    public void GenericCall_Omitted_ParsesAsPlainCall() =>
        ShouldPrintAs("identity(10);", "(call (name identity) (int 10))");

    /// <summary><c>Result&lt;Int, E&gt;.Ok(1)</c> — a variante de um enum genérico.</summary>
    [Fact]
    public void GenericVariant_Access() =>
        ShouldPrintAs(
            "Result<Int, Str>.Ok(1);",
            "(call (member Ok (instantiate <Int, Str> (name Result))) (int 1))");

    /// <summary>
    /// Spec §13: argumentos genéricos misturando string, inteiro, booleano, tipo e
    /// função literal.
    /// </summary>
    [Fact]
    public void ConstGenericArguments_Mixed()
    {
        var arguments = Parse("""def t = SomeType<"value", 1, true, Int, fn() Int { return 1; }>;""")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<InstantiateExpression>().Arguments;

        arguments.Length.ShouldBe(5);
        arguments[0].ShouldBeOfType<ValueArgumentSyntax>().Value.ShouldBeOfType<StrLiteral>();
        arguments[1].ShouldBeOfType<ValueArgumentSyntax>().Value.ShouldBeOfType<IntLiteral>();
        arguments[2].ShouldBeOfType<ValueArgumentSyntax>().Value.ShouldBeOfType<BoolLiteral>();
        arguments[3].ShouldBeOfType<NameArgumentSyntax>().Name.ShouldBe("Int");
        arguments[4].ShouldBeOfType<ValueArgumentSyntax>().Value.ShouldBeOfType<FunctionExpression>();
    }

    /// <summary>O sinal é absorvido no literal, como no desugar de <c>-10</c>.</summary>
    [Fact]
    public void ConstGenericArgument_NegativeLiteral()
    {
        var argument = Parse("def t = Tagged<-1>;")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<InstantiateExpression>()
            .Arguments.ShouldHaveSingleItem();

        argument.ShouldBeOfType<ValueArgumentSyntax>().Value
            .ShouldBeOfType<IntLiteral>().Value.ShouldBe(-1);
    }

    /// <summary>
    /// Em posição de tipo <c>fn(Int) Int</c> é um tipo de função; em posição de
    /// expressão, um <c>{</c> depois do retorno delata um valor.
    /// </summary>
    [Fact]
    public void FunctionArgument_TypeVersusValue()
    {
        var arguments = Parse("def t = F<fn(Int) Int, fn(a: Int) Int { return a; }>;")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>()
            .Value.ShouldBeOfType<InstantiateExpression>().Arguments;

        arguments[0].ShouldBeOfType<TypeArgumentSyntax>().Type.ShouldBeOfType<FunctionTypeSyntax>();
        arguments[1].ShouldBeOfType<ValueArgumentSyntax>().Value.ShouldBeOfType<FunctionExpression>();
    }

    [Fact]
    public void GenericType_InAnnotation()
    {
        var annotation = Parse("def x: Result<Int, Str> = y;")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>()
            .Annotation.ShouldBeOfType<NamedTypeSyntax>();

        annotation.Arguments.Length.ShouldBe(2);
    }

    [Fact]
    public void ConstGenericArgument_InAnnotation()
    {
        var annotation = Parse("def x: FixedArray<Int, 3> = y;")
            .Statements.ShouldHaveSingleItem().ShouldBeOfType<DefStatement>()
            .Annotation.ShouldBeOfType<NamedTypeSyntax>();

        annotation.Arguments[1].ShouldBeOfType<ValueArgumentSyntax>()
            .Value.ShouldBeOfType<IntLiteral>().Value.ShouldBe(3);
    }

    // ------------------------------------------------- a leitura relacional

    [Fact]
    public void LessThan_IsNotGeneric() =>
        ShouldPrintAs("a < b;", "(binary < (name a) (name b))");

    [Fact]
    public void LessThanChain_KeepsRelationalReading() =>
        Codes("def x = a < b + 1;").ShouldBeEmpty();

    /// <summary>
    /// O preço de não ter turbofish: <c>a &lt; b &gt; (c)</c> vira chamada genérica.
    /// Registrado como limitação conhecida no Apêndice C (Q5).
    /// </summary>
    [Fact]
    public void LessThanGreaterThanParen_BecomesGenericCall() =>
        ShouldPrintAs("a < b > (c);", "(call (instantiate <b> (name a)) (name c))");

    /// <summary>
    /// Com um operando à direita de verdade, a leitura relacional vence — é ela
    /// que consegue continuar.
    /// </summary>
    [Fact]
    public void GreaterThanIdentifier_KeepsRelationalReading() =>
        Codes("def x = (a < b) > c;").ShouldBeEmpty();

    /// <summary>
    /// O backtracking é local: cada <c>&lt;</c> falha no token seguinte e não
    /// recomeça, então uma cadeia longa não explode.
    /// </summary>
    [Fact]
    public void Backtracking_IsLinear()
    {
        var source = "def x = " + string.Join(" ", Enumerable.Repeat("a <", 50)) + " b;";

        var stopwatch = Stopwatch.StartNew();
        ParseWithDiagnostics(source);
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);
    }

    /// <summary>Uma tentativa que falha não pode deixar diagnósticos para trás.</summary>
    [Fact]
    public void FailedSpeculation_ReportsNothing() => Codes("def x = a < b;").ShouldBeEmpty();

    [Fact]
    public void EmptyGenericArgumentList_InTypePosition_IsError() =>
        Codes("def x: Box<> = y;").ShouldContain(DiagnosticCodes.EmptyGenericArgumentList);
}

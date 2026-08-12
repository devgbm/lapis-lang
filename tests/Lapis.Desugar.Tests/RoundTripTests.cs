namespace Lapis.Desugar.Tests;

/// <summary>
/// A propriedade de round-trip do plano 02 §2.8: o <c>CoreSourcePrinter</c>
/// produz código <c>.ls</c> válido que, re-parseado e re-desugarado, dá a mesma
/// Core AST.
///
/// É o que habilita <c>lapis pe</c> a emitir programas executáveis e o que
/// permite testar o partial evaluator comparando residuais reparseáveis.
/// </summary>
public sealed class RoundTripTests : DesugarTestBase
{
    [Theory]
    [InlineData("def x = 1;")]
    [InlineData("def x = -10;")]
    [InlineData("def x = 1.5;")]
    [InlineData("def x = true;")]
    [InlineData("def x = \"texto com \\\"aspas\\\" e \\n\";")]
    [InlineData("def x = ();")]
    [InlineData("def x = 1 + 2 * 3;")]
    [InlineData("def x = (1 + 2) * 3;")]
    [InlineData("def x = 1 - 2 - 3;")]
    [InlineData("def x = 1 - (2 - 3);")]
    [InlineData("def x = 8 / 4 / 2;")]
    [InlineData("def x = 8 / (4 / 2);")]
    [InlineData("def x = -y + z;")]
    [InlineData("def x = !(a == b);")]
    [InlineData("def x = a < b;")]
    [InlineData("def x: Int = 1;")]
    [InlineData("def x: [Int;?] = y;")]
    [InlineData("def x: [Int;3] = y;")]
    [InlineData("f();")]
    [InlineData("f(1, 2);")]
    [InlineData("f(g(1));")]
    [InlineData("def f = fn() Int { return 1; };")]
    [InlineData("def f = fn(a: Int, b: Int) Int { return a + b; };")]
    [InlineData("def f = fn() { };")]
    [InlineData("def f = fn() { return; };")]
    [InlineData("def f = fn(g: fn(Int) Int) Int { return g(1); };")]
    [InlineData("if c { a(); } else { b(); }")]
    [InlineData("if c { a(); }")]
    [InlineData("def x = { def y = 1; y + 1 };")]
    [InlineData("def x = a && b;")]
    [InlineData("def x = a || b;")]
    [InlineData("def a = .[1, 2, 3];")]
    [InlineData("def a = .[];")]
    [InlineData("def a = .[.[1], .[2]];")]
    [InlineData("def a = .[Int; 0; 8];")]
    [InlineData("def a = .[Option<Int>; Option<Int>.None; 3];")]
    [InlineData("def a = .[[Int;2]; b; 3];")]
    [InlineData("def a = .[Int; 0; n + 1];")]
    [InlineData("def r = a[0];")]
    [InlineData("def r = a[0][1];")]
    [InlineData("def v = A.B;")]
    [InlineData("def v = A.B.C;")]
    [InlineData("def v = Result.Ok(1);")]
    [InlineData("def C = enum { Red, Green };")]
    [InlineData("def C = enum { Wrap(Int) };")]
    [InlineData("def R = enum<T, E> { Ok(T), Err(E) };")]
    [InlineData("def r: Result<Int, Str> = x;")]
    [InlineData("def a: [[Int;1];?] = x;")]
    [InlineData("def x = match v { _ => 1 };")]
    [InlineData("def x = match v { Color.Red => 1, _ => 2 };")]
    [InlineData("def x = match v { Result.Ok(a) => a, Result.Err(e) => 0 };")]
    [InlineData("def x = match v { A.B(C.D(a)) => a };")]
    [InlineData("def x = match v { 1 => \"um\", -2 => \"menos dois\", _ => \"outro\" };")]
    [InlineData("def x = match v { true => 1, false => 2 };")]
    [InlineData("def T = type { };")]
    [InlineData("def T = type { id: Int; name: Str; };")]
    [InlineData("def T = type<A, B> { first: A; second: B; };")]
    [InlineData("def u = .User { id: 1, name: \"g\" };")]
    [InlineData("def u = .Unit { };")]
    [InlineData("def u = .Box<Int> { value: 1 };")]
    [InlineData("def u = .A { inner: .B { v: 1 } };")]
    [InlineData("def v = .P { x: 1 }.x;")]
    [InlineData("def f = fn<T>(v: T) T { return v; };")]
    [InlineData("def f = fn<T, N: Int>(v: T) T { return v; };")]
    [InlineData("def f = fn<Make: fn() Int>() Int { return Make(); };")]
    [InlineData("def x = identity<Int>(10);")]
    [InlineData("def x = f<Int, Str>(a, b);")]
    [InlineData("def x = Result<Int, Str>.Ok(1);")]
    [InlineData("def T = type<A, N: Int> { first: A; };")]
    [InlineData("def x: FixedArray<Int, 3> = y;")]
    [InlineData("def x: Tagged<-1> = y;")]
    [InlineData("def x: Labeled<\"v\"> = y;")]
    [InlineData("def t = SomeType<\"value\", 1, true, Int, fn() Int { return 1; }>;")]
    [InlineData("def u = .Boxed<Int, 3> { value: 1 };")]
    [InlineData("def x = 100000000000000000000.0;")]
    [InlineData("def x = 0.00001234;")]
    [InlineData("def x = 0.000000000000000000000001;")]
    [InlineData("goto fim;\nlabel fim;")]
    [InlineData("goto fim if c;\nprint(1);\nlabel fim;\nprint(2);")]
    [InlineData("def x = 1;\ngoto fim;\ndef y = 2;\nlabel fim;\nprint(x);")]
    [InlineData("goto c;\nlabel a;\nlabel b;\nlabel c;\nprint(1);")]
    [InlineData("label a;\ngoto a;")]
    [InlineData("goto b;\nlabel a;\nprint(1);\nlabel b;\ngoto a;")]
    [InlineData("def f = fn() Void { goto fim; print(1); label fim; };")]
    [InlineData("def x = { goto fim; label fim; 1 };")]
    [InlineData("var x = 1;")]
    [InlineData("var x: Int = 1;")]
    [InlineData("var x = 1;\nx = 2;")]
    [InlineData("def T = type { a: Int; };\ndef T.m = 1;")]
    [InlineData("def T = type { a: Int; };\ndef T.m: Int = 1;")]
    [InlineData("var u = x;\nu.a = 1;")]
    [InlineData("var u = x;\nu.a.b = 1;")]
    [InlineData("var i = 0;\nlabel repete;\ni = i + 1;\ngoto repete if i < 3;")]
    [InlineData("def f = fn() Int { var x = 1; x = 2; return x; };")]
    public void PrintThenReparse_ProducesTheSameCore(string source)
    {
        var original = Print(source);
        var printed = PrintSource(source);

        var reparsed = Print(printed);

        reparsed.ShouldBe(original, $"código impresso:\n{printed}");
    }

    [Fact]
    public void HelloExample_RoundTrips()
    {
        const string Source = """
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            def main = fn() Void {
                def result = add(10, 20);

                print(result);
            };

            main();
            """;

        Print(PrintSource(Source)).ShouldBe(Print(Source));
    }

    [Fact]
    public void AbsExample_RoundTrips()
    {
        const string Source = """
            def abs = fn(x: Int) Int {
                if x < 0 {
                    return -x;
                }

                return x;
            };
            """;

        Print(PrintSource(Source)).ShouldBe(Print(Source));
    }

    [Fact]
    public void PrintedSource_IsReadable()
    {
        var printed = PrintSource("def add = fn(a: Int, b: Int) Int { return a + b; };");

        printed.ShouldBe("""
            def add = fn(a: Int, b: Int) Int {
                return a + b;
            };

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Printer_ParenthesizesOnlyWhenNeeded()
    {
        PrintSource("def x = 1 + 2 * 3;").ShouldContain("1 + 2 * 3");
        PrintSource("def x = (1 + 2) * 3;").ShouldContain("(1 + 2) * 3");
        PrintSource("def x = 1 - (2 - 3);").ShouldContain("1 - (2 - 3)");
    }

    [Fact]
    public void Printer_KeepsFloatDecimalPoint() => PrintSource("def x = 1.0;").ShouldContain("1.0");

    /// <summary>
    /// A gramática de float da 0.2 é <c>dígitos "." dígitos</c> (spec §6) e não tem
    /// expoente: imprimir <c>1E+20</c> daria código que a própria linguagem não
    /// reparseia — e o round-trip acima é justamente o que não pode cair.
    /// </summary>
    [Theory]
    [InlineData("def x = 100000000000000000000.0;", "100000000000000000000.0")]
    [InlineData("def x = 0.00001234;", "0.00001234")]
    [InlineData("def x = -1500.0;", "1500.0")]
    public void Printer_NeverUsesScientificNotation(string source, string expected)
    {
        var printed = PrintSource(source);

        printed.ShouldContain(expected);
        printed.ShouldNotContain("E+");
        printed.ShouldNotContain("E-");
    }
}

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
    [InlineData("def x: Int[] = y;")]
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
    [InlineData("def a = [1, 2, 3];")]
    [InlineData("def a = [];")]
    [InlineData("def a = [[1], [2]];")]
    [InlineData("def r = a[0];")]
    [InlineData("def r = a[0][1];")]
    [InlineData("def v = A.B;")]
    [InlineData("def v = A.B.C;")]
    [InlineData("def v = Result.Ok(1);")]
    [InlineData("def C = enum { Red, Green };")]
    [InlineData("def C = enum { Wrap(Int) };")]
    [InlineData("def R = enum<T, E> { Ok(T), Err(E) };")]
    [InlineData("def r: Result<Int, IndexError> = x;")]
    [InlineData("def a: Int[][] = x;")]
    [InlineData("def x = match v { _ => 1 };")]
    [InlineData("def x = match v { Color.Red => 1, _ => 2 };")]
    [InlineData("def x = match v { Result.Ok(a) => a, Result.Err(e) => 0 };")]
    [InlineData("def x = match v { A.B(C.D(a)) => a };")]
    [InlineData("def x = match v { 1 => \"um\", -2 => \"menos dois\", _ => \"outro\" };")]
    [InlineData("def x = match v { true => 1, false => 2 };")]
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
}

using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

public sealed class RecoveryTests : ParserTestBase
{
    [Fact]
    public void AfterBadStatement_ContinuesParsingFile()
    {
        var (file, diagnostics) = ParseWithDiagnostics("def x 1; def y = 2;");

        diagnostics.ShouldNotBeEmpty();
        file.Statements.Length.ShouldBe(2);
    }

    [Fact]
    public void UnclosedBrace_ReportsLap0108_WithoutHanging()
    {
        var codes = Codes("def f = fn() { ");

        codes.ShouldContain(DiagnosticCodes.UnclosedBrace);
    }

    [Fact]
    public void UnexpectedEof_ReportsLap0109() =>
        Codes("def x =").ShouldContain(DiagnosticCodes.UnexpectedEndOfFile);

    [Fact]
    public void MultipleErrors_AreAllReported()
    {
        var codes = Codes("def a 1; def b 2; def c 3;");

        codes.Count(c => c == DiagnosticCodes.ExpectedEquals).ShouldBe(3);
    }

    [Fact]
    public void BadTokenFromLexer_DoesNotProduceDuplicateError()
    {
        var (_, diagnostics) = ParseWithDiagnostics("#");

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnexpectedCharacter);
    }

    [Fact]
    public void DeepNesting_ReportsLap0115_WithoutStackOverflow()
    {
        var source = new string('(', 500) + "1" + new string(')', 500) + ";";

        var codes = Codes(source);

        codes.ShouldContain(DiagnosticCodes.ExpressionTooDeep);
    }

    /// <summary>
    /// Estourado o limite, cada uma das ~200 chamadas ainda no ar reportaria o seu
    /// <c>)</c> faltante ao desempilhar — 201 erros para um problema só, com o
    /// único útil enterrado no topo.
    /// </summary>
    [Fact]
    public void DeepNesting_ReportsExactlyOneDiagnostic()
    {
        var source = new string('(', 500) + "1" + new string(')', 500) + ";";

        Codes(source).ShouldHaveSingleItem().ShouldBe(DiagnosticCodes.ExpressionTooDeep);
    }

    /// <summary>
    /// E a supressão termina no statement seguinte: silenciar o resto do arquivo
    /// seria trocar uma cascata por uma omissão.
    /// </summary>
    [Fact]
    public void AfterDeepNesting_NextStatementIsStillChecked()
    {
        var source = new string('(', 500) + "1" + new string(')', 500) + "; def a 1;";

        Codes(source).ShouldBe([DiagnosticCodes.ExpressionTooDeep, DiagnosticCodes.ExpectedEquals]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("// só comentário")]
    [InlineData(";;;")]
    [InlineData("}")]
    [InlineData(")")]
    [InlineData("def")]
    [InlineData("fn")]
    [InlineData("return")]
    [InlineData("if")]
    [InlineData("else")]
    [InlineData("= = =")]
    [InlineData("((((")]
    [InlineData("{{{{")]
    [InlineData("def def def")]
    [InlineData("match 1 { _ => a b }")]
    [InlineData("match 1 { _ => a = 1 }")]
    [InlineData("var")]
    [InlineData("var =")]
    [InlineData("x =")]
    [InlineData("= 1;")]
    [InlineData("goto")]
    [InlineData("label")]
    [InlineData("goto if;")]
    [InlineData("throw")]
    [InlineData("throw;")]
    [InlineData("throw throw")]
    public void Parser_NeverThrows(string source) =>
        Should.NotThrow(() => ParseWithDiagnostics(source));

    [Fact]
    public void EmptyFile_HasNoStatements() => Parse(string.Empty).Statements.ShouldBeEmpty();

    [Fact]
    public void OnlyComments_HasNoStatements() => Parse("// a\n/* b */").Statements.ShouldBeEmpty();
}

public sealed class SnapshotTests : ParserTestBase
{
    /// <summary>Teste-âncora do M1: o exemplo da spec §34.</summary>
    [Fact]
    public void HelloExample_Snapshot()
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

        Print(Source).ShouldBe(
            """
            (source-file
              (def add
                (fn ((a Int) (b Int)) Int
                  (block
                    (stmt
                      (return
                        (binary +
                          (name a)
                          (name b)
                        )
                      )
                    )
                  )
                )
              )
              (def main
                (fn () Void
                  (block
                    (def result
                      (call
                        (name add)
                        (int 10)
                        (int 20)
                      )
                    )
                    (stmt
                      (call
                        (name print)
                        (name result)
                      )
                    )
                  )
                )
              )
              (stmt
                (call
                  (name main)
                )
              )
            )
            """);
    }

    /// <summary>Retorno antecipado da spec §12.</summary>
    [Fact]
    public void AbsExample_Snapshot()
    {
        const string Source = """
            def abs = fn(x: Int) Int {
                if x < 0 {
                    return -x;
                }

                return x;
            };
            """;

        Print(Source).ShouldBe(
            """
            (source-file
              (def abs
                (fn ((x Int)) Int
                  (block
                    (stmt
                      (if
                        (binary <
                          (name x)
                          (int 0)
                        )
                        (block
                          (stmt
                            (return
                              (unary -
                                (name x)
                              )
                            )
                          )
                        )
                      )
                    )
                    (stmt
                      (return
                        (name x)
                      )
                    )
                  )
                )
              )
            )
            """);
    }

    [Fact]
    public void ClosureExample_FromSpecSection32()
    {
        const string Source = """
            def multiplier = 10;

            def multiply = fn(x: Int) Int {
                return x * multiplier;
            };
            """;

        Should.NotThrow(() => Parse(Source));
    }

    [Fact]
    public void Examples_HelloLs_Parses()
    {
        var path = FindRepoFile(Path.Combine("examples", "hello.ls"));

        var (_, diagnostics) = ParseWithDiagnostics(File.ReadAllText(path));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Examples_FunctionsLs_Parses()
    {
        var path = FindRepoFile(Path.Combine("examples", "functions.ls"));

        var (_, diagnostics) = ParseWithDiagnostics(File.ReadAllText(path));

        diagnostics.ShouldBeEmpty();
    }

    internal static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"não encontrado a partir de {AppContext.BaseDirectory}", relativePath);
    }
}

public sealed class SpanTests : ParserTestBase
{
    [Fact]
    public void Binary_SpanCoversBothOperands()
    {
        var file = Parse("1 + 2;");
        var expression = file.Statements[0].ShouldBeOfType<Ast.Surface.ExpressionStatement>().Expression;

        expression.Span.Start.ShouldBe(0);
        expression.Span.End.ShouldBe(5);
    }

    [Fact]
    public void Call_SpanCoversCalleeAndParens()
    {
        var file = Parse("main();");
        var expression = file.Statements[0].ShouldBeOfType<Ast.Surface.ExpressionStatement>().Expression;

        expression.Span.Start.ShouldBe(0);
        expression.Span.End.ShouldBe(6);
    }

    [Fact]
    public void EveryNode_HasSpanWithinFile()
    {
        const string Source = "def add = fn(a: Int, b: Int) Int { return a + b; };";
        var file = Parse(Source);

        foreach (var statement in file.Statements)
        {
            statement.Span.Start.ShouldBeGreaterThanOrEqualTo(0);
            statement.Span.End.ShouldBeLessThanOrEqualTo(Source.Length);
        }
    }
}

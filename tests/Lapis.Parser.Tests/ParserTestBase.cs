using System.Collections.Immutable;
using Lapis.Ast.Printing;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Parser.Tests;

public abstract class ParserTestBase
{
    protected static (SourceFile File, ImmutableArray<Diagnostic> Diagnostics) ParseWithDiagnostics(string source)
    {
        var diagnostics = new DiagnosticBag();
        var file = Parser.Parse(SourceText.From(source), diagnostics);
        return (file, diagnostics.ToSortedArray());
    }

    protected static SourceFile Parse(string source)
    {
        var (file, diagnostics) = ParseWithDiagnostics(source);

        diagnostics.ShouldBeEmpty(
            $"esperado parse limpo, obtido: {string.Join(", ", diagnostics.Select(d => d.Code))}");

        return file;
    }

    /// <summary>S-expression do arquivo inteiro, para comparação em snapshot inline.</summary>
    protected static string Print(string source) => SurfaceSExprPrinter.Print(Parse(source));

    /// <summary>S-expression da única expressão de um arquivo com um statement.</summary>
    protected static string PrintExpression(string source)
    {
        var file = Parse(source);
        var statement = file.Statements.ShouldHaveSingleItem().ShouldBeOfType<ExpressionStatement>();
        return SurfaceSExprPrinter.Print(statement.Expression);
    }

    protected static ImmutableArray<string> Codes(string source) =>
        [.. ParseWithDiagnostics(source).Diagnostics.Select(d => d.Code)];

    protected static Diagnostic SingleDiagnostic(string source) =>
        ParseWithDiagnostics(source).Diagnostics.ShouldHaveSingleItem();

    /// <summary>Compara ignorando indentação, para que os testes fiquem legíveis.</summary>
    protected static void ShouldPrintAs(string source, string expected)
    {
        var actual = PrintExpression(source);
        Normalize(actual).ShouldBe(Normalize(expected));
    }

    /// <summary>
    /// Como <see cref="ShouldPrintAs"/>, mas para o statement inteiro — um
    /// <c>def</c> ou uma atribuição não são expressões (nem devem ser).
    /// </summary>
    protected static void ShouldPrintStatementAs(string source, string expected)
    {
        var statement = Parse(source).Statements.ShouldHaveSingleItem();
        Normalize(SurfaceSExprPrinter.Print(statement)).ShouldBe(Normalize(expected));
    }

    /// <summary>
    /// Colapsa espaço em branco e junta os fechamentos, para que a forma indentada
    /// do printer e a forma compacta escrita no teste comparem iguais.
    /// </summary>
    private static string Normalize(string text)
    {
        var collapsed = string.Join(" ", text.Split((char[])['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries));

        return collapsed.Replace(" )", ")", StringComparison.Ordinal);
    }
}

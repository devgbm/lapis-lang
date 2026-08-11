using System.Collections.Immutable;
using Lapis.Ast.Printing;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Macros.Tests;

public abstract class MacroTestBase
{
    protected static (SourceFile File, ImmutableArray<Diagnostic> Diagnostics) ParseWithDiagnostics(string source)
    {
        var diagnostics = new DiagnosticBag();
        var file = Parser.Parser.Parse(SourceText.From(source), diagnostics);
        return (file, diagnostics.ToSortedArray());
    }

    protected static SourceFile Parse(string source)
    {
        var (file, diagnostics) = ParseWithDiagnostics(source);

        diagnostics.ShouldBeEmpty(
            $"esperado parse limpo, obtido: {string.Join(", ", diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        return file;
    }

    protected static (SourceFile File, ImmutableArray<Diagnostic> Diagnostics) ExpandWithDiagnostics(string source)
    {
        var diagnostics = new DiagnosticBag();
        var file = Parser.Parser.Parse(SourceText.From(source), diagnostics);

        diagnostics.HasErrors.ShouldBeFalse(
            $"erro de parse: {string.Join(", ", diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        var expanded = MacroExpander.Expand(file, diagnostics);
        return (expanded, diagnostics.ToSortedArray());
    }

    /// <summary>A Surface AST depois da expansão, em S-expression.</summary>
    protected static string Expand(string source)
    {
        var (file, diagnostics) = ExpandWithDiagnostics(source);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty(
            $"erro de expansão: {string.Join(", ", diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        return SurfaceSExprPrinter.Print(file);
    }

    protected static ImmutableArray<string> ExpansionCodes(string source) =>
        [.. ExpandWithDiagnostics(source).Diagnostics.Select(d => d.Code)];

    protected static Diagnostic ShouldFailWith(string source, string code)
    {
        var (_, diagnostics) = ExpandWithDiagnostics(source);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        errors.ShouldContain(
            d => d.Code == code,
            $"esperado {code}, obtido: {string.Join(", ", errors.Select(d => d.Code))}");

        return errors.First(d => d.Code == code);
    }

    /// <summary>
    /// Compara a expansão com o mesmo programa escrito à mão. É a asserção que
    /// importa: uma macro tem de produzir <b>exatamente</b> o que alguém escreveria.
    /// </summary>
    protected static void ShouldExpandTo(string source, string equivalent) =>
        Expand(source).ShouldBe(SurfaceSExprPrinter.Print(Parse(equivalent)));

    /// <summary>A declaração de macro do primeiro statement.</summary>
    protected static MacroDeclaration Declaration(string source) =>
        Parse(source).Statements[0].ShouldBeOfType<MacroDeclaration>();
}

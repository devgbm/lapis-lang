using System.Collections.Immutable;
using Lapis.Ast.Core;
using Lapis.Ast.Typed;
using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker.Tests;

public abstract class TypeCheckerTestBase
{
    protected static (TypedProgram Program, ImmutableArray<Diagnostic> Diagnostics) CheckWithDiagnostics(string source)
    {
        var diagnostics = new DiagnosticBag();
        var file = Parser.Parser.Parse(SourceText.From(source), diagnostics);

        diagnostics.HasErrors.ShouldBeFalse(
            $"erro de parse: {string.Join(", ", diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        var core = Desugar.Desugarer.Desugar(file, diagnostics);
        var typed = TypeChecker.Check(core, PreludeFixture.Scope, diagnostics);

        return (typed, diagnostics.ToSortedArray());
    }

    protected static TypedProgram Check(string source)
    {
        var (program, diagnostics) = CheckWithDiagnostics(source);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty(
            $"esperado sem erros, obtido: {string.Join(", ", diagnostics.Select(d => $"{d.Code} {d.Message}"))}");

        return program;
    }

    protected static ImmutableArray<string> Codes(string source) =>
        [.. CheckWithDiagnostics(source).Diagnostics.Select(d => d.Code)];

    /// <summary>Asserta que a checagem falha exatamente com o código dado, uma vez.</summary>
    protected static Diagnostic ShouldFailWith(string source, string code)
    {
        var (_, diagnostics) = CheckWithDiagnostics(source);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        errors.ShouldContain(
            d => d.Code == code,
            $"esperado {code}, obtido: {string.Join(", ", errors.Select(d => d.Code))}");

        return errors.First(d => d.Code == code);
    }

    protected static void ShouldPass(string source) => Check(source);

    /// <summary>Tipo do valor do primeiro <c>def</c> do programa.</summary>
    protected static LapisType TypeOfFirstDef(string source)
    {
        var program = Check(source);
        var let = program.Program.Body.ShouldBeOfType<CoreLet>();
        return program.TypeOf(let.Value);
    }

    /// <summary>Tipo do valor do <c>def</c> com o nome dado.</summary>
    protected static LapisType TypeOfDef(string source, string name)
    {
        var program = Check(source);
        var current = program.Program.Body;

        while (current is CoreLet let)
        {
            if (!let.IsSynthetic && let.Name == name)
            {
                return program.TypeOf(let.Value);
            }

            current = let.Body;
        }

        throw new InvalidOperationException($"'{name}' não encontrado no programa");
    }
}

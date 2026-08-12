using System.Collections.Immutable;
using Lapis.Ast.Core;
using Lapis.Ast.Printing;
using Lapis.Diagnostics;

namespace Lapis.Desugar.Tests;

public abstract class DesugarTestBase
{
    protected static CoreProgram Compile(string source)
    {
        var diagnostics = new DiagnosticBag();
        var file = Parser.Parser.Parse(SourceText.From(source), diagnostics);

        diagnostics.HasErrors.ShouldBeFalse(
            $"erro de parse: {string.Join(", ", diagnostics.Select(d => d.Code))}");

        var program = Desugarer.Desugar(file, diagnostics);

        diagnostics.HasErrors.ShouldBeFalse();
        return program;
    }

    /// <summary>Para diagnósticos que o próprio desugar reporta (plano 25 — <c>is</c>).</summary>
    protected static ImmutableArray<string> CompileCodes(string source)
    {
        var diagnostics = new DiagnosticBag();
        var file = Parser.Parser.Parse(SourceText.From(source), diagnostics);

        diagnostics.HasErrors.ShouldBeFalse(
            $"erro de parse: {string.Join(", ", diagnostics.Select(d => d.Code))}");

        Desugarer.Desugar(file, diagnostics);

        return [.. diagnostics.Select(d => d.Code)];
    }

    protected static string Print(string source) => CoreSExprPrinter.Print(Compile(source));

    protected static string PrintSource(string source) => CoreSourcePrinter.Print(Compile(source));

    protected static void ShouldDesugarTo(string source, string expected) =>
        Normalize(Print(source)).ShouldBe(Normalize(expected));

    /// <summary>Todos os nós da Core, em pré-ordem.</summary>
    protected static List<CoreExpr> AllNodes(CoreProgram program)
    {
        var collector = new NodeCollector();
        collector.Visit(program.Body);
        return collector.Nodes;
    }

    private static string Normalize(string text)
    {
        var collapsed = string.Join(
            " ",
            text.Split((char[])['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries));

        return collapsed.Replace(" )", ")", StringComparison.Ordinal);
    }

    private sealed class NodeCollector : CoreWalker
    {
        public List<CoreExpr> Nodes { get; } = [];

        protected override void OnNode(CoreExpr node) => Nodes.Add(node);
    }
}

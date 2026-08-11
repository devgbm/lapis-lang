namespace Lapis.Cli.Tests;

/// <summary>
/// Trava o grafo de dependências definido em plans/00-architecture-and-conventions.md §2.
/// Essas regras são a parte da arquitetura mais cara de corrigir depois — por isso
/// existem desde o M0, com os projetos ainda vazios.
/// </summary>
public sealed class ArchitectureTests
{
    [Fact]
    public void Diagnostics_HasNoProjectReferences()
    {
        RepoLayout.Project("Lapis.Diagnostics").ProjectReferences.ShouldBeEmpty();
    }

    /// <summary>
    /// A Surface AST é a saída do parser, e uma invocação de macro carrega
    /// <b>tokens crus</b>: o parser não conhece a forma de <c>@unless</c> até a
    /// macro estar registrada, então ele delimita e entrega os tokens (spec de
    /// macros §9, plano 17 §17.2). Isso torna <c>Token</c> parte da representação
    /// de superfície, e é por isso que <c>Lapis.Ast</c> enxerga o lexer.
    ///
    /// A dependência inversa nunca existiu de fato — o lexer referenciava
    /// <c>Lapis.Ast</c> sem usar nada de lá.
    /// </summary>
    [Fact]
    public void Ast_DependsOnlyOnLexerAndDiagnostics()
    {
        // spec §36: Lapis.Ast não deve conhecer o evaluator.
        RepoLayout.Project("Lapis.Ast").TransitiveReferences()
            .ShouldBe(["Lapis.Lexer", "Lapis.Diagnostics"], ignoreOrder: true);
    }

    [Fact]
    public void Lexer_DependsOnlyOnDiagnostics()
    {
        RepoLayout.Project("Lapis.Lexer").TransitiveReferences()
            .ShouldBe(["Lapis.Diagnostics"], ignoreOrder: true);
    }

    /// <summary>
    /// A expansão é anterior ao checker e ao evaluator, e não pode enxergar
    /// nenhum dos dois. O plano 18 quebra isto de propósito, para rodar
    /// <c>constraint</c>, e explica como.
    /// </summary>
    [Fact]
    public void Macros_DoesNotReachTheCheckerOrTheEvaluator()
    {
        var references = RepoLayout.Project("Lapis.Macros").TransitiveReferences();

        references.ShouldNotContain("Lapis.TypeChecker");
        references.ShouldNotContain("Lapis.Evaluator");
        references.ShouldNotContain("Lapis.Runtime");
    }

    [Theory]
    [InlineData("Lapis.Parser")]
    [InlineData("Lapis.Desugar")]
    public void FrontEnd_DoesNotReferenceRuntime(string project)
    {
        // spec §36: o parser e o desugar não executam código.
        RepoLayout.Project(project).TransitiveReferences().ShouldNotContain("Lapis.Runtime");
    }

    [Theory]
    [InlineData("Lapis.Parser")]
    [InlineData("Lapis.Desugar")]
    [InlineData("Lapis.TypeChecker")]
    [InlineData("Lapis.Runtime")]
    public void NothingBelowEvaluator_ReferencesEvaluator(string project)
    {
        RepoLayout.Project(project).TransitiveReferences().ShouldNotContain("Lapis.Evaluator");
    }

    [Fact]
    public void Runtime_DoesNotReferenceTypeChecker()
    {
        // A quebra do ciclo Runtime ↔ TypeChecker: o prelude é carregado pelo
        // orquestrador (Lapis.Cli), não pelo runtime. Ver plans/09-prelude.md §9.3.
        RepoLayout.Project("Lapis.Runtime").TransitiveReferences().ShouldNotContain("Lapis.TypeChecker");
    }

    [Fact]
    public void PartialEvaluator_DoesNotReferenceCli()
    {
        RepoLayout.Project("Lapis.PartialEvaluator").TransitiveReferences().ShouldNotContain("Lapis.Cli");
    }

    [Fact]
    public void Cli_IsTheOnlyProjectReferencingEverything()
    {
        var cli = RepoLayout.Project("Lapis.Cli").TransitiveReferences();

        foreach (var project in RepoLayout.SourceProjects.Where(p => p.Name != "Lapis.Cli"))
        {
            cli.ShouldContain(project.Name);
        }
    }

    [Fact]
    public void NoProjectReferenceCycles()
    {
        // Ordenação topológica: se sobrar algum projeto, há ciclo.
        var remaining = RepoLayout.AllProjects.ToDictionary(p => p.Name, p => p.ProjectReferences.ToHashSet());

        while (remaining.Count > 0)
        {
            var ready = remaining.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();

            ready.ShouldNotBeEmpty($"ciclo de referências entre: {string.Join(", ", remaining.Keys.Order())}");

            foreach (var name in ready)
            {
                remaining.Remove(name);
            }

            foreach (var dependencies in remaining.Values)
            {
                dependencies.ExceptWith(ready);
            }
        }
    }

    [Fact]
    public void NoSourceProject_ReferencesATestProject()
    {
        foreach (var project in RepoLayout.SourceProjects)
        {
            project.ProjectReferences.ShouldAllBe(r => !r.EndsWith(".Tests", StringComparison.Ordinal));
        }
    }
}

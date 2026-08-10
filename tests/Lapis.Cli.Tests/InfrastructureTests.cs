using System.Text.RegularExpressions;

namespace Lapis.Cli.Tests;

/// <summary>
/// Garante que a configuração comum (Directory.Build.props, versionamento central)
/// continue valendo para todos os projetos — plans/01-solution-skeleton.md §"Testes de infraestrutura".
/// </summary>
public sealed class InfrastructureTests
{
    [Fact]
    public void EveryProject_InheritsTargetFramework()
    {
        // Nenhum .csproj pode fixar o TFM localmente: ele vem do Directory.Build.props.
        foreach (var project in RepoLayout.AllProjects)
        {
            project.Document.Descendants("TargetFramework").ShouldBeEmpty(
                $"{project.Name} sobrescreve TargetFramework");
            project.Document.Descendants("TargetFrameworks").ShouldBeEmpty(
                $"{project.Name} sobrescreve TargetFrameworks");
        }
    }

    [Fact]
    public void EveryProject_InheritsNullableAndWarningsAsErrors()
    {
        foreach (var project in RepoLayout.AllProjects)
        {
            project.Document.Descendants("Nullable").ShouldBeEmpty($"{project.Name} sobrescreve Nullable");
            project.Document.Descendants("TreatWarningsAsErrors").ShouldBeEmpty(
                $"{project.Name} sobrescreve TreatWarningsAsErrors");
        }
    }

    [Fact]
    public void NoPackageReference_PinsItsOwnVersion()
    {
        // Versionamento central: as versões vivem só em Directory.Packages.props.
        foreach (var project in RepoLayout.AllProjects)
        {
            foreach (var reference in project.Document.Descendants("PackageReference"))
            {
                reference.Attribute("Version").ShouldBeNull(
                    $"{project.Name} fixa a versão de {reference.Attribute("Include")?.Value}");
            }
        }
    }

    [Fact]
    public void CentralPackageManagement_IsEnabled()
    {
        var path = Path.Combine(RepoLayout.Root, "Directory.Packages.props");

        File.Exists(path).ShouldBeTrue();
        File.ReadAllText(path).ShouldContain("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
    }

    [Fact]
    public void InvariantGlobalization_IsEnabled()
    {
        // Sem isso, `3.14` poderia ser formatado como `3,14` (plano 00 §3).
        var path = Path.Combine(RepoLayout.Root, "Directory.Build.props");

        File.ReadAllText(path).ShouldContain("<InvariantGlobalization>true</InvariantGlobalization>");
    }

    [Fact]
    public void CliAssembly_IsNamedLapis()
    {
        // spec §33: o projeto produz um executável chamado `lapis`.
        RepoLayout.Project("Lapis.Cli").Document
            .Descendants("AssemblyName")
            .Select(e => e.Value)
            .ShouldContain("lapis");
    }

    [Fact]
    public void EverySourceProject_HasATestProjectOrIsExempt()
    {
        // Lapis.Ast, Lapis.Diagnostics e Lapis.Runtime são exercitados pelos testes
        // dos projetos que os consomem (planos 02, 07).
        string[] exempt = ["Lapis.Ast", "Lapis.Diagnostics", "Lapis.Runtime"];

        foreach (var project in RepoLayout.SourceProjects.Where(p => !exempt.Contains(p.Name)))
        {
            RepoLayout.AllProjects.ShouldContain(
                p => p.Name == $"{project.Name}.Tests",
                $"{project.Name} não possui projeto de teste");
        }
    }

    [Theory]
    [InlineData("hello.ls")]
    [InlineData("functions.ls")]
    [InlineData("arrays.ls")]
    [InlineData("result.ls")]
    [InlineData("types.ls")]
    [InlineData("generics.ls")]
    public void Examples_FromSpecSection35_Exist(string fileName)
    {
        File.Exists(Path.Combine(RepoLayout.Root, "examples", fileName)).ShouldBeTrue();
    }

    [Fact]
    public void PlansAndSpec_AreVersioned()
    {
        Directory.Exists(Path.Combine(RepoLayout.Root, "plans")).ShouldBeTrue();
        File.Exists(Path.Combine(RepoLayout.Root, "spec", "lapislang-0.2.md")).ShouldBeTrue();
        File.Exists(Path.Combine(RepoLayout.Root, "spec", "lapislang-macros-0.1.md")).ShouldBeTrue();
    }

    /// <summary>
    /// Todo plano referenciado pelo índice existe. Um índice que aponta para o
    /// vazio é pior que índice nenhum, e é o tipo de coisa que só se percebe meses
    /// depois — daí o teste.
    /// </summary>
    [Fact]
    public void PlansIndex_HasNoDanglingLinks()
    {
        var plans = Path.Combine(RepoLayout.Root, "plans");
        var index = File.ReadAllText(Path.Combine(plans, "README.md"));

        var referenced = Regex.Matches(index, @"\]\((?<file>[0-9a-z-]+\.md)\)")
            .Select(m => m.Groups["file"].Value)
            .Distinct(StringComparer.Ordinal);

        foreach (var file in referenced)
        {
            File.Exists(Path.Combine(plans, file)).ShouldBeTrue($"plans/README.md aponta para '{file}'");
        }
    }

    /// <summary>
    /// Todo plano declara o milestone a que pertence, e todo milestone declarado
    /// aparece na tabela do índice. É o que impede um plano de ficar órfão do
    /// roteiro depois de uma reordenação.
    /// </summary>
    [Fact]
    public void EveryPlan_DeclaresAMilestoneListedInTheIndex()
    {
        var plans = Path.Combine(RepoLayout.Root, "plans");
        var index = File.ReadAllText(Path.Combine(plans, "README.md"));

        foreach (var path in Directory.EnumerateFiles(plans, "*.md").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);

            // O índice e os apêndices não são planos.
            if (name == "README.md" || name.StartsWith("appendix-", StringComparison.Ordinal))
            {
                continue;
            }

            var milestones = Regex.Matches(File.ReadAllText(path), @"\*\*Milestone:\*\*(?<rest>[^\n]*)")
                .SelectMany(m => Regex.Matches(m.Groups["rest"].Value, @"M(?<n>\d+)"))
                .Select(m => m.Groups["n"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            milestones.ShouldNotBeEmpty($"{name} não declara '**Milestone:**'");

            foreach (var milestone in milestones)
            {
                index.ShouldContain(
                    $"**M{milestone}**",
                    customMessage: $"{name} declara M{milestone}, ausente da tabela de milestones");
            }
        }
    }

    /// <summary>
    /// Nenhum código de diagnóstico do catálogo aparece em duas faixas. Foi
    /// exatamente o que aconteceu ao propor macros em `LAP04xx`, que já era do
    /// partial evaluator.
    /// </summary>
    [Fact]
    public void DiagnosticCatalogue_HasNoDuplicateCodes()
    {
        var catalogue = File.ReadAllText(
            Path.Combine(RepoLayout.Root, "plans", "appendix-b-diagnostics.md"));

        var duplicates = Regex.Matches(catalogue, @"^\|\s*~?~?`(?<code>LAP\d{4})`", RegexOptions.Multiline)
            .Select(m => m.Groups["code"].Value)
            .GroupBy(code => code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        duplicates.ShouldBeEmpty($"códigos repetidos no catálogo: {string.Join(", ", duplicates)}");
    }
}

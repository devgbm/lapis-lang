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
    public void Examples_FromSpecSection35_Exist(string fileName)
    {
        File.Exists(Path.Combine(RepoLayout.Root, "examples", fileName)).ShouldBeTrue();
    }

    [Fact]
    public void PlansAndSpec_AreVersioned()
    {
        Directory.Exists(Path.Combine(RepoLayout.Root, "plans")).ShouldBeTrue();
        File.Exists(Path.Combine(RepoLayout.Root, "spec", "lapislang-0.2.md")).ShouldBeTrue();
    }
}

using System.Collections.Immutable;
using System.Xml.Linq;

namespace Lapis.Cli.Tests;

/// <summary>
/// Acesso ao layout do repositório a partir dos testes.
///
/// Os testes de arquitetura leem os <c>.csproj</c> em vez dos assemblies compilados
/// porque o compilador C# omite referências a assemblies cujos tipos não são usados —
/// enquanto os projetos estiverem vazios, a metadata compilada não diria nada. O que
/// queremos travar é a dependência **declarada**.
/// </summary>
public static class RepoLayout
{
    public static string Root { get; } = FindRoot();

    public static ImmutableArray<ProjectInfo> AllProjects { get; } = LoadProjects();

    public static ImmutableArray<ProjectInfo> SourceProjects { get; } =
        [.. AllProjects.Where(p => p.IsSource)];

    public static ProjectInfo Project(string name) =>
        AllProjects.SingleOrDefault(p => p.Name == name)
        ?? throw new InvalidOperationException($"projeto '{name}' não encontrado");

    /// <summary>Nome do arquivo de solução. O SDK 10 gera o formato XML (<c>.slnx</c>).</summary>
    public const string SolutionFileName = "LapisLang.slnx";

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"{SolutionFileName} não encontrado a partir de {AppContext.BaseDirectory}");
    }

    private static ImmutableArray<ProjectInfo> LoadProjects()
    {
        var builder = ImmutableArray.CreateBuilder<ProjectInfo>();

        foreach (var folder in new[] { "src", "tests" })
        {
            var root = Path.Combine(Root, folder);

            foreach (var file in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
            {
                builder.Add(ProjectInfo.Load(file, isSource: folder == "src"));
            }
        }

        return builder.ToImmutable();
    }
}

public sealed record ProjectInfo(
    string Name,
    string Path,
    bool IsSource,
    ImmutableArray<string> ProjectReferences,
    XDocument Document)
{
    public static ProjectInfo Load(string path, bool isSource)
    {
        var document = XDocument.Load(path);

        var references = document
            .Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(v => v is not null)
            .Select(v => System.IO.Path.GetFileNameWithoutExtension(v!.Replace('\\', '/')))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToImmutableArray();

        return new ProjectInfo(
            System.IO.Path.GetFileNameWithoutExtension(path),
            path,
            isSource,
            references,
            document);
    }

    /// <summary>Fecho transitivo das referências de projeto.</summary>
    public ImmutableHashSet<string> TransitiveReferences()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(ProjectReferences);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (!seen.Add(current))
            {
                continue;
            }

            foreach (var next in RepoLayout.Project(current).ProjectReferences)
            {
                pending.Push(next);
            }
        }

        return [.. seen];
    }
}

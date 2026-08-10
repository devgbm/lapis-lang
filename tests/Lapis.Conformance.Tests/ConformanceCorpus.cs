using System.Collections.Immutable;

namespace Lapis.Conformance.Tests;

/// <summary>
/// Descoberta dos casos em <c>tests/conformance/**/*.ls</c>.
///
/// Um teste xUnit por arquivo, nomeado pelo caminho relativo: quando um caso
/// falha, o nome do teste já é o arquivo a abrir.
/// </summary>
public static class ConformanceCorpus
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string Root { get; } = Path.Combine(RepoRoot, "tests", "conformance");

    public static ImmutableArray<ConformanceCase> All { get; } = Discover();

    /// <summary>Os casos como dados de <c>[Theory]</c>.</summary>
    public static TheoryData<ConformanceCase> Cases()
    {
        var data = new TheoryData<ConformanceCase>();

        foreach (var testCase in All)
        {
            data.Add(testCase);
        }

        return data;
    }

    private static ImmutableArray<ConformanceCase> Discover()
    {
        if (!Directory.Exists(Root))
        {
            return [];
        }

        return
        [
            .. Directory.EnumerateFiles(Root, "*.ls", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .Select(relative => ConformanceCase.Parse(
                    relative,
                    File.ReadAllText(Path.Combine(Root, relative)))),
        ];
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LapisLang.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("raiz do repositório não encontrada a partir de " + AppContext.BaseDirectory);
    }
}

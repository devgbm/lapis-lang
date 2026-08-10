using Lapis.Runtime;

namespace Lapis.TypeChecker.Tests;

/// <summary>
/// O prelude carregado uma vez para toda a suíte. A carga passa pelo pipeline
/// completo, então uma regressão em qualquer fase quebra isto imediatamente.
/// </summary>
public static class PreludeFixture
{
    public static PreludeScope Scope { get; } = Cli.PreludeLoader.Load();
}

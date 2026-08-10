namespace Lapis.TypeChecker.Tests;

/// <summary>
/// Verificação de fiação do M0: o projeto de teste enxerga o assembly que vai exercitar.
/// Os testes reais deste projeto estão especificados em plans/06-typechecker.md e entram
/// junto com a implementação correspondente.
/// </summary>
public sealed class WiringTests
{
    [Fact]
    public void TargetAssembly_IsReferenced()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Lapis.TypeChecker.dll");

        File.Exists(path).ShouldBeTrue($"assembly alvo não encontrado em {path}");
    }
}

namespace Lapis.Desugar;

/// <summary>
/// Gera nomes que o usuário não consegue escrever.
///
/// O prefixo <c>$</c> não é produzível pelo lexer (spec §7: identificadores
/// começam com letra ou <c>_</c>), então um nome fresco nunca captura nem é
/// capturado por um nome do programa (plano 05 §5.4). O mesmo gerador será
/// reutilizado pelo partial evaluator ao especializar funções.
/// </summary>
public sealed class FreshNameGenerator
{
    private int _counter;

    public string Next(string hint = "tmp") => $"${hint}{_counter++}";

    /// <summary>Um nome é fresco se foi gerado por este mecanismo.</summary>
    public static bool IsFresh(string name) => name.StartsWith('$');
}

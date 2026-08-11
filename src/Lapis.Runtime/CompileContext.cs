namespace Lapis.Runtime;

/// <summary>
/// Estado acumulado durante a compilação (spec de macros §8.3, plano 18 §18.3).
///
/// É a <b>única</b> coisa mutável do sistema, e só enquanto a compilação dura.
/// Não é um recurso da linguagem: é estado do compilador exposto por nativas, do
/// mesmo modo que <c>print</c> expõe I/O. A regra "bindings são imutáveis"
/// (spec §8) segue intacta — nenhum <c>def</c> muda de valor por causa disto.
///
/// Chaves e valores são <c>Str</c>. Não é preguiça: os casos reais — registrar,
/// deduplicar, contar — cabem em <c>Str</c>, e um contexto tipado exigiria
/// serialização, que é um subsistema inteiro. A restrição é revisitável sem
/// quebrar nada.
///
/// Uma instância por compilação. É o que faz duas compilações não se enxergarem,
/// e é por isso que este tipo não tem estado estático nenhum.
/// </summary>
public sealed class CompileContext
{
    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);

    public bool Has(string key) => _entries.ContainsKey(key);

    public string? Get(string key) => _entries.GetValueOrDefault(key);

    public void Put(string key, string value) => _entries[key] = value;

    /// <summary>
    /// As chaves com o prefixo dado, em ordem de inserção — a ordem em que as
    /// macros rodaram, que é a ordem do arquivo. Determinismo importa: o
    /// diagnóstico que uma constraint produz não pode depender do humor da tabela
    /// de hash.
    /// </summary>
    public IReadOnlyList<string> Keys(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return [.. _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal))];
    }

    public int Count => _entries.Count;
}

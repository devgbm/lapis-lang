using System.Collections.Immutable;
using System.Text;

namespace Lapis.Runtime;

/// <summary>
/// Destino da saída de <c>print</c>. Existe para que os testes de conformidade
/// possam capturar o que o programa imprimiu — nada escreve em <c>Console</c>
/// diretamente.
/// </summary>
public interface IOutput
{
    void Write(string text);
}

public sealed class ConsoleOutput(TextWriter writer) : IOutput
{
    public void Write(string text) => writer.Write(text);
}

public sealed class StringOutput : IOutput
{
    private readonly StringBuilder _builder = new();

    public string Text => _builder.ToString();

    public void Write(string text) => _builder.Append(text);
}

/// <summary>Invoca uma closure. Fornecido pelo evaluator na construção do contexto.</summary>
public delegate Value Invoker(Value callee, ImmutableArray<Value> arguments);

/// <summary>
/// O que uma primitiva nativa pode usar.
///
/// O <see cref="Invoke"/> é injetado pelo evaluator: sem essa inversão,
/// <c>Lapis.Runtime</c> precisaria referenciar <c>Lapis.Evaluator</c> e o grafo de
/// dependências deixaria de ser acíclico (plano 07 §7.6).
/// </summary>
public sealed class RuntimeContext(IOutput output)
{
    public IOutput Output { get; } = output;

    public Invoker Invoke { get; set; } = (_, _) =>
        throw new Diagnostics.InternalCompilerException("nenhum invocador registrado no contexto");
}

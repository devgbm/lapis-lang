using System.Collections.Immutable;
using Lapis.Ast.Core;
using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.Ast.Typed;

/// <summary>
/// Identidade de um binding, atribuída pelo type checker. Permite distinguir dois
/// <c>x</c> em escopos diferentes sem renomear a árvore.
/// </summary>
public readonly record struct BindingId(int Value);

/// <summary>Decisão do checker que o evaluator precisa consultar.</summary>
public abstract record Resolution;

public sealed record VariableResolution(BindingId Binding) : Resolution;

/// <summary>Argumentos genéricos e o tipo já instanciado de uma chamada.</summary>
public sealed record CallResolution(
    ImmutableArray<LapisType> TypeArguments,
    FunctionType Instantiated) : Resolution;

/// <summary>A variante selecionada por um acesso a membro sobre um enum.</summary>
public sealed record VariantResolution(TypeDefinition Enum, int VariantIndex) : Resolution;

/// <summary>A definição criada por um <c>type</c> ou <c>enum</c>.</summary>
public sealed record TypeDefinitionResolution(TypeDefinition Definition) : Resolution;

/// <summary>
/// Saída do type checker: a mesma Core AST, mais tabelas indexadas por
/// <see cref="CoreExpr.NodeId"/>.
///
/// Não há um segundo conjunto de nós — duplicá-los custaria 16 classes e todos os
/// visitors, sem ganho (plano 02 §2.6).
/// </summary>
public sealed class TypedProgram(
    CoreProgram program,
    ImmutableDictionary<int, LapisType> nodeTypes,
    ImmutableDictionary<int, Resolution> resolutions)
{
    public CoreProgram Program { get; } = program;

    public ImmutableDictionary<int, LapisType> NodeTypes { get; } = nodeTypes;

    public ImmutableDictionary<int, Resolution> Resolutions { get; } = resolutions;

    public LapisType TypeOf(CoreExpr node) =>
        NodeTypes.TryGetValue(node.NodeId, out var type)
            ? type
            : throw new InternalCompilerException($"nó {node.NodeId} sem tipo atribuído", node.Span);

    public TResolution? ResolutionOf<TResolution>(CoreExpr node)
        where TResolution : Resolution =>
        Resolutions.TryGetValue(node.NodeId, out var resolution) ? resolution as TResolution : null;
}

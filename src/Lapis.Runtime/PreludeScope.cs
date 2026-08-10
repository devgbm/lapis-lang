using System.Collections.Immutable;
using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.Runtime;

/// <summary>Um nome exportado pelo prelude, com seu tipo e seu valor.</summary>
public sealed record PreludeBinding(string Name, LapisType Type, Value Value);

/// <summary>
/// As definições do prelude, já resolvidas.
///
/// Este tipo é <b>apenas dado</b>. A carga — rodar o pipeline inteiro sobre
/// <c>prelude.ls</c> — mora no orquestrador, porque colocá-la aqui fecharia o
/// ciclo <c>Runtime → Evaluator → TypeChecker → Runtime</c> (plano 09 §9.3).
///
/// O evaluator constrói <c>Result</c> a partir da <see cref="TypeDefinition"/>
/// guardada aqui, não de uma busca por nome no escopo corrente: assim sombrear
/// <c>Result</c> no programa do usuário não muda a semântica de <c>[]</c>.
/// </summary>
public sealed class PreludeScope
{
    public const string ResultName = "Result";
    public const string IndexErrorName = "IndexError";
    public const string OkVariant = "Ok";
    public const string ErrVariant = "Err";
    public const string OutOfBoundsVariant = "OutOfBounds";

    public PreludeScope(ImmutableArray<PreludeBinding> bindings)
    {
        Bindings = bindings;
        Result = RequireEnum(bindings, ResultName, OkVariant, ErrVariant);
        IndexError = RequireEnum(bindings, IndexErrorName, OutOfBoundsVariant);

        OkVariantIndex = Result.IndexOfVariant(OkVariant);
        ErrVariantIndex = Result.IndexOfVariant(ErrVariant);
        OutOfBoundsIndex = IndexError.IndexOfVariant(OutOfBoundsVariant);

        OutOfBounds = new EnumValue(IndexError, OutOfBoundsIndex, [], []);
        IndexErrorType = new NamedType(IndexError, []);
    }

    public ImmutableArray<PreludeBinding> Bindings { get; }

    public TypeDefinition Result { get; }

    public TypeDefinition IndexError { get; }

    public int OkVariantIndex { get; }

    public int ErrVariantIndex { get; }

    public int OutOfBoundsIndex { get; }

    /// <summary>O valor <c>IndexError.OutOfBounds</c>, que é único e imutável.</summary>
    public EnumValue OutOfBounds { get; }

    public LapisType IndexErrorType { get; }

    public EnumValue MakeOk(Value payload, LapisType okType) =>
        new(Result, OkVariantIndex, [payload], [okType, IndexErrorType]);

    public EnumValue MakeIndexError(LapisType okType) =>
        new(Result, ErrVariantIndex, [OutOfBounds], [okType, IndexErrorType]);

    private static TypeDefinition RequireEnum(
        ImmutableArray<PreludeBinding> bindings,
        string name,
        params string[] variants)
    {
        var binding = bindings.FirstOrDefault(b => b.Name == name)
            ?? throw new InternalCompilerException($"o prelude não define '{name}'");

        if (binding.Type is not MetaType { Definition: { Kind: TypeDefinitionKind.Enum } definition })
        {
            throw new InternalCompilerException($"'{name}' do prelude não é um enum");
        }

        foreach (var variant in variants)
        {
            if (definition.IndexOfVariant(variant) < 0)
            {
                throw new InternalCompilerException($"'{name}' do prelude não tem a variante '{variant}'");
            }
        }

        return definition;
    }
}

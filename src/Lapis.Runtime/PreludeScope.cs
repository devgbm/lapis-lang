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
    public const string ContextErrorName = "ContextError";
    public const string OkVariant = "Ok";
    public const string ErrVariant = "Err";
    public const string OutOfBoundsVariant = "OutOfBounds";
    public const string MissingVariant = "Missing";

    public const string TypeInfoName = "TypeInfo";
    public const string FieldInfoName = "FieldInfo";
    public const string VariantInfoName = "VariantInfo";
    public const string TypeKindName = "TypeKind";
    public const string StructVariant = "Struct";
    public const string EnumVariant = "Enum";

    public PreludeScope(ImmutableArray<PreludeBinding> bindings)
    {
        Bindings = bindings;
        Result = RequireEnum(bindings, ResultName, OkVariant, ErrVariant);
        IndexError = RequireEnum(bindings, IndexErrorName, OutOfBoundsVariant);
        ContextError = RequireEnum(bindings, ContextErrorName, MissingVariant);
        TypeKind = RequireEnum(bindings, TypeKindName, StructVariant, EnumVariant);

        TypeInfo = RequireStruct(bindings, TypeInfoName);
        FieldInfo = RequireStruct(bindings, FieldInfoName);
        VariantInfo = RequireStruct(bindings, VariantInfoName);

        OkVariantIndex = Result.IndexOfVariant(OkVariant);
        ErrVariantIndex = Result.IndexOfVariant(ErrVariant);
        OutOfBoundsIndex = IndexError.IndexOfVariant(OutOfBoundsVariant);

        OutOfBounds = new EnumValue(IndexError, OutOfBoundsIndex, [], []);
        IndexErrorType = new NamedType(IndexError, []);

        Missing = new EnumValue(ContextError, ContextError.IndexOfVariant(MissingVariant), [], []);
        ContextErrorType = new NamedType(ContextError, []);

        TypeInfoType = new NamedType(TypeInfo, []);
    }

    public ImmutableArray<PreludeBinding> Bindings { get; }

    public TypeDefinition Result { get; }

    public TypeDefinition IndexError { get; }

    public TypeDefinition ContextError { get; }

    public TypeDefinition TypeKind { get; }

    public TypeDefinition TypeInfo { get; }

    public TypeDefinition FieldInfo { get; }

    public TypeDefinition VariantInfo { get; }

    /// <summary>O tipo de <c>reflect(...)</c> (plano 19 §19.2).</summary>
    public LapisType TypeInfoType { get; }

    public int OkVariantIndex { get; }

    public int ErrVariantIndex { get; }

    public int OutOfBoundsIndex { get; }

    /// <summary>O valor <c>IndexError.OutOfBounds</c>, que é único e imutável.</summary>
    public EnumValue OutOfBounds { get; }

    public LapisType IndexErrorType { get; }

    /// <summary>O valor <c>ContextError.Missing</c>, único e imutável.</summary>
    public EnumValue Missing { get; }

    public LapisType ContextErrorType { get; }

    public EnumValue MakeOk(Value payload, LapisType okType) =>
        new(Result, OkVariantIndex, [payload], IndexResultArguments(okType));

    public EnumValue MakeIndexError(LapisType okType) =>
        new(Result, ErrVariantIndex, [OutOfBounds], IndexResultArguments(okType));

    /// <summary>Os argumentos genéricos de <c>Result&lt;T, IndexError&gt;</c> (spec §21).</summary>
    public ImmutableArray<GenericArgument> IndexResultArguments(LapisType okType) =>
        GenericArgument.OfTypes([okType, IndexErrorType]);

    /// <summary><c>Result&lt;Str, ContextError&gt;</c> — o retorno de <c>contextGet</c>.</summary>
    public LapisType ContextResultType =>
        new NamedType(Result, GenericArgument.OfTypes([PrimitiveType.Str, ContextErrorType]));

    public EnumValue MakeContextOk(string value) =>
        new(Result, OkVariantIndex, [new StrValue(value)], ContextResultArguments);

    public EnumValue MakeContextMissing() =>
        new(Result, ErrVariantIndex, [Missing], ContextResultArguments);

    private ImmutableArray<GenericArgument> ContextResultArguments =>
        GenericArgument.OfTypes([PrimitiveType.Str, ContextErrorType]);

    /// <summary>
    /// O <c>TypeInfo</c> de uma definição já checada (plano 19 §19.4).
    ///
    /// Monta um <see cref="StructValue"/> comum a partir da
    /// <see cref="TypeDefinition"/>, do mesmo jeito que <see cref="MakeOk"/> monta
    /// um <c>Result</c> — nenhuma reflexão de C#, nenhum caminho especial no
    /// evaluator. Reflection é um valor da linguagem, e é isso que a torna
    /// imutável e dobrável pelo partial evaluator sem regra própria.
    ///
    /// Esta é a fonte <b>resolvida</b>: os nomes de tipo saem de tipos já
    /// checados. A fonte sintática, que a expansão de macros usa, vive no
    /// orquestrador — a diferença entre as duas é o §19.3, e é inevitável, porque
    /// durante a expansão o checker ainda não rodou.
    /// </summary>
    /// <param name="arguments">
    /// Os argumentos genéricos com que o tipo foi escrito, quando há.
    /// <c>reflect(Box)</c> descreve a <b>declaração</b> — o campo tem tipo
    /// <c>T</c>; <c>reflect(Box&lt;Int&gt;)</c> descreve a instância, e o campo tem
    /// tipo <c>Int</c>.
    /// </param>
    public StructValue MakeTypeInfo(TypeDefinition definition, ImmutableArray<GenericArgument> arguments = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bindings = Bind(definition, arguments);

        return MakeTypeInfo(
            definition.Name,
            definition.Kind,
            [.. definition.TypeParameters.Select(p => p.Name)],
            [.. definition.Fields.Select(f => (f.Name, Display(f.Type, bindings)))],
            [.. definition.Variants.Select(v =>
                (v.Name, (IReadOnlyList<string>)[.. v.Payload.Select(t => Display(t, bindings))]))]);
    }

    private static Dictionary<string, GenericArgument>? Bind(
        TypeDefinition definition,
        ImmutableArray<GenericArgument> arguments)
    {
        if (arguments.IsDefaultOrEmpty || definition.TypeParameters.Length != arguments.Length)
        {
            return null;
        }

        var bindings = new Dictionary<string, GenericArgument>(StringComparer.Ordinal);

        for (var i = 0; i < arguments.Length; i++)
        {
            bindings[definition.TypeParameters[i].Name] = arguments[i];
        }

        return bindings;
    }

    private static string Display(LapisType type, Dictionary<string, GenericArgument>? bindings) =>
        (bindings is null ? type : TypeSubstitution.Apply(type, bindings)).ToDisplayString();

    /// <summary>
    /// A mesma construção a partir de nomes já extraídos, para quem tem os
    /// metadados mas não uma <see cref="TypeDefinition"/> — a fonte sintática do
    /// compile time (§19.3).
    /// </summary>
    public StructValue MakeTypeInfo(
        string name,
        TypeDefinitionKind kind,
        IReadOnlyList<string> typeParameterNames,
        IReadOnlyList<(string Name, string TypeName)> fields,
        IReadOnlyList<(string Name, IReadOnlyList<string> PayloadTypeNames)> variants)
    {
        ArgumentNullException.ThrowIfNull(typeParameterNames);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(variants);

        var kindValue = new EnumValue(
            TypeKind,
            TypeKind.IndexOfVariant(kind == TypeDefinitionKind.Enum ? EnumVariant : StructVariant),
            [],
            []);

        return new StructValue(
            TypeInfo,
            [
                new StrValue(name),
                kindValue,
                Strings(typeParameterNames),
                new ArrayValue(
                    [.. fields.Select(f => (Value)new StructValue(
                        FieldInfo, [new StrValue(f.Name), new StrValue(f.TypeName)], []))],
                    new NamedType(FieldInfo, [])),
                new ArrayValue(
                    [.. variants.Select(v => (Value)new StructValue(
                        VariantInfo,
                        [
                            new StrValue(v.Name),
                            new IntValue(v.PayloadTypeNames.Count),
                            Strings(v.PayloadTypeNames),
                        ],
                        []))],
                    new NamedType(VariantInfo, [])),
            ],
            []);
    }

    private static ArrayValue Strings(IReadOnlyList<string> values) =>
        new([.. values.Select(v => (Value)new StrValue(v))], PrimitiveType.Str);

    private static TypeDefinition RequireStruct(ImmutableArray<PreludeBinding> bindings, string name)
    {
        var binding = bindings.FirstOrDefault(b => b.Name == name)
            ?? throw new InternalCompilerException($"o prelude não define '{name}'");

        return binding.Type is MetaType { Definition: { Kind: TypeDefinitionKind.Struct } definition }
            ? definition
            : throw new InternalCompilerException($"'{name}' do prelude não é um type");
    }

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

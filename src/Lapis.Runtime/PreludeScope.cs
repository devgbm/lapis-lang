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
    public const string OptionName = "Option";
    public const string ContextErrorName = "ContextError";
    public const string OkVariant = "Ok";
    public const string ErrVariant = "Err";
    public const string SomeVariant = "Some";
    public const string NoneVariant = "None";
    public const string MissingVariant = "Missing";

    public const string TypeInfoName = "TypeInfo";
    public const string FieldInfoName = "FieldInfo";
    public const string VariantInfoName = "VariantInfo";
    public const string TypeKindName = "TypeKind";
    public const string StructVariant = "Struct";
    public const string EnumVariant = "Enum";

    /// <param name="macros">
    /// As macros que o <c>prelude.ls</c> declara (plano 20). São dado como os
    /// bindings: quem as registra é o expander, e quem as lê do arquivo é o
    /// orquestrador.
    /// </param>
    public PreludeScope(
        ImmutableArray<PreludeBinding> bindings,
        ImmutableArray<Ast.Surface.MacroDeclaration> macros = default)
    {
        Bindings = bindings;
        Macros = macros.IsDefault ? [] : macros;
        Result = RequireEnum(bindings, ResultName, OkVariant, ErrVariant);
        Option = RequireEnum(bindings, OptionName, SomeVariant, NoneVariant);
        ContextError = RequireEnum(bindings, ContextErrorName, MissingVariant);
        TypeKind = RequireEnum(bindings, TypeKindName, StructVariant, EnumVariant);

        TypeInfo = RequireStruct(bindings, TypeInfoName);
        FieldInfo = RequireStruct(bindings, FieldInfoName);
        VariantInfo = RequireStruct(bindings, VariantInfoName);

        OkVariantIndex = Result.IndexOfVariant(OkVariant);
        ErrVariantIndex = Result.IndexOfVariant(ErrVariant);
        SomeVariantIndex = Option.IndexOfVariant(SomeVariant);
        NoneVariantIndex = Option.IndexOfVariant(NoneVariant);

        Missing = new EnumValue(ContextError, ContextError.IndexOfVariant(MissingVariant), [], []);
        ContextErrorType = new NamedType(ContextError, []);

        TypeInfoType = new NamedType(TypeInfo, []);
    }

    public ImmutableArray<PreludeBinding> Bindings { get; }

    /// <summary>
    /// As construções que a linguagem <b>não tem</b>, escritas na própria
    /// linguagem: <c>@unless</c> e <c>@while</c> (plano 20).
    ///
    /// Elas não têm tipo nem valor, e por isso não são <see cref="PreludeBinding"/>:
    /// uma macro não é first-class citizen (Q19). Ficam aqui porque o prelude é o
    /// lugar de tudo o que todo programa enxerga.
    /// </summary>
    public ImmutableArray<Ast.Surface.MacroDeclaration> Macros { get; }

    public TypeDefinition Result { get; }

    /// <summary>
    /// O retorno de uma indexação que pode falhar (Q31).
    ///
    /// Era <c>Result&lt;T, IndexError&gt;</c> até o M12. <c>IndexError.OutOfBounds</c>
    /// nunca carregou informação — um enum de uma variante cujo significado é
    /// "falhou" —, e <c>Result</c> existe para o erro que <b>diz</b> alguma coisa.
    /// </summary>
    public TypeDefinition Option { get; }

    public TypeDefinition ContextError { get; }

    public TypeDefinition TypeKind { get; }

    public TypeDefinition TypeInfo { get; }

    public TypeDefinition FieldInfo { get; }

    public TypeDefinition VariantInfo { get; }

    /// <summary>O tipo de <c>reflect(...)</c> (plano 19 §19.2).</summary>
    public LapisType TypeInfoType { get; }

    public int OkVariantIndex { get; }

    public int ErrVariantIndex { get; }

    public int SomeVariantIndex { get; }

    public int NoneVariantIndex { get; }

    /// <summary>O valor <c>ContextError.Missing</c>, único e imutável.</summary>
    public EnumValue Missing { get; }

    public LapisType ContextErrorType { get; }

    /// <summary><c>Option&lt;T&gt;</c> — o tipo de uma indexação que pode falhar.</summary>
    public LapisType OptionOf(LapisType element) =>
        new NamedType(Option, GenericArgument.OfTypes([element]));

    public EnumValue MakeSome(Value payload, LapisType element) =>
        new(Option, SomeVariantIndex, [payload], GenericArgument.OfTypes([element]));

    public EnumValue MakeNone(LapisType element) =>
        new(Option, NoneVariantIndex, [], GenericArgument.OfTypes([element]));

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
                new SpanValue(
                    [.. fields.Select(f => (Value)new StructValue(
                        FieldInfo, [new StrValue(f.Name), new StrValue(f.TypeName)], []))],
                    new NamedType(FieldInfo, [])),
                new SpanValue(
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

    private static SpanValue Strings(IReadOnlyList<string> values) =>
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

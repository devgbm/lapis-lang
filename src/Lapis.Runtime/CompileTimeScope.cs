using System.Collections.Immutable;
using Lapis.Ast.Types;

namespace Lapis.Runtime;

/// <summary>
/// O ambiente extra de <b>compile time</b> (plano 18 §18.1).
///
/// A decisão que organiza o milestone é "uma linguagem, um evaluator, dois
/// ambientes": <c>constraint</c> roda na própria LapisLang, avaliada pelo mesmo
/// evaluator do runtime. O que muda entre as fases é só o que está em escopo — e
/// é exatamente este tipo.
///
/// Passar uma instância ao checker e ao evaluator é o que liga as nativas de
/// contexto e libera <c>throw</c>; passar <c>null</c> é o runtime, onde nenhuma
/// das duas coisas existe.
/// </summary>
public sealed class CompileTimeScope
{
    public CompileTimeScope(CompileContext context, PreludeScope prelude)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(prelude);

        Context = context;
        Bindings = CompileTimeNatives.For(context, prelude);
    }

    public CompileContext Context { get; }

    /// <summary>As nativas de contexto, com nome, tipo e valor já resolvidos.</summary>
    public ImmutableArray<PreludeBinding> Bindings { get; }
}

/// <summary>
/// As nativas visíveis só durante a compilação (spec de macros §8.3).
///
/// Elas não estão em <see cref="Natives.All"/> de propósito: <c>Natives.All</c> é
/// o escopo raiz de <b>todo</b> programa, e uma nativa de contexto lá dentro
/// tornaria o estado de compilação alcançável em runtime — que é justamente a
/// separação que o plano 18 existe para manter.
///
/// Cada uma fecha sobre o <see cref="CompileContext"/> da compilação corrente, o
/// que faz duas compilações não se enxergarem sem nenhum cuidado adicional.
/// </summary>
public static class CompileTimeNatives
{
    public const string HasName = "contextHas";
    public const string GetName = "contextGet";
    public const string PutName = "contextPut";
    public const string KeysName = "contextKeys";

    /// <summary>Todos os nomes reservados pela fase de compilação.</summary>
    public static ImmutableArray<string> AllNames { get; } = [HasName, GetName, PutName, KeysName];

    public static ImmutableArray<PreludeBinding> For(CompileContext context, PreludeScope prelude)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(prelude);

        return
        [
            Native(
                HasName,
                FunctionType.Of([PrimitiveType.Str], PrimitiveType.Bool),
                (arguments, _) => BoolValue.Of(context.Has(Key(arguments[0])))),

            // Devolve `Result` porque chave ausente é falha **esperada**, e a spec
            // §30 manda falha esperada aparecer no tipo. Não é `throw`: quem
            // decide se a ausência é erro é a constraint, não a primitiva.
            Native(
                GetName,
                FunctionType.Of([PrimitiveType.Str], prelude.ContextResultType),
                (arguments, _) => context.Get(Key(arguments[0])) is { } value
                    ? prelude.MakeContextOk(value)
                    : prelude.MakeContextMissing()),

            Native(
                PutName,
                FunctionType.Of([PrimitiveType.Str, PrimitiveType.Str], PrimitiveType.Void),
                (arguments, _) =>
                {
                    context.Put(Key(arguments[0]), Key(arguments[1]));
                    return VoidValue.Instance;
                }),

            Native(
                KeysName,
                FunctionType.Of([PrimitiveType.Str], new ArrayType(PrimitiveType.Str)),
                (arguments, _) => new ArrayValue(
                    [.. context.Keys(Key(arguments[0])).Select(k => (Value)new StrValue(k))],
                    PrimitiveType.Str)),
        ];
    }

    private static PreludeBinding Native(
        string name,
        FunctionType signature,
        Func<ImmutableArray<Value>, RuntimeContext, Value> implementation) =>
        new(name, signature, new NativeFunctionValue(name, signature, implementation));

    private static string Key(Value value) => ((StrValue)value).Value;
}

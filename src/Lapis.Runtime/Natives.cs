using System.Collections.Immutable;
using Lapis.Ast.Types;

namespace Lapis.Runtime;

/// <summary>
/// Os únicos nomes que não dá para escrever em LapisLang (plano 09 §9.2).
///
/// Regra de admissão: um nome só pode ser nativo se for <b>impossível</b> defini-lo
/// no <c>prelude.ls</c>. Cada nativo novo precisa dessa justificativa escrita.
/// </summary>
public static class Natives
{
    /// <summary>
    /// <c>print: fn(Any) Void</c> — nativo porque tem efeito de I/O.
    ///
    /// Não é genérico: como argumentos genéricos passaram a ser sempre explícitos
    /// (Q7), uma assinatura <c>fn&lt;T&gt;(T) Void</c> obrigaria a escrever
    /// <c>print&lt;Int&gt;(x)</c> em todo programa — inclusive nos exemplos da spec.
    /// O parâmetro usa o tipo interno <c>Any</c>, que nenhuma sintaxe produz.
    /// </summary>
    public const string PrintName = "print";

    public static readonly FunctionType PrintSignature =
        FunctionType.Of([AnyType.Instance], PrimitiveType.Void);

    public static NativeFunctionValue Print { get; } = new(
        PrintName,
        PrintSignature,
        (arguments, context) =>
        {
            context.Output.Write(ValueFormatter.Format(arguments[0]));
            context.Output.Write("\n");
            return VoidValue.Instance;
        });

    /// <summary>Todos os nativos, na ordem em que entram no escopo raiz.</summary>
    public static ImmutableArray<NativeFunctionValue> All { get; } = [Print];
}

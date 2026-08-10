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
    /// <summary><c>print: fn&lt;T&gt;(value: T) Void</c> — nativo porque tem efeito de I/O.</summary>
    public const string PrintName = "print";

    private static readonly TypeParameterType PrintTypeParameter = new("T");

    public static readonly FunctionType PrintSignature =
        new([PrintTypeParameter], PrimitiveType.Void, [PrintTypeParameter]);

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

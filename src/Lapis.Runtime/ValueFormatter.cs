using System.Globalization;
using Lapis.Ast;
using Lapis.Diagnostics;

namespace Lapis.Runtime;

/// <summary>
/// Formatação de valores. As regras estão fixadas no plano 07 §7.4 porque os
/// golden files de conformidade dependem delas.
///
/// Sempre cultura-invariante: <c>3.14</c> nunca vira <c>3,14</c>.
/// </summary>
public static class ValueFormatter
{
    /// <summary>Como <c>print</c> mostra o valor. Strings saem sem aspas.</summary>
    public static string Format(Value value) => Format(value, quoteStrings: false);

    /// <summary>Para testes e mensagens de erro. Strings saem com aspas e escapes.</summary>
    public static string FormatDebug(Value value) => Format(value, quoteStrings: true);

    private static string Format(Value value, bool quoteStrings) => value switch
    {
        IntValue v => v.Value.ToString(CultureInfo.InvariantCulture),
        FloatValue v => new ConstFloat(v.Value).ToDisplayString(),
        BoolValue v => v.Value ? "true" : "false",
        StrValue v => quoteStrings ? new ConstStr(v.Value).ToDisplayString() : v.Value,
        VoidValue => "()",
        ArrayValue v => "[" + string.Join(", ", v.Elements.Select(e => Format(e, quoteStrings: true))) + "]",

        // Enums são impressos qualificados, exatamente como a linguagem exige que
        // sejam escritos (Q3): `Result.Ok(20)`, não `Ok(20)`.
        EnumValue v => FormatEnum(v),

        TypeValue v => $"<tipo {v.Definition.Name}>",
        VariantConstructorValue v => $"<{v.Definition.Name}.{v.Definition.Variants[v.VariantIndex].Name}>",
        ClosureValue v => $"<{v.Signature.ToDisplayString()}>",
        NativeFunctionValue v => $"<nativo {v.Name}>",
        _ => throw InternalCompilerException.Unreachable(value),
    };

    private static string FormatEnum(EnumValue value)
    {
        var qualified = $"{value.Definition.Name}.{value.Variant.Name}";

        return value.Payload.IsDefaultOrEmpty
            ? qualified
            : $"{qualified}({string.Join(", ", value.Payload.Select(p => Format(p, quoteStrings: true)))})";
    }
}

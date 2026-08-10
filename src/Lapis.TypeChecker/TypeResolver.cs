using System.Collections.Immutable;
using Lapis.Ast.Surface;
using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker;

/// <summary>
/// Resolve sintaxe de tipo (<see cref="TypeSyntax"/>) em tipo semântico
/// (<see cref="LapisType"/>).
///
/// Os primitivos não são palavras-chave (Apêndice A §A.6): são identificadores
/// resolvidos aqui.
/// </summary>
public sealed class TypeResolver(DiagnosticBag diagnostics)
{
    private static readonly Dictionary<string, LapisType> Primitives = new(StringComparer.Ordinal)
    {
        ["Int"] = PrimitiveType.Int,
        ["Float"] = PrimitiveType.Float,
        ["Bool"] = PrimitiveType.Bool,
        ["Str"] = PrimitiveType.Str,
        ["Void"] = PrimitiveType.Void,
    };

    public static bool IsPrimitiveName(string name) => Primitives.ContainsKey(name);

    public LapisType Resolve(TypeSyntax? syntax, SourceSpan fallbackSpan)
    {
        // Tipo de retorno ausente significa Void (spec §6).
        if (syntax is null)
        {
            return PrimitiveType.Void;
        }

        switch (syntax)
        {
            case NamedTypeSyntax named:
                if (Primitives.TryGetValue(named.Name, out var primitive))
                {
                    return primitive;
                }

                // "?" é o marcador que o parser deixa após um erro de sintaxe:
                // já houve diagnóstico, não reportar de novo.
                if (named.Name == "?")
                {
                    return ErrorType.Instance;
                }

                diagnostics.ReportError(
                    DiagnosticCodes.UnknownType,
                    named.Span,
                    $"tipo '{named.Name}' não existe");
                return ErrorType.Instance;

            case ArrayTypeSyntax array:
                return new ArrayType(Resolve(array.Element, fallbackSpan));

            case FunctionTypeSyntax function:
                var parameters = function.Parameters
                    .Select(p => Resolve(p, fallbackSpan))
                    .ToImmutableArray();

                return new FunctionType(parameters, Resolve(function.Return, fallbackSpan), []);

            default:
                throw InternalCompilerException.Unreachable(syntax, syntax.Span);
        }
    }
}

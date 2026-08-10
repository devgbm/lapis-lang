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
/// resolvidos aqui. Nomes de tipos definidos pelo usuário e parâmetros de tipo
/// vêm do escopo.
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

    /// <summary>Parâmetros de tipo em escopo, ao resolver o corpo de uma declaração genérica.</summary>
    public Dictionary<string, TypeParameterType> TypeParameters { get; } = new(StringComparer.Ordinal);

    public LapisType Resolve(TypeSyntax? syntax, Scope scope)
    {
        // Tipo de retorno ausente significa Void (spec §6).
        if (syntax is null)
        {
            return PrimitiveType.Void;
        }

        switch (syntax)
        {
            case NamedTypeSyntax named:
                return ResolveNamed(named, scope);

            case ArrayTypeSyntax array:
                return new ArrayType(Resolve(array.Element, scope));

            case FunctionTypeSyntax function:
                var parameters = function.Parameters
                    .Select(p => Resolve(p, scope))
                    .ToImmutableArray();

                return new FunctionType(parameters, Resolve(function.Return, scope), []);

            default:
                throw InternalCompilerException.Unreachable(syntax, syntax.Span);
        }
    }

    private LapisType ResolveNamed(NamedTypeSyntax named, Scope scope)
    {
        if (Primitives.TryGetValue(named.Name, out var primitive))
        {
            return ReportUnexpectedArguments(named, primitive);
        }

        if (TypeParameters.TryGetValue(named.Name, out var parameter))
        {
            return ReportUnexpectedArguments(named, parameter);
        }

        // "?" é o marcador que o parser deixa após um erro de sintaxe: já houve
        // diagnóstico, não reportar de novo.
        if (named.Name == "?")
        {
            return ErrorType.Instance;
        }

        if (!scope.TryLookup(named.Name, out var binding) || binding.Type is not MetaType meta)
        {
            diagnostics.ReportError(
                DiagnosticCodes.UnknownType, named.Span, $"tipo '{named.Name}' não existe");
            return ErrorType.Instance;
        }

        var definition = meta.Definition;
        var arguments = named.Arguments.Select(a => Resolve(a, scope)).ToImmutableArray();

        if (arguments.Length != definition.TypeParameters.Length)
        {
            var code = arguments.IsEmpty
                ? DiagnosticCodes.GenericTypeNeedsArguments
                : DiagnosticCodes.GenericArityMismatch;

            diagnostics.ReportError(
                code,
                named.Span,
                $"'{definition.Name}' espera {definition.TypeParameters.Length} argumentos "
                + $"genéricos, fornecidos {arguments.Length}");

            return ErrorType.Instance;
        }

        return new NamedType(definition, arguments);
    }

    private LapisType ReportUnexpectedArguments(NamedTypeSyntax named, LapisType resolved)
    {
        if (!named.Arguments.IsDefaultOrEmpty)
        {
            diagnostics.ReportError(
                DiagnosticCodes.GenericArityMismatch,
                named.Span,
                $"'{named.Name}' não é genérico e não aceita argumentos de tipo");
            return ErrorType.Instance;
        }

        return resolved;
    }
}

using System.Collections.Immutable;
using Lapis.Ast;
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
    private Dictionary<string, TypeParameterType> TypeParameters { get; } = new(StringComparer.Ordinal);

    public bool IsTypeParameter(string name) => TypeParameters.ContainsKey(name);

    /// <summary>
    /// Traz nomes de parâmetros de tipo para escopo e devolve o que eles
    /// sombrearam, para <see cref="ExitTypeParameters"/> restaurar.
    ///
    /// O par existe por causa do aninhamento: um <c>fn&lt;T&gt;</c> dentro de
    /// outro <c>fn&lt;T&gt;</c> liga o seu próprio <c>T</c>, e ao sair o de fora
    /// tem de voltar — remover às cegas apagaria os dois.
    /// </summary>
    public Dictionary<string, TypeParameterType?> EnterTypeParameters(IEnumerable<string> names)
    {
        var shadowed = new Dictionary<string, TypeParameterType?>(StringComparer.Ordinal);

        foreach (var name in names)
        {
            shadowed[name] = TypeParameters.GetValueOrDefault(name);
            TypeParameters[name] = new TypeParameterType(name);
        }

        return shadowed;
    }

    public void ExitTypeParameters(Dictionary<string, TypeParameterType?> shadowed)
    {
        ArgumentNullException.ThrowIfNull(shadowed);

        foreach (var (name, previous) in shadowed)
        {
            if (previous is null)
            {
                TypeParameters.Remove(name);
            }
            else
            {
                TypeParameters[name] = previous;
            }
        }
    }

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

        var raw = named.Arguments.Select(a => Read(a, scope)).ToList();
        var arguments = GenericArguments.Resolve(
            diagnostics, definition.Name, definition.TypeParameters, raw, named.Span);

        return arguments is null ? ErrorType.Instance : new NamedType(definition, arguments.Value);
    }

    /// <summary>
    /// Lê um argumento genérico em <b>posição de tipo</b>. Aqui um valor const só
    /// pode ser um literal: uma função literal como argumento exige posição de
    /// expressão (Q17), e <c>fn(Int) Int</c> escrito num tipo é um tipo de função.
    /// </summary>
    private RawGenericArgument Read(GenericArgumentSyntax argument, Scope scope)
    {
        switch (argument)
        {
            case TypeArgumentSyntax a:
                return RawGenericArgument.OfType(Resolve(a.Type, scope), a.Span);

            case ValueArgumentSyntax { Value: var value }:
                return ReadLiteral(value) is { } constant
                    ? RawGenericArgument.OfConstant(new ConstArgument(constant), argument.Span)
                    : RawGenericArgument.RuntimeValue(argument.Span);

            case NameArgumentSyntax a:
                return ReadName(a, scope);

            default:
                throw InternalCompilerException.Unreachable(argument, argument.Span);
        }
    }

    private RawGenericArgument ReadName(NameArgumentSyntax argument, Scope scope)
    {
        if (Primitives.TryGetValue(argument.Name, out var primitive))
        {
            return RawGenericArgument.OfType(primitive, argument.Span);
        }

        if (TypeParameters.TryGetValue(argument.Name, out var parameter))
        {
            return RawGenericArgument.OfType(parameter, argument.Span);
        }

        if (!scope.TryLookup(argument.Name, out var binding))
        {
            diagnostics.ReportError(
                DiagnosticCodes.UnknownType, argument.Span, $"'{argument.Name}' não existe");

            return RawGenericArgument.Error(argument.Span);
        }

        if (binding.Type is MetaType meta)
        {
            return RawGenericArgument.OfType(new NamedType(meta.Definition, meta.Arguments), argument.Span);
        }

        // Um `def` ligado a um literal é constante e serve de argumento (Q18);
        // qualquer outro nome designa um valor que só existe em execução.
        return binding.Constant is { } constant
            ? RawGenericArgument.OfConstant(constant, argument.Span)
            : RawGenericArgument.RuntimeValue(argument.Span);
    }

    private static ConstantValue? ReadLiteral(Expression value) => value switch
    {
        IntLiteral v => new ConstInt(v.Value),
        FloatLiteral v => new ConstFloat(v.Value),
        BoolLiteral v => v.Value ? ConstBool.True : ConstBool.False,
        StrLiteral v => new ConstStr(v.Value),
        _ => null,
    };

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

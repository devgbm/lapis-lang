using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Types;
using Lapis.Diagnostics;

namespace Lapis.TypeChecker;

/// <summary>
/// Um argumento genérico já lido do código, mas ainda não confrontado com o
/// parâmetro correspondente.
///
/// A leitura precisa vir antes da decisão porque um identificador nu é ambíguo:
/// em <c>Foo&lt;N&gt;</c>, só o parâmetro de <c>Foo</c> diz se <c>N</c> devia ser
/// um tipo ou uma constante (Apêndice A §A.7). Por isso ambas as leituras viajam
/// juntas, e o que não se aplica vem <c>null</c>.
/// </summary>
internal sealed record RawGenericArgument(
    SourceSpan Span,
    LapisType? Type,
    GenericArgument? Constant,
    bool IsRuntimeValue = false,
    bool IsError = false)
{
    public static RawGenericArgument OfType(LapisType type, SourceSpan span) => new(span, type, null);

    public static RawGenericArgument OfConstant(GenericArgument constant, SourceSpan span) =>
        new(span, null, constant);

    /// <summary>Uma expressão válida, mas cujo valor só existe em tempo de execução.</summary>
    public static RawGenericArgument RuntimeValue(SourceSpan span) => new(span, null, null, IsRuntimeValue: true);

    /// <summary>Já houve diagnóstico ao ler este argumento; não reportar de novo.</summary>
    public static RawGenericArgument Error(SourceSpan span) => new(span, null, null, IsError: true);
}

/// <summary>
/// Confronta os argumentos genéricos escritos com os parâmetros declarados
/// (spec §26, último item). É o ponto único onde <c>LAP0290</c>–<c>LAP0294</c>
/// são decididos, compartilhado por posição de tipo e posição de expressão.
/// </summary>
internal static class GenericArguments
{
    /// <summary>
    /// Devolve os argumentos resolvidos, ou <c>null</c> quando a aridade não bate —
    /// caso em que o diagnóstico já foi reportado e não há o que instanciar.
    /// </summary>
    public static ImmutableArray<GenericArgument>? Resolve(
        DiagnosticBag diagnostics,
        string owner,
        ImmutableArray<GenericParameter> parameters,
        IReadOnlyList<RawGenericArgument> arguments,
        SourceSpan span)
    {
        if (parameters.Length != arguments.Count)
        {
            var code = arguments.Count == 0
                ? DiagnosticCodes.GenericTypeNeedsArguments
                : DiagnosticCodes.GenericArityMismatch;

            diagnostics.ReportError(
                code,
                span,
                $"'{owner}' espera {parameters.Length} argumentos genéricos, fornecidos {arguments.Count}");

            return null;
        }

        var resolved = ImmutableArray.CreateBuilder<GenericArgument>(parameters.Length);

        for (var i = 0; i < parameters.Length; i++)
        {
            resolved.Add(Resolve(diagnostics, parameters[i], arguments[i]));
        }

        return resolved.ToImmutable();
    }

    private static GenericArgument Resolve(
        DiagnosticBag diagnostics,
        GenericParameter parameter,
        RawGenericArgument argument)
    {
        if (argument.IsError)
        {
            return new TypeArgument(ErrorType.Instance);
        }

        return parameter.ConstType is null
            ? ResolveType(diagnostics, parameter, argument)
            : ResolveConstant(diagnostics, parameter, argument, parameter.ConstType);
    }

    private static GenericArgument ResolveType(
        DiagnosticBag diagnostics,
        GenericParameter parameter,
        RawGenericArgument argument)
    {
        if (argument.Type is not null)
        {
            return new TypeArgument(argument.Type);
        }

        diagnostics.ReportError(
            DiagnosticCodes.ExpectedTypeArgument,
            argument.Span,
            $"o parâmetro '{parameter.Name}' é de tipo; um valor não serve como argumento");

        return new TypeArgument(ErrorType.Instance);
    }

    private static GenericArgument ResolveConstant(
        DiagnosticBag diagnostics,
        GenericParameter parameter,
        RawGenericArgument argument,
        LapisType expected)
    {
        if (argument.Constant is null)
        {
            // Um nome que existe mas designa um valor de execução não é constante:
            // nem o checker nem o partial evaluator têm de onde tirar seu valor.
            var (code, message) = argument.IsRuntimeValue
                ? (DiagnosticCodes.GenericArgumentNotConstant,
                    $"o argumento de '{parameter.Name}' não é constante em tempo de compilação")
                : (DiagnosticCodes.ExpectedConstArgument,
                    $"o parâmetro '{parameter.Name}' é constante; um tipo não serve como argumento");

            diagnostics.ReportError(code, argument.Span, message);

            return new ConstArgument(ConstUnit.Instance);
        }

        var actual = argument.Constant switch
        {
            ConstArgument c => c.Value.Type,
            ConstFunctionArgument f => f.Signature,
            _ => ErrorType.Instance,
        };

        if (!TypeRelations.IsAssignableTo(actual, expected))
        {
            diagnostics.ReportError(
                DiagnosticCodes.ConstArgumentTypeMismatch,
                argument.Span,
                $"'{parameter.Name}' espera {expected.ToDisplayString()}, "
                + $"encontrado {actual.ToDisplayString()}");
        }

        return argument.Constant;
    }
}

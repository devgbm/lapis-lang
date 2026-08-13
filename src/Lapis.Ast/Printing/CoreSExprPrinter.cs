using System.Collections.Immutable;
using System.Text;
using Lapis.Ast.Core;
using Lapis.Ast.Printing;
using Lapis.Diagnostics;

namespace Lapis.Ast.Printing;

/// <summary>
/// Imprime a Core AST como S-expression. Saída de <c>lapis desugar</c> e formato
/// dos snapshots de desugar.
///
/// Cadeias de <see cref="CoreLet"/> são re-achatadas em blocos: como a Core não
/// tem nó <c>Block</c>, imprimir literalmente produziria uma escada ilegível
/// (plano 02 §2.3).
/// </summary>
public static class CoreSExprPrinter
{
    public static string Print(CoreProgram program, bool includeNodeIds = false) =>
        Print(program.Body, includeNodeIds);

    public static string Print(CoreExpr expression, bool includeNodeIds = false)
    {
        var builder = new StringBuilder();
        PrintExpression(builder, expression, 0, includeNodeIds);
        return builder.ToString().TrimEnd();
    }

    private static void PrintExpression(StringBuilder builder, CoreExpr node, int indent, bool ids)
    {
        var tag = ids ? $"#{node.NodeId} " : string.Empty;

        switch (node)
        {
            case CoreLiteral n:
                Line(builder, indent, $"({tag}lit {n.Value.ToDisplayString()})");
                break;

            case CoreVariable n:
                Line(builder, indent, $"({tag}var {n.Name})");
                break;

            case CoreLet:
                PrintLetChain(builder, node, indent, ids);
                break;

            case CoreLambda n:
                var parameters = string.Join(
                    " ",
                    n.Parameters.Select(p => p.Type is null
                        ? $"({p.Name})"
                        : $"({p.Name} {SurfaceSExprPrinter.PrintType(p.Type)})"));
                var returnType = n.ReturnType is null ? "Void" : SurfaceSExprPrinter.PrintType(n.ReturnType);
                Open(builder, indent, $"{tag}lambda{PrintTypeParameters(n.TypeParameters)} ({parameters}) {returnType}");
                PrintExpression(builder, n.Body, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreCall n:
                Open(builder, indent, $"{tag}call");
                PrintExpression(builder, n.Callee, indent + 1, ids);

                foreach (var argument in n.Arguments)
                {
                    PrintExpression(builder, argument, indent + 1, ids);
                }

                Close(builder, indent);
                break;

            case CoreInstantiate n:
                Open(builder, indent, $"{tag}instantiate {PrintGenericArguments(n.Arguments)}");
                PrintExpression(builder, n.Target, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreThrow n:
                Open(builder, indent, $"{tag}throw");
                PrintExpression(builder, n.Value, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreReturn n:
                if (n.Value is null)
                {
                    Line(builder, indent, $"({tag}return)");
                }
                else
                {
                    Open(builder, indent, $"{tag}return");
                    PrintExpression(builder, n.Value, indent + 1, ids);
                    Close(builder, indent);
                }

                break;

            case CoreIf n:
                Open(builder, indent, $"{tag}if");
                PrintExpression(builder, n.Condition, indent + 1, ids);
                PrintExpression(builder, n.Then, indent + 1, ids);
                PrintExpression(builder, n.Else, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreBinary n:
                Open(builder, indent, $"{tag}{n.Operator.Symbol()}");
                PrintExpression(builder, n.Left, indent + 1, ids);
                PrintExpression(builder, n.Right, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreUnary n:
                Open(builder, indent, $"{tag}{n.Operator.Symbol()}u");
                PrintExpression(builder, n.Operand, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreSpan n:
                Open(builder, indent, $"{tag}span");

                foreach (var element in n.Elements)
                {
                    PrintExpression(builder, element, indent + 1, ids);
                }

                Close(builder, indent);
                break;

            case CoreSpanRepeat n:
                Open(builder, indent, $"{tag}span-repeat {SurfaceSExprPrinter.PrintType(n.Element)}");
                PrintExpression(builder, n.Initializer, indent + 1, ids);
                PrintExpression(builder, n.Size, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreIndex n:
                Open(builder, indent, $"{tag}index");
                PrintExpression(builder, n.Target, indent + 1, ids);
                PrintExpression(builder, n.Index, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreField n:
                Open(builder, indent, $"{tag}field {n.Name}");
                PrintExpression(builder, n.Target, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreEnumDef n:
                Open(builder, indent, $"{tag}enum{PrintTypeParameters(n.TypeParameters)}");

                foreach (var variant in n.Variants)
                {
                    var payload = variant.Payload.IsDefaultOrEmpty
                        ? string.Empty
                        : " " + string.Join(" ", variant.Payload.Select(SurfaceSExprPrinter.PrintType));
                    Line(builder, indent + 1, $"(variant {variant.Name}{payload})");
                }

                Close(builder, indent);
                break;

            case CoreMatch n:
                Open(builder, indent, $"{tag}match");
                PrintExpression(builder, n.Scrutinee, indent + 1, ids);

                foreach (var arm in n.Arms)
                {
                    Open(builder, indent + 1, $"arm {PrintPattern(arm.Pattern)}");
                    PrintExpression(builder, arm.Body, indent + 2, ids);
                    Close(builder, indent + 1);
                }

                Close(builder, indent);
                break;

            case CoreIs n:
                var owner = n.OwnerName is null ? string.Empty : n.OwnerName + ".";
                var binding = n.BindingName is null ? string.Empty : $" ({n.BindingName})";
                Open(builder, indent, $"{tag}is {owner}{n.VariantName}{binding}");
                PrintExpression(builder, n.Scrutinee, indent + 1, ids);
                PrintExpression(builder, n.Then, indent + 1, ids);
                PrintExpression(builder, n.Else, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreTypeDef n:
                Open(builder, indent, $"{tag}type{PrintTypeParameters(n.TypeParameters)}");

                foreach (var field in n.Fields)
                {
                    Line(builder, indent + 1, $"(field {field.Name} {SurfaceSExprPrinter.PrintType(field.Type)})");
                }

                Close(builder, indent);
                break;

            case CoreConstruct n:
                Open(builder, indent, $"{tag}construct {n.TypeName}{PrintGenericArguments(n.TypeArguments)}");

                foreach (var field in n.Fields)
                {
                    Open(builder, indent + 1, $"init {field.Name}");
                    PrintExpression(builder, field.Value, indent + 2, ids);
                    Close(builder, indent + 1);
                }

                Close(builder, indent);
                break;

            case CoreAssign n:
                Open(builder, indent, $"{tag}assign {n.Name}{PathOf(n)}");

                // O índice sai como filho, e não embutido no cabeçalho: ele é
                // expressão, e uma S-expression que o escondesse mentiria sobre
                // a forma da árvore.
                foreach (var segment in n.Path)
                {
                    if (segment is CoreIndexSegment index)
                    {
                        PrintExpression(builder, index.Index, indent + 1, ids);
                    }
                }

                PrintExpression(builder, n.Value, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreLoop n:
                Open(builder, indent, n.Label is null ? $"{tag}loop" : $"{tag}loop :{n.Label}");
                PrintExpression(builder, n.Body, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreBreak { Value: null } n:
                Line(builder, indent, n.Label is null ? $"({tag}break)" : $"({tag}break :{n.Label})");
                break;

            case CoreBreak n:
                Open(builder, indent, n.Label is null ? $"{tag}break" : $"{tag}break :{n.Label}");
                PrintExpression(builder, n.Value!, indent + 1, ids);
                Close(builder, indent);
                break;

            case CoreContinue n:
                Line(builder, indent, n.Label is null ? $"({tag}continue)" : $"({tag}continue :{n.Label})");
                break;

            default:
                throw InternalCompilerException.Unreachable(node, node.Span);
        }
    }

    public static string PrintTypeParameters(ImmutableArray<CoreTypeParameter> parameters) =>
        parameters.IsDefaultOrEmpty
            ? string.Empty
            : "<" + string.Join(", ", parameters.Select(PrintTypeParameter)) + ">";

    public static string PrintTypeParameter(CoreTypeParameter parameter) =>
        parameter.ConstType is null
            ? parameter.Name
            : $"{parameter.Name}: {SurfaceSExprPrinter.PrintType(parameter.ConstType)}";

    public static string PrintGenericArguments(ImmutableArray<CoreGenericArgument> arguments) =>
        arguments.IsDefaultOrEmpty
            ? string.Empty
            : "<" + string.Join(", ", arguments.Select(PrintGenericArgument)) + ">";

    public static string PrintGenericArgument(CoreGenericArgument argument) => argument switch
    {
        CoreTypeArgument a => SurfaceSExprPrinter.PrintType(a.Type),
        CoreNameArgument a => a.Name,
        CoreValueArgument a => CoreSourcePrinter.PrintExpressionCompact(a.Value),
        _ => throw new InternalCompilerException($"argumento genérico inesperado: {argument.GetType().Name}"),
    };

    public static string PrintPattern(CorePattern pattern) => pattern switch
    {
        CoreWildcardPattern => "_",
        CoreBindingPattern p => p.Name,
        CoreLiteralPattern p => p.Value.ToDisplayString(),
        CoreVariantPattern { Arguments.IsDefaultOrEmpty: true } p => $"{p.EnumName}.{p.VariantName}",
        CoreVariantPattern p =>
            $"{p.EnumName}.{p.VariantName}({string.Join(", ", p.Arguments.Select(PrintPattern))})",
        _ => throw new InternalCompilerException($"padrão inesperado: {pattern.GetType().Name}"),
    };

    private static void PrintLetChain(StringBuilder builder, CoreExpr node, int indent, bool ids)
    {
        Open(builder, indent, "block");

        var current = node;

        while (current is CoreLet let)
        {
            if (let.IsSynthetic)
            {
                Open(builder, indent + 1, "stmt");
                PrintExpression(builder, let.Value, indent + 2, ids);
                Close(builder, indent + 1);
            }
            else
            {
                var annotation = let.Annotation is null
                    ? string.Empty
                    : $" : {SurfaceSExprPrinter.PrintType(let.Annotation)}";
                var keyword = let.IsMutable ? "var" : "let";
                Open(
                    builder,
                    indent + 1,
                    $"{(ids ? $"#{let.NodeId} " : string.Empty)}{keyword} {let.Name}{annotation}");
                PrintExpression(builder, let.Value, indent + 2, ids);
                Close(builder, indent + 1);
            }

            current = let.Body;
        }

        Open(builder, indent + 1, "tail");
        PrintExpression(builder, current, indent + 2, ids);
        Close(builder, indent + 1);

        Close(builder, indent);
    }

    private static void Open(StringBuilder builder, int indent, string head) =>
        builder.Append(' ', indent * 2).Append('(').AppendLine(head);

    /// <summary>
    /// O caminho no cabeçalho do <c>assign</c>. Um índice vira <c>[]</c> vazio:
    /// a expressão que está dentro dele é impressa como filho, e repeti-la aqui
    /// duplicaria a árvore em vez de descrevê-la.
    /// </summary>
    private static string PathOf(CoreAssign node) => string.Concat(node.Path.Select(segment => segment switch
    {
        CoreFieldSegment field => "." + field.Name,
        CoreIndexSegment => "[]",
        _ => throw InternalCompilerException.Unreachable(segment),
    }));

    private static void Close(StringBuilder builder, int indent) =>
        builder.Append(' ', indent * 2).AppendLine(")");

    private static void Line(StringBuilder builder, int indent, string text) =>
        builder.Append(' ', indent * 2).AppendLine(text);
}

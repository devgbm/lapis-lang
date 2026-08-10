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
                    n.Parameters.Select(p => $"({p.Name} {SurfaceSExprPrinter.PrintType(p.Type)})"));
                var returnType = n.ReturnType is null ? "Void" : SurfaceSExprPrinter.PrintType(n.ReturnType);
                Open(builder, indent, $"{tag}lambda ({parameters}) {returnType}");
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

            default:
                throw InternalCompilerException.Unreachable(node, node.Span);
        }
    }

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
                Open(builder, indent + 1, $"{(ids ? $"#{let.NodeId} " : string.Empty)}let {let.Name}{annotation}");
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

    private static void Close(StringBuilder builder, int indent) =>
        builder.Append(' ', indent * 2).AppendLine(")");

    private static void Line(StringBuilder builder, int indent, string text) =>
        builder.Append(' ', indent * 2).AppendLine(text);
}

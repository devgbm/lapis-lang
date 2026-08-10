using System.Text;
using Lapis.Ast.Surface;

namespace Lapis.Ast.Printing;

/// <summary>
/// Imprime a Surface AST como S-expression indentada. É a saída de
/// <c>lapis ast</c> e o formato dos snapshots de parser (plano 04).
/// </summary>
public static class SurfaceSExprPrinter
{
    public static string Print(SourceFile file)
    {
        var builder = new StringBuilder();
        builder.AppendLine("(source-file");

        foreach (var statement in file.Statements)
        {
            PrintStatement(builder, statement, 1);
        }

        builder.Append(')');
        return builder.ToString();
    }

    public static string Print(Expression expression)
    {
        var builder = new StringBuilder();
        PrintExpression(builder, expression, 0);
        return builder.ToString().TrimEnd();
    }

    private static void PrintStatement(StringBuilder builder, Statement statement, int indent)
    {
        switch (statement)
        {
            case DefStatement def:
                Open(builder, indent, $"def {def.Name}");

                if (def.Annotation is not null)
                {
                    Line(builder, indent + 1, $"(type {PrintType(def.Annotation)})");
                }

                PrintExpression(builder, def.Value, indent + 1);
                Close(builder, indent);
                break;

            case ExpressionStatement expression:
                Open(builder, indent, "stmt");
                PrintExpression(builder, expression.Expression, indent + 1);
                Close(builder, indent);
                break;

            default:
                Line(builder, indent, $"(<desconhecido {statement.GetType().Name}>)");
                break;
        }
    }

    private static void PrintExpression(StringBuilder builder, Expression expression, int indent)
    {
        switch (expression)
        {
            case IntLiteral n:
                Line(builder, indent, $"(int {n.Value})");
                break;

            case FloatLiteral n:
                Line(builder, indent, $"(float {new ConstFloat(n.Value).ToDisplayString()})");
                break;

            case BoolLiteral n:
                Line(builder, indent, $"(bool {(n.Value ? "true" : "false")})");
                break;

            case StrLiteral n:
                Line(builder, indent, $"(str {new ConstStr(n.Value).ToDisplayString()})");
                break;

            case UnitLiteral:
                Line(builder, indent, "(unit)");
                break;

            case IdentifierExpression n:
                Line(builder, indent, $"(name {n.Name})");
                break;

            case ErrorExpression:
                Line(builder, indent, "(error)");
                break;

            case UnaryExpression n:
                Open(builder, indent, $"unary {n.Operator.Symbol()}");
                PrintExpression(builder, n.Operand, indent + 1);
                Close(builder, indent);
                break;

            case BinaryExpression n:
                Open(builder, indent, $"binary {n.Operator.Symbol()}");
                PrintExpression(builder, n.Left, indent + 1);
                PrintExpression(builder, n.Right, indent + 1);
                Close(builder, indent);
                break;

            case BlockExpression n:
                Open(builder, indent, "block");

                foreach (var statement in n.Statements)
                {
                    PrintStatement(builder, statement, indent + 1);
                }

                if (n.Tail is not null)
                {
                    Open(builder, indent + 1, "tail");
                    PrintExpression(builder, n.Tail, indent + 2);
                    Close(builder, indent + 1);
                }

                Close(builder, indent);
                break;

            case IfExpression n:
                Open(builder, indent, "if");
                PrintExpression(builder, n.Condition, indent + 1);
                PrintExpression(builder, n.Then, indent + 1);

                if (n.Else is not null)
                {
                    PrintExpression(builder, n.Else, indent + 1);
                }

                Close(builder, indent);
                break;

            case ReturnExpression n:
                if (n.Value is null)
                {
                    Line(builder, indent, "(return)");
                }
                else
                {
                    Open(builder, indent, "return");
                    PrintExpression(builder, n.Value, indent + 1);
                    Close(builder, indent);
                }

                break;

            case FunctionExpression n:
                var parameters = string.Join(" ", n.Parameters.Select(p => $"({p.Name} {PrintType(p.Type)})"));
                var returnType = n.ReturnType is null ? "Void" : PrintType(n.ReturnType);
                Open(builder, indent, $"fn ({parameters}) {returnType}");
                PrintExpression(builder, n.Body, indent + 1);
                Close(builder, indent);
                break;

            case CallExpression n:
                Open(builder, indent, "call");
                PrintExpression(builder, n.Callee, indent + 1);

                foreach (var argument in n.Arguments)
                {
                    PrintExpression(builder, argument, indent + 1);
                }

                Close(builder, indent);
                break;

            case ArrayExpression n:
                Open(builder, indent, "array");

                foreach (var element in n.Elements)
                {
                    PrintExpression(builder, element, indent + 1);
                }

                Close(builder, indent);
                break;

            case IndexExpression n:
                Open(builder, indent, "index");
                PrintExpression(builder, n.Target, indent + 1);
                PrintExpression(builder, n.Index, indent + 1);
                Close(builder, indent);
                break;

            case MemberExpression n:
                Open(builder, indent, $"member {n.Name}");
                PrintExpression(builder, n.Target, indent + 1);
                Close(builder, indent);
                break;

            case EnumExpression n:
                var typeParameters = n.TypeParameters.IsDefaultOrEmpty
                    ? string.Empty
                    : "<" + string.Join(" ", n.TypeParameters.Select(p => p.Name)) + ">";
                Open(builder, indent, $"enum{typeParameters}");

                foreach (var variant in n.Variants)
                {
                    var payload = variant.Payload.IsDefaultOrEmpty
                        ? string.Empty
                        : " " + string.Join(" ", variant.Payload.Select(PrintType));
                    Line(builder, indent + 1, $"(variant {variant.Name}{payload})");
                }

                Close(builder, indent);
                break;

            default:
                Line(builder, indent, $"(<desconhecido {expression.GetType().Name}>)");
                break;
        }
    }

    public static string PrintType(TypeSyntax type) => type switch
    {
        NamedTypeSyntax { Arguments.IsDefaultOrEmpty: true } n => n.Name,
        NamedTypeSyntax n => $"{n.Name}<{string.Join(", ", n.Arguments.Select(PrintType))}>",
        ArrayTypeSyntax n => PrintType(n.Element) + "[]",
        FunctionTypeSyntax n =>
            $"fn({string.Join(", ", n.Parameters.Select(PrintType))}) {PrintType(n.Return)}",
        _ => "<?>",
    };

    private static void Open(StringBuilder builder, int indent, string head) =>
        builder.Append(' ', indent * 2).Append('(').AppendLine(head);

    private static void Close(StringBuilder builder, int indent) =>
        builder.Append(' ', indent * 2).AppendLine(")");

    private static void Line(StringBuilder builder, int indent, string text) =>
        builder.Append(' ', indent * 2).AppendLine(text);
}

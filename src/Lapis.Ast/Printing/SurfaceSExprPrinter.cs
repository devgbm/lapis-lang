using System.Collections.Immutable;
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
                Open(builder, indent, $"{(def.IsMutable ? "var" : "def")} {def.Name}");

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

            case AssignStatement assign:
                Open(builder, indent, $"assign {assign.Name}");
                PrintExpression(builder, assign.Value, indent + 1);
                Close(builder, indent);
                break;

            case GotoStatement { Condition: null } jump:
                Line(builder, indent, $"(goto {jump.Label})");
                break;

            case GotoStatement jump:
                Open(builder, indent, $"goto-if {jump.Label}");
                PrintExpression(builder, jump.Condition!, indent + 1);
                Close(builder, indent);
                break;

            case LabelStatement label:
                Line(builder, indent, $"(label {label.Label})");
                break;

            case MacroDeclaration macro:
                Open(builder, indent, $"macro {macro.Name}");

                foreach (var rule in macro.Rules)
                {
                    Open(builder, indent + 1, "rule");
                    Line(builder, indent + 2, $"(match {PrintPattern(rule.Pattern)})");

                    if (rule.Constraint is not null)
                    {
                        Open(builder, indent + 2, "constraint");
                        PrintExpression(builder, rule.Constraint, indent + 3);
                        Close(builder, indent + 2);
                    }

                    Open(builder, indent + 2, "expand");
                    PrintExpression(builder, rule.Expansion, indent + 3);
                    Close(builder, indent + 2);
                    Close(builder, indent + 1);
                }

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

            case MacroInvocation n:
                Line(
                    builder,
                    indent,
                    $"(macro-call {n.Name} {string.Join(" ", n.Arguments.Select(t => t.Text))})".TrimEnd());
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

            case ThrowExpression n:
                Open(builder, indent, "throw");
                PrintExpression(builder, n.Value, indent + 1);
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
                Open(builder, indent, $"fn{PrintTypeParameters(n.TypeParameters)} ({parameters}) {returnType}");
                PrintExpression(builder, n.Body, indent + 1);
                Close(builder, indent);
                break;

            case InstantiateExpression n:
                Open(builder, indent, $"instantiate {PrintGenericArguments(n.Arguments)}");
                PrintExpression(builder, n.Target, indent + 1);
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

            case SpanExpression n:
                Open(builder, indent, "span");

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
                Open(builder, indent, $"enum{PrintTypeParameters(n.TypeParameters)}");

                foreach (var variant in n.Variants)
                {
                    var payload = variant.Payload.IsDefaultOrEmpty
                        ? string.Empty
                        : " " + string.Join(" ", variant.Payload.Select(PrintType));
                    Line(builder, indent + 1, $"(variant {variant.Name}{payload})");
                }

                Close(builder, indent);
                break;

            case MatchExpression n:
                Open(builder, indent, "match");
                PrintExpression(builder, n.Scrutinee, indent + 1);

                foreach (var arm in n.Arms)
                {
                    Open(builder, indent + 1, $"arm {PrintPattern(arm.Pattern)}");
                    PrintExpression(builder, arm.Body, indent + 2);
                    Close(builder, indent + 1);
                }

                Close(builder, indent);
                break;

            case TypeExpression n:
                Open(builder, indent, $"type{PrintTypeParameters(n.TypeParameters)}");

                foreach (var field in n.Fields)
                {
                    Line(builder, indent + 1, $"(field {field.Name} {PrintType(field.Type)})");
                }

                Close(builder, indent);
                break;

            case ConstructExpression n:
                Open(builder, indent, $"construct {n.TypeName}{PrintGenericArguments(n.TypeArguments)}");

                foreach (var field in n.Fields)
                {
                    Open(builder, indent + 1, $"init {field.Name}");
                    PrintExpression(builder, field.Value, indent + 2);
                    Close(builder, indent + 1);
                }

                Close(builder, indent);
                break;

            default:
                Line(builder, indent, $"(<desconhecido {expression.GetType().Name}>)");
                break;
        }
    }

    public static string PrintPattern(Pattern pattern) => pattern switch
    {
        WildcardPattern => "_",
        BindingPattern p => p.Name,
        LiteralPattern p => p.Value.ToDisplayString(),
        VariantPattern { Arguments.IsDefaultOrEmpty: true } p => $"{p.EnumName}.{p.VariantName}",
        VariantPattern p =>
            $"{p.EnumName}.{p.VariantName}({string.Join(", ", p.Arguments.Select(PrintPattern))})",
        _ => "<?>",
    };

    /// <summary>
    /// Parâmetros genéricos de uma declaração (Q1): <c>&lt;T, N: Int&gt;</c>. Vazio
    /// quando não há nenhum, para não poluir a saída do caso comum.
    /// </summary>
    public static string PrintTypeParameters(ImmutableArray<TypeParameterSyntax> parameters) =>
        parameters.IsDefaultOrEmpty
            ? string.Empty
            : "<" + string.Join(", ", parameters.Select(PrintTypeParameter)) + ">";

    public static string PrintTypeParameter(TypeParameterSyntax parameter) =>
        parameter.ConstType is null ? parameter.Name : $"{parameter.Name}: {PrintType(parameter.ConstType)}";

    public static string PrintGenericArguments(ImmutableArray<GenericArgumentSyntax> arguments) =>
        arguments.IsDefaultOrEmpty
            ? string.Empty
            : "<" + string.Join(", ", arguments.Select(PrintGenericArgument)) + ">";

    /// <summary>
    /// Um argumento genérico impresso como <b>código-fonte</b>, e não como
    /// S-expression: é assim que ele reaparece dentro de um tipo, e é o que mantém
    /// a saída do <c>CoreSourcePrinter</c> re-parseável (plano 02 §2.8).
    /// </summary>
    public static string PrintGenericArgument(GenericArgumentSyntax argument) => argument switch
    {
        TypeArgumentSyntax a => PrintType(a.Type),
        NameArgumentSyntax a => a.Name,
        ValueArgumentSyntax { Value: IntLiteral v } => new ConstInt(v.Value).ToDisplayString(),
        ValueArgumentSyntax { Value: FloatLiteral v } => new ConstFloat(v.Value).ToDisplayString(),
        ValueArgumentSyntax { Value: BoolLiteral v } => new ConstBool(v.Value).ToDisplayString(),
        ValueArgumentSyntax { Value: StrLiteral v } => new ConstStr(v.Value).ToDisplayString(),

        // Uma função literal como argumento só é escrevível em posição de
        // expressão (Q17), onde quem imprime é o CoreSourcePrinter, a partir da
        // Core. Aqui ela nunca chega.
        _ => "<?>",
    };

    /// <summary>O padrão de uma regra, na forma em que foi escrito.</summary>
    public static string PrintPattern(MacroPattern pattern) => pattern switch
    {
        PatternSequence p => string.Join(" ", p.Items.Select(PrintPattern)),
        PatternCapture p => $"{p.Category}:{p.Name}",
        PatternLiteral p => p.Text,
        PatternRepeat p => $"({PrintPattern(p.Item)})* separado por {p.Separator}",
        _ => "?",
    };

    public static string PrintType(TypeSyntax type) => type switch
    {
        NamedTypeSyntax { Arguments.IsDefaultOrEmpty: true } n => n.Name,
        NamedTypeSyntax n => $"{n.Name}{PrintGenericArguments(n.Arguments)}",
        SpanTypeSyntax n => $"[{PrintType(n.Element)};{PrintSize(n.Size)}]",
        FunctionTypeSyntax n =>
            $"fn({string.Join(", ", n.Parameters.Select(PrintType))}) {PrintType(n.Return)}",
        _ => "<?>",
    };

    private static string PrintSize(SpanSizeSyntax size) => size switch
    {
        FixedSizeSyntax n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        NamedSizeSyntax n => n.Name,
        _ => "?",
    };

    private static void Open(StringBuilder builder, int indent, string head) =>
        builder.Append(' ', indent * 2).Append('(').AppendLine(head);

    private static void Close(StringBuilder builder, int indent) =>
        builder.Append(' ', indent * 2).AppendLine(")");

    private static void Line(StringBuilder builder, int indent, string text) =>
        builder.Append(' ', indent * 2).AppendLine(text);
}

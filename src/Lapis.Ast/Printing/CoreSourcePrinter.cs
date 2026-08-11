using System.Text;
using Lapis.Ast.Core;
using Lapis.Diagnostics;

namespace Lapis.Ast.Printing;

/// <summary>
/// Imprime a Core AST como código <c>.ls</c> válido e re-parseável.
///
/// É requisito, não conveniência: habilita a propriedade de round-trip
/// <c>desugar(parse(print(core))) ≡ core</c> e é a saída de <c>lapis pe</c>
/// (plano 02 §2.8).
/// </summary>
public static class CoreSourcePrinter
{
    /// <summary>Imprime o programa inteiro como uma sequência de statements top-level.</summary>
    public static string Print(CoreProgram program)
    {
        var builder = new StringBuilder();
        PrintSequence(builder, program.Body, 0, topLevel: true);
        return builder.ToString().TrimEnd() + "\n";
    }

    public static string PrintExpression(CoreExpr expression)
    {
        var builder = new StringBuilder();
        Print(builder, expression, 0, Precedence.Lowest);
        return builder.ToString();
    }

    /// <summary>
    /// A mesma expressão numa linha só. Usada para argumentos genéricos, que vivem
    /// dentro de <c>&lt;...&gt;</c> e ficariam ilegíveis quebrados em várias linhas —
    /// e para a identidade estrutural de um argumento de função
    /// (<see cref="Types.ConstFunctionArgument"/>), que assim não depende da
    /// indentação do ponto onde a função foi escrita.
    /// </summary>
    public static string PrintExpressionCompact(CoreExpr expression)
    {
        var text = PrintExpression(expression);
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Imprime uma cadeia de <c>Let</c> como statements. No topo, sem chaves; em
    /// posição de expressão, o chamador já abriu o bloco.
    /// </summary>
    private static void PrintSequence(StringBuilder builder, CoreExpr node, int indent, bool topLevel)
    {
        var current = node;

        while (current is CoreLet let)
        {
            Indent(builder, indent);

            if (let.IsSynthetic)
            {
                Print(builder, let.Value, indent, Precedence.Lowest);
                builder.AppendLine(";");
            }
            else
            {
                builder.Append(let.IsMutable ? "var " : "def ").Append(let.Name);

                if (let.Annotation is not null)
                {
                    builder.Append(": ").Append(SurfaceSExprPrinter.PrintType(let.Annotation));
                }

                builder.Append(" = ");
                Print(builder, let.Value, indent, Precedence.Lowest);
                builder.AppendLine(";");
            }

            current = let.Body;
        }

        if (current is CoreLabeled labeled)
        {
            PrintLabeled(builder, labeled, indent, topLevel);
            return;
        }

        // Um salto nunca é cauda: `goto` é statement, e imprimi-lo sem `;` daria
        // texto que não reparseia.
        if (current is CoreGoto or CoreGotoIf)
        {
            // O salto implícito que fecha o segmento não é impresso: o `label`
            // logo abaixo é o que o recria no reparse. Imprimir daria um `goto`
            // a mais, e a Core deixaria de ser a mesma.
            if (current is CoreGoto { IsImplicit: true })
            {
                return;
            }

            Indent(builder, indent);
            Print(builder, current, indent, Precedence.Lowest);
            builder.AppendLine(";");
            return;
        }

        // Uma cauda `()` é sempre omitida: no topo ela é só o fim do arquivo, e
        // dentro de um bloco `{ s; }` e `{ s; () }` desugaram para a mesma Core —
        // omitir mantém o round-trip e deixa a saída bem mais legível.
        if (current is CoreLiteral { Value: ConstUnit })
        {
            return;
        }

        Indent(builder, indent);
        Print(builder, current, indent, Precedence.Lowest);

        if (topLevel)
        {
            builder.AppendLine(";");
        }
        else
        {
            builder.AppendLine();
        }
    }

    private static void Print(StringBuilder builder, CoreExpr node, int indent, Precedence context)
    {
        switch (node)
        {
            case CoreLiteral n:
                builder.Append(n.Value.ToDisplayString());
                break;

            case CoreVariable n:
                builder.Append(n.Name);
                break;

            case CoreLet or CoreLabeled:
                builder.AppendLine("{");
                PrintSequence(builder, node, indent + 1, topLevel: false);
                Indent(builder, indent);
                builder.Append('}');
                break;

            case CoreGoto n:
                builder.Append("goto ").Append(n.Label);
                break;

            case CoreAssign n:
                builder.Append(n.Name).Append(" = ");
                Print(builder, n.Value, indent, Precedence.Lowest);
                break;

            case CoreGotoIf n:
                builder.Append("goto ").Append(n.Label).Append(" if ");
                Print(builder, n.Condition, indent, Precedence.Lowest);
                break;

            case CoreLambda n:
                PrintLambda(builder, n, indent);
                break;

            case CoreCall n:
                Print(builder, n.Callee, indent, Precedence.Postfix);
                builder.Append('(');

                for (var i = 0; i < n.Arguments.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    Print(builder, n.Arguments[i], indent, Precedence.Lowest);
                }

                builder.Append(')');
                break;

            case CoreInstantiate n:
                Print(builder, n.Target, indent, Precedence.Postfix);
                builder.Append(CoreSExprPrinter.PrintGenericArguments(n.Arguments));
                break;

            case CoreReturn n:
                builder.Append("return");

                if (n.Value is not null)
                {
                    builder.Append(' ');
                    Print(builder, n.Value, indent, Precedence.Lowest);
                }

                break;

            case CoreIf n:
                builder.Append("if ");
                Print(builder, n.Condition, indent, Precedence.Lowest);
                builder.AppendLine(" {");
                PrintBlockBody(builder, n.Then, indent + 1);
                Indent(builder, indent);
                builder.AppendLine("} else {");
                PrintBlockBody(builder, n.Else, indent + 1);
                Indent(builder, indent);
                builder.Append('}');
                break;

            case CoreBinary n:
                PrintBinary(builder, n, indent, context);
                break;

            case CoreUnary n:
                var needsParens = context > Precedence.Unary;

                if (needsParens)
                {
                    builder.Append('(');
                }

                builder.Append(n.Operator.Symbol());
                Print(builder, n.Operand, indent, Precedence.Unary);

                if (needsParens)
                {
                    builder.Append(')');
                }

                break;

            case CoreArray n:
                builder.Append('[');

                for (var i = 0; i < n.Elements.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    Print(builder, n.Elements[i], indent, Precedence.Lowest);
                }

                builder.Append(']');
                break;

            case CoreIndex n:
                Print(builder, n.Target, indent, Precedence.Postfix);
                builder.Append('[');
                Print(builder, n.Index, indent, Precedence.Lowest);
                builder.Append(']');
                break;

            case CoreField n:
                Print(builder, n.Target, indent, Precedence.Postfix);
                builder.Append('.').Append(n.Name);
                break;

            case CoreEnumDef n:
                PrintEnumDef(builder, n, indent);
                break;

            case CoreMatch n:
                builder.Append("match ");
                Print(builder, n.Scrutinee, indent, Precedence.Lowest);
                builder.AppendLine(" {");

                for (var i = 0; i < n.Arms.Length; i++)
                {
                    Indent(builder, indent + 1);
                    builder.Append(CoreSExprPrinter.PrintPattern(n.Arms[i].Pattern)).Append(" => ");
                    Print(builder, n.Arms[i].Body, indent + 1, Precedence.Lowest);
                    builder.AppendLine(i < n.Arms.Length - 1 ? "," : string.Empty);
                }

                Indent(builder, indent);
                builder.Append('}');
                break;

            case CoreTypeDef n:
                PrintTypeDef(builder, n, indent);
                break;

            case CoreConstruct n:
                builder.Append('.').Append(n.TypeName)
                       .Append(CoreSExprPrinter.PrintGenericArguments(n.TypeArguments))
                       .Append(" { ");

                for (var i = 0; i < n.Fields.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(n.Fields[i].Name).Append(": ");
                    Print(builder, n.Fields[i].Value, indent, Precedence.Lowest);
                }

                builder.Append(" }");
                break;

            default:
                throw InternalCompilerException.Unreachable(node, node.Span);
        }
    }

    /// <summary>
    /// A operação inversa da decomposição em blocos básicos: a entrada, e depois
    /// <c>label L;</c> seguido do corpo de cada join.
    /// </summary>
    private static void PrintLabeled(StringBuilder builder, CoreLabeled node, int indent, bool topLevel)
    {
        // A entrada e todo join que não é o último são **seguidos** por um
        // `label L;`, então o que os fecha é statement, não cauda: precisa de `;`.
        // Só o último join carrega o valor do construto, e aí a regra volta a ser
        // a do chamador.
        PrintSequence(builder, node.Entry, indent, topLevel: true);

        for (var i = 0; i < node.Joins.Length; i++)
        {
            Indent(builder, indent);
            builder.Append("label ").Append(node.Joins[i].Name).AppendLine(";");

            PrintSequence(builder, node.Joins[i].Body, indent, i == node.Joins.Length - 1 && topLevel);
        }
    }

    private static void PrintTypeDef(StringBuilder builder, CoreTypeDef node, int indent)
    {
        builder.Append("type").Append(CoreSExprPrinter.PrintTypeParameters(node.TypeParameters)).AppendLine(" {");

        foreach (var field in node.Fields)
        {
            Indent(builder, indent + 1);
            builder.Append(field.Name).Append(": ")
                   .Append(SurfaceSExprPrinter.PrintType(field.Type)).AppendLine(";");
        }

        Indent(builder, indent);
        builder.Append('}');
    }

    private static void PrintEnumDef(StringBuilder builder, CoreEnumDef node, int indent)
    {
        builder.Append("enum").Append(CoreSExprPrinter.PrintTypeParameters(node.TypeParameters)).AppendLine(" {");

        for (var i = 0; i < node.Variants.Length; i++)
        {
            var variant = node.Variants[i];
            Indent(builder, indent + 1);
            builder.Append(variant.Name);

            if (!variant.Payload.IsDefaultOrEmpty)
            {
                builder.Append('(')
                       .Append(string.Join(", ", variant.Payload.Select(SurfaceSExprPrinter.PrintType)))
                       .Append(')');
            }

            builder.AppendLine(i < node.Variants.Length - 1 ? "," : string.Empty);
        }

        Indent(builder, indent);
        builder.Append('}');
    }

    private static void PrintLambda(StringBuilder builder, CoreLambda lambda, int indent)
    {
        builder.Append("fn").Append(CoreSExprPrinter.PrintTypeParameters(lambda.TypeParameters)).Append('(');

        for (var i = 0; i < lambda.Parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var parameter = lambda.Parameters[i];
            builder.Append(parameter.Name).Append(": ").Append(SurfaceSExprPrinter.PrintType(parameter.Type));
        }

        builder.Append(") ");

        if (lambda.ReturnType is not null)
        {
            builder.Append(SurfaceSExprPrinter.PrintType(lambda.ReturnType)).Append(' ');
        }

        builder.AppendLine("{");
        PrintBlockBody(builder, lambda.Body, indent + 1);
        Indent(builder, indent);
        builder.Append('}');
    }

    /// <summary>Corpo de um bloco: statements da cadeia de <c>Let</c> mais a cauda.</summary>
    private static void PrintBlockBody(StringBuilder builder, CoreExpr body, int indent)
    {
        // `{ }` vazio: um `()` sozinho seria redundante, mas mantê-lo garante o
        // round-trip quando o bloco é a cauda de outra expressão.
        PrintSequence(builder, body, indent, topLevel: false);
    }

    private static void PrintBinary(StringBuilder builder, CoreBinary node, int indent, Precedence context)
    {
        var precedence = (Precedence)node.Operator.Precedence();
        var needsParens = context > precedence;

        if (needsParens)
        {
            builder.Append('(');
        }

        Print(builder, node.Left, indent, precedence);
        builder.Append(' ').Append(node.Operator.Symbol()).Append(' ');

        // Operadores são associativos à esquerda: o filho direito de mesma
        // precedência precisa de parênteses para preservar a forma da árvore.
        Print(builder, node.Right, indent, precedence + 1);

        if (needsParens)
        {
            builder.Append(')');
        }
    }

    private static void Indent(StringBuilder builder, int indent) => builder.Append(' ', indent * 4);

    private enum Precedence
    {
        Lowest = 0,
        Unary = 7,
        Postfix = 8,
    }
}

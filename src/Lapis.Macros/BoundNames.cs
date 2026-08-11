using Lapis.Ast.Surface;

namespace Lapis.Macros;

/// <summary>
/// Nomes que um corpo de <c>expand</c> <b>liga</b>: os <c>def</c>, <c>var</c> e
/// <c>label</c> escritos dentro dele.
///
/// É a fronteira da higiene. Renomear o que a macro liga impede que ela capture
/// um nome do programa; <b>não</b> renomear o que ela apenas referencia é o que
/// deixa `print` continuar sendo `print`.
///
/// Parâmetros de função dentro do <c>expand</c> ficam de fora de propósito: eles
/// já são escopados pela própria função, e nada do programa entra ali.
/// </summary>
internal static class BoundNames
{
    public static HashSet<string> Of(BlockExpression block)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        Collect(block, names);
        return names;
    }

    private static void Collect(Expression expression, HashSet<string> names)
    {
        switch (expression)
        {
            case BlockExpression block:
                foreach (var statement in block.Statements)
                {
                    Collect(statement, names);
                }

                if (block.Tail is not null)
                {
                    Collect(block.Tail, names);
                }

                break;

            case IfExpression n:
                Collect(n.Condition, names);
                Collect(n.Then, names);

                if (n.Else is not null)
                {
                    Collect(n.Else, names);
                }

                break;

            case FunctionExpression n:
                Collect(n.Body, names);
                break;

            case MatchExpression n:
                foreach (var arm in n.Arms)
                {
                    Collect(arm.Body, names);
                }

                break;
        }
    }

    private static void Collect(Statement statement, HashSet<string> names)
    {
        switch (statement)
        {
            case DefStatement s:
                names.Add(s.Name);
                Collect(s.Value, names);
                break;

            case LabelStatement s:
                names.Add(s.Label);
                break;

            // Um `goto` não liga o rótulo, mas precisa acompanhar o `label` que o
            // liga — senão o salto ficaria apontando para o nome não renomeado.
            case GotoStatement s:
                names.Add(s.Label);
                break;

            case ExpressionStatement s:
                Collect(s.Expression, names);
                break;
        }
    }
}

namespace Lapis.Ast.Types;

/// <summary>
/// As duas únicas relações entre tipos da LapisLang 0.2: atribuibilidade
/// (governada por <c>Never &lt;: T</c>) e junção de ramos (plano 06 §6.6).
/// </summary>
public static class TypeRelations
{
    /// <summary>Um valor de <paramref name="source"/> pode ocupar uma posição de <paramref name="target"/>?</summary>
    public static bool IsAssignableTo(LapisType source, LapisType target)
    {
        // ErrorType absorve: já houve diagnóstico, não gerar cascata.
        if (source is ErrorType || target is ErrorType)
        {
            return true;
        }

        // `return e` tem tipo Never e cabe em qualquer posição.
        if (source is NeverType)
        {
            return true;
        }

        return source == target;
    }

    /// <summary>
    /// Tipo comum de dois ramos. Devolve <c>null</c> quando não existe — o
    /// chamador reporta o diagnóstico apropriado com os dois tipos.
    /// </summary>
    public static LapisType? Join(LapisType left, LapisType right)
    {
        if (left is ErrorType || right is ErrorType)
        {
            return ErrorType.Instance;
        }

        if (left is NeverType)
        {
            return right;
        }

        if (right is NeverType)
        {
            return left;
        }

        return left == right ? left : null;
    }

    /// <summary>
    /// Igualdade estrutural com <c>==</c> é permitida? Funções ficam de fora:
    /// comparar closures não é decidível e destruiria a equivalência
    /// PE/evaluator (plano 06 §6.7).
    /// </summary>
    public static bool IsComparable(LapisType type) => type switch
    {
        ErrorType => true,
        PrimitiveType p => p.Kind != PrimitiveKind.Void,
        ArrayType a => IsComparable(a.Element),
        FunctionType => false,
        _ => false,
    };
}

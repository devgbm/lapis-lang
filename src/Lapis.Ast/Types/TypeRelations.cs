namespace Lapis.Ast.Types;

/// <summary>
/// As duas únicas relações entre tipos da LapisLang: atribuibilidade e junção de
/// ramos (plano 06 §6.6).
///
/// A subtipagem tem exatamente <b>duas</b> regras, e nenhuma delas é variância:
/// <c>Never &lt;: T</c> (Q13) e <c>[T;N] &lt;: [T;?]</c> (Q29).
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

        // Qualquer valor cabe onde uma primitiva declara Any (hoje só `print`).
        if (target is AnyType)
        {
            return true;
        }

        // Q29: um span de tamanho conhecido cabe onde se espera tamanho
        // desconhecido. `?` não é outro tamanho — é a ausência da informação, e
        // esquecer o que se sabia é sempre seguro.
        //
        // Numa direção só: `[T;?]` num `[T;N]` seria afirmar um tamanho que
        // ninguém verificou. E o elemento continua **invariante** — `[Int;3]` não
        // é `[Any;?]`, senão escrever no span quebraria o tipo.
        if (target is SpanType { Size: UnknownSize } wanted && source is SpanType actual)
        {
            return actual.Element == wanted.Element;
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

        // Dois spans do mesmo elemento e tamanhos diferentes juntam-se em `?`: é
        // o menor tipo que descreve os dois, e é o que faz
        // `if c { .[1] } else { .[1,2] }` ter tipo em vez de erro.
        if (left is SpanType a && right is SpanType b && a.Element == b.Element && a.Size != b.Size)
        {
            return SpanType.Unknown(a.Element);
        }

        return left == right ? left : null;
    }

    /// <summary>
    /// Igualdade estrutural com <c>==</c> é permitida? Funções ficam de fora:
    /// comparar closures não é decidível e destruiria a equivalência
    /// PE/evaluator (plano 06 §6.7).
    /// </summary>
    public static bool IsComparable(LapisType type) => IsComparable(type, []);

    private static bool IsComparable(LapisType type, HashSet<int> visiting) => type switch
    {
        ErrorType => true,
        PrimitiveType p => p.Kind != PrimitiveKind.Void,
        SpanType s => IsComparable(s.Element, visiting),
        FunctionType => false,

        // Um enum é comparável se todas as cargas forem; um struct, se todos os
        // campos forem. O conjunto `visiting` protege contra tipos mutuamente
        // recursivos, que a v0.2 ainda não permite construir mas que virão.
        NamedType n => !visiting.Add(n.Definition.Id) || IsDefinitionComparable(n.Definition, visiting),

        _ => false,
    };

    private static bool IsDefinitionComparable(TypeDefinition definition, HashSet<int> visiting) =>
        definition.Variants.All(v => v.Payload.All(t => IsComparable(t, visiting)))
        && definition.Fields.All(f => IsComparable(f.Type, visiting));
}

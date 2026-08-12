using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Core;
using Lapis.Ast.Surface;
using Lapis.Ast.Typed;

namespace Lapis.PartialEvaluator;

/// <summary>
/// Nomes que uma expressão usa sem declarar. É o que permite eliminar um
/// <c>Let</c> puro cujo nome ninguém lê (plano 12 §12.5).
/// </summary>
public static class FreeVariables
{
    /// <param name="types">
    /// Tabela do checker, quando existe. Serve a um caso só: <c>u.saudar()</c> lê
    /// o <c>Let</c> ligado a <c>User#saudar</c>, e o nome do dono <b>não está na
    /// árvore</b> — é o tipo de <c>u</c>. Só a resolução sabe.
    ///
    /// Sem ela a conta é sintática, e a compensação está em
    /// <see cref="Occurs"/>: um nome de membro nunca é dado como livre-de-uso.
    /// </param>
    public static ImmutableHashSet<string> Of(CoreExpr expression, TypedProgram? types = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var free = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        Collect(expression, free, types);
        return free.ToImmutable();
    }

    /// <summary>
    /// O nome aparece livre em <paramref name="expression"/>?
    ///
    /// Sem a tabela do checker, um nome de <b>membro</b> responde sempre
    /// <c>true</c>: a conta sintática não consegue ligar <c>u.saudar</c> a
    /// <c>User#saudar</c>, e errar para o lado de manter o binding custa uma linha
    /// no residual — errar para o outro apaga código vivo.
    /// </summary>
    public static bool Occurs(string name, CoreExpr expression, TypedProgram? types = null) =>
        (types is null && MemberNames.Split(name) is not null)
        || Of(expression, types).Contains(name);

    private static void Collect(
        CoreExpr node,
        ImmutableHashSet<string>.Builder free,
        TypedProgram? types)
    {
        switch (node)
        {
            case CoreVariable n:
                free.Add(n.Name);
                break;

            // O nome do `Let` liga no corpo: o que o corpo usa com esse nome não é
            // livre, mas o que o **valor** usa é — o nome não é visível no próprio
            // valor (Q8, e é o que torna a 0.2 não recursiva).
            case CoreLet n:
                CollectFromType(n.Annotation, free);
                Collect(n.Value, free, types);
                CollectBinding(n.Name, n.Body, free, types);
                break;

            // Uma atribuição usa o nome: eliminá-la porque "ninguém lê" seria
            // apagar o efeito.
            case CoreAssign n:
                free.Add(n.Name);
                Collect(n.Value, free, types);
                break;

            case CoreLambda n:
                foreach (var parameter in n.Parameters)
                {
                    CollectFromType(parameter.Type, free);
                }

                CollectFromType(n.ReturnType, free);

                var body = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
                Collect(n.Body, body, types);

                foreach (var parameter in n.Parameters)
                {
                    body.Remove(parameter.Name);
                }

                // Um parâmetro const genérico também liga um nome dentro do corpo.
                foreach (var typeParameter in n.TypeParameters.Where(p => p.IsConst))
                {
                    body.Remove(typeParameter.Name);
                }

                free.UnionWith(body);
                break;

            case CoreMatch n:
                Collect(n.Scrutinee, free, types);

                foreach (var arm in n.Arms)
                {
                    // `Result.Ok(v)` cita o enum pelo nome: sem contá-lo, a
                    // definição do enum viraria código morto e o residual quebraria.
                    free.UnionWith(EnumsNamedBy(arm.Pattern));

                    var armBody = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
                    Collect(arm.Body, armBody, types);
                    armBody.ExceptWith(BoundBy(arm.Pattern));
                    free.UnionWith(armBody);
                }

                break;

            // `User.hello` lê o `Let` ligado a `User#hello`, e o nome sintético não
            // aparece em lugar nenhum da árvore: o uso é um `CoreField`, cujo
            // `Name` é só `hello`. Sem contá-lo, o membro vira código morto e o
            // residual cita um membro que não existe mais.
            //
            // A resolução do checker diz o nome exato, e é a única fonte que
            // funciona para chamada por instância: em `u.saudar()` o dono é o
            // **tipo** de `u`, que a árvore não carrega.
            //
            // Sem a tabela, sobra a leitura sintática — certa para `T.m`, e
            // compensada em `Occurs` para o resto.
            case CoreField n:
                if (types?.ResolutionOf<MemberResolution>(n) is { } member)
                {
                    free.Add(member.SyntheticName);
                }
                else if (n.Target is CoreVariable owner)
                {
                    free.Add(MemberNames.Of(owner.Name, n.Name));
                }

                Collect(n.Target, free, types);
                break;

            // O elemento de `.[T; inicial; n]` é uma **anotação**: `T` pode ser um
            // tipo do usuário, e apagá-lo por "ninguém usa" quebraria a construção.
            case CoreSpanRepeat n:
                CollectFromType(n.Element, free);
                Collect(n.Initializer, free, types);
                Collect(n.Size, free, types);
                break;

            // A construção referencia o tipo por **string**, não por
            // `CoreVariable` — mas `Flag` em `.Flag { }` é o mesmo `Flag` do
            // `def Flag = type { }`, e some junto se ninguém contar.
            case CoreConstruct n:
                free.Add(n.TypeName);

                foreach (var argument in n.TypeArguments)
                {
                    CollectFromArgument(argument, free);
                }

                foreach (var field in n.Fields)
                {
                    Collect(field.Value, free, types);
                }

                break;

            // `escala<n>(5)` **usa** `n`, e o uso não é um `CoreVariable`: um
            // argumento genérico nu é um `CoreNameArgument`, uma string. Sem
            // contá-lo, `def n = 3;` vira código morto e o residual cita um nome
            // que não existe mais. Vale igual para o nome de tipo em
            // `Caixa<Cor>` — é o mesmo raciocínio de `CoreConstruct`.
            case CoreInstantiate n:
                Collect(n.Target, free, types);

                foreach (var argument in n.Arguments)
                {
                    CollectFromArgument(argument, free);
                }

                break;

            default:
                foreach (var child in Children(node))
                {
                    Collect(child, free, types);
                }

                break;
        }
    }

    private static void CollectBinding(
        string name,
        CoreExpr body,
        ImmutableHashSet<string>.Builder free,
        TypedProgram? types)
    {
        var inner = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        Collect(body, inner, types);
        inner.Remove(name);
        free.UnionWith(inner);
    }

    /// <summary>
    /// Nomes de tipo citados por uma anotação. Um tipo definido pelo usuário é um
    /// binding como outro qualquer, e apagá-lo por "ninguém usa" quebraria a
    /// anotação que o cita.
    /// </summary>
    private static void CollectFromType(TypeSyntax? type, ImmutableHashSet<string>.Builder free)
    {
        switch (type)
        {
            case null:
                break;

            case NamedTypeSyntax n:
                free.Add(n.Name);

                foreach (var argument in n.Arguments)
                {
                    CollectFromSurfaceArgument(argument, free);
                }

                break;

            case SpanTypeSyntax n:
                CollectFromType(n.Element, free);

                // O tamanho pode ser um nome: `[Int;n]` cita o `n`, e é a mesma
                // razão de contar o argumento genérico nu.
                if (n.Size is NamedSizeSyntax size)
                {
                    free.Add(size.Name);
                }

                break;

            case FunctionTypeSyntax n:
                foreach (var parameter in n.Parameters)
                {
                    CollectFromType(parameter, free);
                }

                CollectFromType(n.Return, free);
                break;
        }
    }

    private static void CollectFromSurfaceArgument(
        GenericArgumentSyntax argument,
        ImmutableHashSet<string>.Builder free)
    {
        switch (argument)
        {
            case TypeArgumentSyntax a:
                CollectFromType(a.Type, free);
                break;

            case NameArgumentSyntax a:
                free.Add(a.Name);
                break;
        }
    }

    private static void CollectFromArgument(
        CoreGenericArgument argument,
        ImmutableHashSet<string>.Builder free,
        TypedProgram? types = null)
    {
        switch (argument)
        {
            case CoreTypeArgument a:
                CollectFromType(a.Type, free);
                break;

            case CoreNameArgument a:
                free.Add(a.Name);
                break;

            case CoreValueArgument a:
                Collect(a.Value, free, types);
                break;
        }
    }

    private static IEnumerable<string> EnumsNamedBy(CorePattern pattern) => pattern switch
    {
        CoreVariantPattern p => [p.EnumName, .. p.Arguments.SelectMany(EnumsNamedBy)],
        _ => [],
    };

    private static IEnumerable<string> BoundBy(CorePattern pattern) => pattern switch
    {
        CoreBindingPattern p => [p.Name],
        CoreVariantPattern p => p.Arguments.SelectMany(BoundBy),
        _ => [],
    };

    private static IEnumerable<CoreExpr> Children(CoreExpr node)
    {
        switch (node)
        {
            case CoreCall n:
                yield return n.Callee;

                foreach (var argument in n.Arguments)
                {
                    yield return argument;
                }

                break;

            case CoreInstantiate n:
                yield return n.Target;

                foreach (var argument in n.Arguments.OfType<CoreValueArgument>())
                {
                    yield return argument.Value;
                }

                break;

            case CoreEnumDef or CoreTypeDef:
                break;

            case CoreReturn { Value: { } value }:
                yield return value;
                break;

            case CoreThrow n:
                yield return n.Value;
                break;

            case CoreIf n:
                yield return n.Condition;
                yield return n.Then;
                yield return n.Else;
                break;

            case CoreBinary n:
                yield return n.Left;
                yield return n.Right;
                break;

            case CoreUnary n:
                yield return n.Operand;
                break;

            case CoreSpan n:
                foreach (var element in n.Elements)
                {
                    yield return element;
                }

                break;

            case CoreSpanRepeat n:
                yield return n.Initializer;
                yield return n.Size;
                break;

            case CoreIndex n:
                yield return n.Target;
                yield return n.Index;
                break;

            case CoreField n:
                yield return n.Target;
                break;

            case CoreLoop n:
                yield return n.Body;
                break;

            case CoreBreak { Value: { } value }:
                yield return value;
                break;
        }
    }
}

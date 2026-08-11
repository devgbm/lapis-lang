using System.Collections.Immutable;
using Lapis.Ast.Core;
using Lapis.Ast.Surface;

namespace Lapis.PartialEvaluator;

/// <summary>
/// Nomes que uma expressão usa sem declarar. É o que permite eliminar um
/// <c>Let</c> puro cujo nome ninguém lê (plano 12 §12.5).
/// </summary>
public static class FreeVariables
{
    public static ImmutableHashSet<string> Of(CoreExpr expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var free = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        Collect(expression, free);
        return free.ToImmutable();
    }

    /// <summary>O nome aparece livre em <paramref name="expression"/>?</summary>
    public static bool Occurs(string name, CoreExpr expression) => Of(expression).Contains(name);

    private static void Collect(CoreExpr node, ImmutableHashSet<string>.Builder free)
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
                Collect(n.Value, free);
                CollectBinding(n.Name, n.Body, free);
                break;

            // Uma atribuição usa o nome: eliminá-la porque "ninguém lê" seria
            // apagar o efeito.
            case CoreAssign n:
                free.Add(n.Name);
                Collect(n.Value, free);
                break;

            case CoreLambda n:
                foreach (var parameter in n.Parameters)
                {
                    CollectFromType(parameter.Type, free);
                }

                CollectFromType(n.ReturnType, free);

                var body = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
                Collect(n.Body, body);

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
                Collect(n.Scrutinee, free);

                foreach (var arm in n.Arms)
                {
                    // `Result.Ok(v)` cita o enum pelo nome: sem contá-lo, a
                    // definição do enum viraria código morto e o residual quebraria.
                    free.UnionWith(EnumsNamedBy(arm.Pattern));

                    var armBody = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
                    Collect(arm.Body, armBody);
                    armBody.ExceptWith(BoundBy(arm.Pattern));
                    free.UnionWith(armBody);
                }

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
                    Collect(field.Value, free);
                }

                break;

            default:
                foreach (var child in Children(node))
                {
                    Collect(child, free);
                }

                break;
        }
    }

    private static void CollectBinding(string name, CoreExpr body, ImmutableHashSet<string>.Builder free)
    {
        var inner = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        Collect(body, inner);
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

            case ArrayTypeSyntax n:
                CollectFromType(n.Element, free);
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
        ImmutableHashSet<string>.Builder free)
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
                Collect(a.Value, free);
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

            case CoreArray n:
                foreach (var element in n.Elements)
                {
                    yield return element;
                }

                break;

            case CoreIndex n:
                yield return n.Target;
                yield return n.Index;
                break;

            case CoreField n:
                yield return n.Target;
                break;

            case CoreGotoIf n:
                yield return n.Condition;
                break;

            case CoreLabeled n:
                yield return n.Entry;

                foreach (var join in n.Joins)
                {
                    yield return join.Body;
                }

                break;
        }
    }
}

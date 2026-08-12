using Lapis.Ast;
using Lapis.Ast.Core;
using Lapis.Ast.Types;
using Lapis.Runtime;

namespace Lapis.PartialEvaluator;

/// <summary>
/// Tempo de ligação de um valor (spec §39): conhecido agora, ou só em execução.
/// </summary>
public abstract record PEValue
{
    public abstract LapisType Type { get; }
}

/// <summary>
/// Valor conhecido em tempo de partial evaluation.
///
/// Só carrega o que tem **forma sintática**: primitivos e arrays deles. Uma
/// closure, um enum construído ou um struct não têm como voltar a ser expressão
/// sem inventar nomes que podem estar sombreados no ponto de emissão — e um PE
/// que captura nome deixa de preservar o comportamento, que é a única coisa que
/// ele não pode fazer (spec §40). Ver <see cref="Residualizer.CanResidualize"/>.
/// </summary>
public sealed record Known(Value Value) : PEValue
{
    public override LapisType Type => Value.Type;
}

/// <summary>
/// Valor que só existe em execução. Carrega o tipo porque o residual precisa
/// continuar tipável, e porque o plano 14 vai refinar isto com domínios
/// abstratos (intervalos).
/// </summary>
public sealed record Unknown(LapisType ValueType) : PEValue
{
    public override LapisType Type => ValueType;
}

/// <summary>
/// O que a especialização de uma expressão produziu.
/// </summary>
public abstract record PEResult
{
    public abstract LapisType Type { get; }
}

public sealed record StaticResult(Value Value) : PEResult
{
    public override LapisType Type => Value.Type;
}

public sealed record DynamicResult(CoreExpr Residual, LapisType ResultType) : PEResult
{
    /// <summary>
    /// Um valor que o PE <b>conhece</b> mas não sabe escrever.
    ///
    /// Um struct ou um enum construído não têm forma sintática que não cite um
    /// nome de tipo — e esse nome pode estar sombreado no ponto de emissão
    /// (<see cref="Residualizer.CanResidualize"/>). O residual, então, continua
    /// sendo a expressão original; mas saber o valor ainda serve para **decidir**
    /// o que vem depois, e é isso que faz <c>reflect(User).name</c> dobrar para
    /// <c>"User"</c> sem que <c>reflect(User)</c> jamais vire um literal de struct.
    ///
    /// Só é preenchido quando a expressão residual é pura e avaliá-la produz
    /// exatamente este valor. Fora disso, <c>null</c>.
    /// </summary>
    public Value? Opaque { get; init; }

    public override LapisType Type => ResultType;
}

public enum PECompletionKind
{
    /// <summary>A avaliação segue o fluxo normal.</summary>
    Normal,

    /// <summary>Um <c>return</c> foi executado em tempo de PE: o que vem depois é inalcançável.</summary>
    Returned,

    /// <summary>
    /// O residual contém um <c>return</c> cuja execução depende de valor dinâmico.
    ///
    /// É a peça que a maioria dos partial evaluators de brinquedo esquece: quando
    /// um ramo retorna e o outro não, o que vem depois **não** pode ser executado
    /// estaticamente — só residualizado.
    /// </summary>
    MayReturn,
}

public readonly record struct PECompletion(PECompletionKind Kind, PEResult Result)
{
    public static PECompletion Normal(PEResult result) => new(PECompletionKind.Normal, result);

    public static PECompletion Normal(Value value) => Normal(new StaticResult(value));

    public static PECompletion Returned(PEResult result) => new(PECompletionKind.Returned, result);

    public static PECompletion MayReturn(PEResult result) => new(PECompletionKind.MayReturn, result);

    public bool IsNormal => Kind == PECompletionKind.Normal;

    /// <summary>O fluxo depois desta completion é alcançável estaticamente?</summary>
    public bool FlowsThroughStatically => Kind == PECompletionKind.Normal;

    /// <summary>
    /// Junção de dois caminhos (os ramos de um <c>if</c>, os braços de um
    /// <c>match</c>).
    ///
    /// <c>Returned</c> só sobrevive quando os <b>dois</b> lados retornam; basta um
    /// retorno possível de um lado para o resultado virar <c>MayReturn</c>, e com
    /// ele o código seguinte deixar de ser executável em tempo de PE.
    /// </summary>
    public static PECompletionKind Join(PECompletionKind left, PECompletionKind right)
    {
        if (left == right)
        {
            return left;
        }

        return left == PECompletionKind.Normal && right == PECompletionKind.Normal
            ? PECompletionKind.Normal
            : PECompletionKind.MayReturn;
    }
}

/// <summary>
/// Um nome no ambiente estático.
/// </summary>
/// <param name="Value">O que se sabe do valor.</param>
/// <param name="ResidualReference">
/// Por qual expressão este nome é acessível no programa residual. Para um binding
/// dinâmico que virou um <c>Let</c> renomeado, é a variável nova — é o que impede
/// captura de nome quando o PE renomeia.
/// </param>
public sealed record PEBinding(PEValue Value, CoreExpr? ResidualReference = null)
{
    /// <summary>
    /// O binding foi declarado com <c>var</c>.
    ///
    /// Um <c>def</c> ligado a um <c>var</c> é um <b>instantâneo</b>; o <c>var</c> é
    /// um slot. Trocar um pelo outro só vale enquanto o slot não muda, e é isso
    /// que impede a substituição de referência trivial de atravessar uma
    /// atribuição.
    /// </summary>
    public bool IsMutable { get; init; }
}

/// <summary>
/// Ambiente estático: mapa de nomes encadeado, na mesma forma léxica do
/// <see cref="Lapis.Runtime.Environment"/> do evaluator.
/// </summary>
public sealed class StaticEnvironment
{
    private readonly Dictionary<string, PEBinding> _bindings = new(StringComparer.Ordinal);

    private StaticEnvironment(StaticEnvironment? parent) => Parent = parent;

    public static StaticEnvironment Root() => new(null);

    public StaticEnvironment? Parent { get; }

    public StaticEnvironment Child() => new(this);

    public bool TryLookup(string name, out PEBinding binding)
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            if (scope._bindings.TryGetValue(name, out var found))
            {
                binding = found;
                return true;
            }
        }

        binding = null!;
        return false;
    }

    /// <summary>Declara num escopo filho, para que o pai não seja alterado.</summary>
    public StaticEnvironment Extend(string name, PEBinding binding)
    {
        var child = Child();
        child._bindings[name] = binding;
        return child;
    }

    /// <summary>Declara no próprio escopo. Usado só ao montar o ambiente inicial.</summary>
    public void Declare(string name, PEBinding binding) => _bindings[name] = binding;

    /// <summary>
    /// Declara um nome top-level como desconhecido, <b>mesmo que o programa o
    /// defina com um literal</b>.
    ///
    /// Sem isto o pedido não teria efeito: um <c>def n = 2;</c> sombreia qualquer
    /// declaração da raiz, e o PE veria <c>2</c>. E sem entrada externa na v0.2,
    /// todo top-level é estático por construção — é este forçamento que dá ao
    /// <c>lapis pe</c> um programa interessante para especializar.
    /// </summary>
    public void DeclareDynamic(string name, LapisType type)
    {
        _forcedDynamic.Add(name);
        Declare(name, new PEBinding(new Unknown(type)));
    }

    /// <summary>O nome foi declarado desconhecido por quem pediu a especialização?</summary>
    public bool IsForcedDynamic(string name)
    {
        var root = this;

        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        return root._forcedDynamic.Contains(name);
    }

    private readonly HashSet<string> _forcedDynamic = new(StringComparer.Ordinal);
}

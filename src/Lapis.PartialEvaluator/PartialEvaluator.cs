using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Core;
using Lapis.Ast.Typed;
using Lapis.Ast.Types;
using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.PartialEvaluator;

/// <summary>Cada transformação atrás de uma flag, para que um teste de equivalência que quebre aponte qual.</summary>
public sealed record PEOptions(
    bool ConstantFolding = true,
    bool DeadCodeElimination = true,
    bool Inlining = false,
    bool BoundsCheckElimination = false,
    int MaxUnfoldDepth = 8,
    int MaxResidualGrowthFactor = 4);

/// <summary>Alimenta a pergunta de pesquisa da spec §61 e o tracing do plano 15.</summary>
public sealed record PEStatistics(
    int NodesBefore,
    int NodesAfter,
    int ConstantsFolded,
    int BranchesEliminated,
    int BindingsEliminated);

public sealed record PartialEvaluationResult(CoreProgram Residual, PEStatistics Statistics);

/// <summary>
/// <c>CoreProgram × StaticEnvironment → CoreProgram residual</c>, preservando o
/// comportamento observável (spec §40):
///
/// <code>evaluate(P, S) ≡ evaluate(PE(P, S), S)</code>
///
/// Esta fatia é a do plano 12: redução de literais, constant folding, propagação
/// de variáveis e eliminação de código morto. Especialização de chamadas
/// (plano 13) e análise de intervalos (plano 14) vêm depois.
///
/// Uma passada, sem ponto fixo: sem inlining não há oportunidade nova a
/// descobrir, e uma passada mantém a terminação trivial.
/// </summary>
public sealed class PartialEvaluator
{
    /// <summary>
    /// Até quantos elementos vale a pena materializar ao dobrar uma repetição.
    ///
    /// Não é limite da linguagem — é onde dobrar deixa de ser otimização: um nó
    /// vira <c>n</c> literais no residual, e para <c>n</c> grande isso é
    /// pessimização. Acima do teto a construção atravessa intacta, o que é sempre
    /// seguro.
    /// </summary>
    private const int MaxFoldedRepeat = 1_024;

    private readonly CoreFactory _factory = new();
    private readonly Residualizer _residualizer;
    private readonly PEOptions _options;
    private readonly TypedProgram? _types;
    private readonly PreludeScope? _prelude;

    private int _folded;
    private int _branchesEliminated;
    private int _bindingsEliminated;

    /// <summary>
    /// Quantas fronteiras de função acima. Só o nível 0 é "top-level", e é lá que
    /// <see cref="StaticEnvironment.IsForcedDynamic"/> vale — forçar um nome
    /// homônimo declarado dentro de uma função seria surpresa, não pedido.
    /// </summary>
    private int _lambdaDepth;

    private PartialEvaluator(PEOptions options, TypedProgram? types, PreludeScope? prelude)
    {
        _options = options;
        _types = types;
        _prelude = prelude;
        _residualizer = new Residualizer(_factory);
    }

    /// <param name="types">
    /// Tabela de tipos, quando existe. É opcional de propósito: rodar o PE sobre um
    /// residual que ele mesmo produziu (idempotência) não deve exigir re-checagem.
    /// </param>
    /// <param name="prelude">
    /// Necessário só para dobrar <c>reflect</c>, que constrói um <c>TypeInfo</c> do
    /// prelude (plano 19 §19.5). Sem ele, <c>reflect</c> atravessa intacto — o PE
    /// deixa de progredir, nunca de estar correto.
    /// </param>
    public static PartialEvaluationResult Specialize(
        CoreProgram program,
        StaticEnvironment? staticEnvironment = null,
        PEOptions? options = null,
        TypedProgram? types = null,
        PreludeScope? prelude = null)
    {
        ArgumentNullException.ThrowIfNull(program);

        var evaluator = new PartialEvaluator(options ?? new PEOptions(), types, prelude);
        var environment = staticEnvironment ?? StaticEnvironment.Root();

        var completion = evaluator.Specialize(program.Body, environment);
        var residual = evaluator._residualizer.Residualize(completion.Result, program.Body.Span);

        return new PartialEvaluationResult(
            evaluator._factory.Program(residual),
            new PEStatistics(
                NodeCounter.Count(program.Body),
                NodeCounter.Count(residual),
                evaluator._folded,
                evaluator._branchesEliminated,
                evaluator._bindingsEliminated));
    }

    // ------------------------------------------------------------ despacho

    private PECompletion Specialize(CoreExpr node, StaticEnvironment environment) => node switch
    {
        CoreLiteral n => PECompletion.Normal(FromConstant(n.Value)),
        CoreVariable n => SpecializeVariable(n, environment),
        CoreLet n => SpecializeLet(n, environment),
        CoreBinary n => SpecializeBinary(n, environment),
        CoreUnary n => SpecializeUnary(n, environment),
        CoreIf n => SpecializeIf(n, environment),
        CoreReturn n => SpecializeReturn(n, environment),
        CoreThrow n => SpecializeThrow(n, environment),
        CoreLambda n => SpecializeLambda(n, environment),
        CoreCall n => SpecializeCall(n, environment),
        CoreSpan n => SpecializeArray(n, environment),
        CoreSpanRepeat n => SpecializeSpanRepeat(n, environment),
        CoreIndex n => SpecializeIndex(n, environment),
        CoreMatch n => SpecializeMatch(n, environment),
        CoreIs n => SpecializeIs(n, environment),
        CoreAssign n => SpecializeAssign(n, environment),
        CoreLoop n => SpecializeLoop(n, environment),
        CoreBreak n => SpecializeBreak(n, environment),
        CoreContinue or CoreEnumDef or CoreTypeDef => PECompletion.Normal(Keep(node)),
        CoreField n => SpecializeField(n, environment),
        CoreInstantiate n => SpecializeInstantiate(n, environment),
        CoreConstruct n => SpecializeConstruct(n, environment),
        _ => throw InternalCompilerException.Unreachable(node, node.Span),
    };

    // -------------------------------------------------------------- nomes

    private PECompletion SpecializeVariable(CoreVariable node, StaticEnvironment environment)
    {
        if (!environment.TryLookup(node.Name, out var binding))
        {
            // Nativos e bindings do prelude não entram no ambiente estático: eles
            // existem, e continuam existindo no residual pelo mesmo nome.
            return PECompletion.Normal(Keep(node));
        }

        return binding.Value switch
        {
            Known known => PECompletion.Normal(Reduce(known.Value, node)),
            _ => PECompletion.Normal(new DynamicResult(binding.ResidualReference ?? node, binding.Value.Type)),
        };
    }

    /// <summary>
    /// O coração da propagação. Três destinos para um <c>Let</c>: o valor é
    /// conhecido e some substituído; é dinâmico mas trivial e some substituído pela
    /// referência; ou sobrevive como <c>Let</c> residual.
    /// </summary>
    private PECompletion SpecializeLet(CoreLet node, StaticEnvironment environment)
    {
        var value = Specialize(node.Value, environment);

        // O valor executou um `return`: o corpo é inalcançável.
        if (value.Kind == PECompletionKind.Returned)
        {
            return value;
        }

        // O valor **pode** ter retornado: o corpo precisa sobreviver, residualizado.
        if (value.Kind == PECompletionKind.MayReturn)
        {
            var afterMayReturn = Specialize(node.Body, Bind(environment, node, Dynamic(node)));

            return PECompletion.MayReturn(new DynamicResult(
                Rebuild(node, _residualizer.Residualize(value.Result, node.Value.Span),
                    _residualizer.Residualize(afterMayReturn.Result, node.Body.Span)),
                afterMayReturn.Result.Type));
        }

        // Quem pediu a especialização pode ter declarado este nome desconhecido.
        var forcedDynamic = _lambdaDepth == 0 && environment.IsForcedDynamic(node.Name);

        // Um `var` nunca propaga: o valor de hoje não é o de amanhã (Q25).
        if (!node.IsMutable && !forcedDynamic && value.Result is StaticResult statik)
        {
            _folded++;
            var propagated = Specialize(
                node.Body, environment.Extend(node.Name, new PEBinding(new Known(statik.Value))));

            // Propagar o valor é uma coisa; **apagar** o binding é outra. Com a
            // eliminação de código morto desligada o `Let` fica, ainda que ninguém
            // leia o nome — é o que mantém as duas flags independentes, e com elas
            // a possibilidade de isolar qual transformação quebrou uma propriedade.
            if (_options.DeadCodeElimination)
            {
                return propagated;
            }

            return new PECompletion(propagated.Kind, new DynamicResult(
                Rebuild(
                    node,
                    _residualizer.Residualize(statik.Value, node.Value.Span),
                    _residualizer.Residualize(propagated.Result, node.Body.Span)),
                propagated.Result.Type));
        }

        var residualValue = _residualizer.Residualize(value.Result, node.Value.Span);

        // Uma referência trivial e pura pode substituir o nome — mas só se o corpo
        // não redeclarar o nome referenciado, senão a substituição capturaria o
        // binding errado; e só se o **referenciado** for imutável.
        //
        // `def copia = u;` com `u` sendo `var` é um instantâneo, não um apelido:
        // trocar `copia` por `u` faz uma atribuição posterior a `u` mudar o que
        // `copia` valia. O caso não aparecia no corpus até `def b = a; a.campo = e;`
        // (plano 21 §21.3b) trazê-lo, mas o defeito é do M7 e vale para
        // `def b = a; a = 2;` do mesmo jeito.
        if (!node.IsMutable
            && !forcedDynamic
            && residualValue is CoreVariable reference
            && !ReferencesMutable(reference, environment)
            && !Rebinds(node.Body, reference.Name))
        {
            _bindingsEliminated++;

            return Specialize(
                node.Body,
                environment.Extend(node.Name, new PEBinding(new Unknown(value.Result.Type), reference)));
        }

        var body = Specialize(node.Body, Bind(environment, node, value.Result.Type));
        var residualBody = _residualizer.Residualize(body.Result, node.Body.Span);

        // Código morto: um `Let` puro cujo nome ninguém lê no residual.
        if (_options.DeadCodeElimination
            && !node.IsMutable
            && !forcedDynamic
            && Effects.IsPure(residualValue)
            && !FreeVariables.Occurs(node.Name, residualBody, _types))
        {
            _bindingsEliminated++;
            return body;
        }

        return new PECompletion(body.Kind, new DynamicResult(
            Rebuild(node, residualValue, residualBody), body.Result.Type));
    }

    private StaticEnvironment Bind(StaticEnvironment environment, CoreLet node, LapisType type) =>
        environment.Extend(
            node.Name,
            new PEBinding(new Unknown(type), _factory.Variable(node.NameSpan, node.Name))
            {
                IsMutable = node.IsMutable,
            });

    /// <summary>
    /// O nome referenciado é um <c>var</c>? Um nome que o ambiente estático não
    /// conhece — nativa, binding do prelude — nunca é.
    /// </summary>
    private static bool ReferencesMutable(CoreVariable reference, StaticEnvironment environment) =>
        environment.TryLookup(reference.Name, out var binding) && binding.IsMutable;

    private LapisType Dynamic(CoreLet node) => TypeOf(node.Value);

    private CoreLet Rebuild(CoreLet node, CoreExpr value, CoreExpr body) =>
        _factory.Let(
            node.Span,
            node.Name,
            node.Annotation,
            value,
            body,
            node.IsSynthetic,
            node.NameSpan,
            node.IsMutable);

    // ---------------------------------------------------------- operadores

    private PECompletion SpecializeBinary(CoreBinary node, StaticEnvironment environment)
    {
        var left = Specialize(node.Left, environment);

        if (!left.FlowsThroughStatically)
        {
            return left;
        }

        var right = Specialize(node.Right, environment);

        if (!right.FlowsThroughStatically)
        {
            return right;
        }

        if (_options.ConstantFolding
            && left.Result is StaticResult a
            && right.Result is StaticResult b)
        {
            // Dobrar com as primitivas do runtime, nunca com aritmética própria:
            // reimplementá-la seria a forma mais provável de introduzir divergência
            // semântica entre PE e evaluator (spec §38).
            var folded = node.Operator.IsComparison()
                ? Primitives.Compare(node.Operator, a.Value, b.Value)
                : Primitives.Apply(node.Operator, a.Value, b.Value);

            _folded++;
            return PECompletion.Normal(Reduce(folded, node));
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.Binary(
                node.Span,
                node.Operator,
                _residualizer.Residualize(left.Result, node.Left.Span),
                _residualizer.Residualize(right.Result, node.Right.Span),
                node.OperatorSpan),
            TypeOf(node)));
    }

    private PECompletion SpecializeUnary(CoreUnary node, StaticEnvironment environment)
    {
        var operand = Specialize(node.Operand, environment);

        if (!operand.FlowsThroughStatically)
        {
            return operand;
        }

        if (_options.ConstantFolding && operand.Result is StaticResult value)
        {
            var folded = node.Operator == UnaryOperator.Negate
                ? Primitives.Negate(value.Value)
                : Primitives.Not(value.Value);

            _folded++;
            return PECompletion.Normal(Reduce(folded, node));
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.Unary(node.Span, node.Operator, _residualizer.Residualize(operand.Result, node.Operand.Span)),
            TypeOf(node)));
    }

    // -------------------------------------------------------- condicionais

    private PECompletion SpecializeIf(CoreIf node, StaticEnvironment environment)
    {
        var condition = Specialize(node.Condition, environment);

        if (!condition.FlowsThroughStatically)
        {
            return condition;
        }

        // Ramo morto: o outro nem chega a ser especializado, então um `1/0` ou um
        // `print` lá dentro simplesmente não existe no residual — ele era
        // inalcançável no original também.
        if (condition.Result is StaticResult { Value: BoolValue taken })
        {
            _branchesEliminated++;
            return Specialize(taken.Value ? node.Then : node.Else, environment.Child());
        }

        var then = Specialize(node.Then, environment.Child());
        var otherwise = Specialize(node.Else, environment.Child());

        var residual = _factory.If(
            node.Span,
            _residualizer.Residualize(condition.Result, node.Condition.Span),
            _residualizer.Residualize(then.Result, node.Then.Span),
            _residualizer.Residualize(otherwise.Result, node.Else.Span));

        return new PECompletion(
            PECompletion.Join(then.Kind, otherwise.Kind),
            new DynamicResult(residual, TypeOf(node)));
    }

    private PECompletion SpecializeReturn(CoreReturn node, StaticEnvironment environment)
    {
        if (node.Value is null)
        {
            return PECompletion.Returned(new DynamicResult(_factory.Return(node.Span, null), NeverType.Instance));
        }

        var value = Specialize(node.Value, environment);

        if (!value.FlowsThroughStatically)
        {
            return value;
        }

        var residual = _factory.Return(node.Span, _residualizer.Residualize(value.Result, node.Value.Span));

        return PECompletion.Returned(new DynamicResult(residual, NeverType.Instance));
    }

    /// <summary>
    /// <c>throw</c> nunca é dobrado: rejeitar é o efeito, e antecipá-lo mudaria
    /// <b>quando</b> a compilação falha. O que o PE faz é o que faz com um
    /// <c>return</c> — especializar o valor e reconhecer que o fluxo não continua.
    ///
    /// Só aparece dentro de um <c>constraint</c>: no programa do usuário
    /// <c>LAP0507</c> chega antes do PE.
    /// </summary>
    private PECompletion SpecializeThrow(CoreThrow node, StaticEnvironment environment)
    {
        var value = Specialize(node.Value, environment);

        if (!value.FlowsThroughStatically)
        {
            return value;
        }

        var residual = _factory.Throw(node.Span, _residualizer.Residualize(value.Result, node.Value.Span));

        return PECompletion.Returned(new DynamicResult(residual, NeverType.Instance));
    }

    // ------------------------------------------------------------ funções

    /// <summary>
    /// O corpo é especializado com os parâmetros desconhecidos — é o que dobra
    /// constantes dentro de funções sem precisar de inlining (plano 13).
    ///
    /// A completion do corpo <b>não</b> sai: um <c>return</c> lá dentro encerra
    /// aquela função, não a que a contém.
    /// </summary>
    private PECompletion SpecializeLambda(CoreLambda node, StaticEnvironment environment)
    {
        var inner = environment.Child();

        foreach (var parameter in node.Parameters)
        {
            inner = inner.Extend(
                parameter.Name,
                new PEBinding(new Unknown(AnyType.Instance), _factory.Variable(parameter.Span, parameter.Name)));
        }

        foreach (var typeParameter in node.TypeParameters.Where(p => p.IsConst))
        {
            inner = inner.Extend(
                typeParameter.Name,
                new PEBinding(
                    new Unknown(AnyType.Instance),
                    _factory.Variable(typeParameter.Span, typeParameter.Name)));
        }

        _lambdaDepth++;

        PECompletion body;

        try
        {
            body = Specialize(node.Body, inner);
        }
        finally
        {
            _lambdaDepth--;
        }

        var residual = _factory.Lambda(
            node.Span,
            node.TypeParameters,
            node.Parameters,
            node.ReturnType,
            _residualizer.Residualize(body.Result, node.Body.Span),
            node.BodyEndSpan);

        return PECompletion.Normal(new DynamicResult(residual, TypeOf(node)));
    }

    /// <summary>
    /// Chamada é sempre residualizada nesta fatia. Não é limitação de esforço: é
    /// que <c>print</c> é uma chamada, e executar <c>print(30)</c> em tempo de PE
    /// moveria a saída do programa para o tempo de compilação — violação direta de
    /// §40. A especialização de chamadas é do plano 13, com regras próprias.
    /// </summary>
    private PECompletion SpecializeCall(CoreCall node, StaticEnvironment environment)
    {
        // `reflect(T)` sobre um tipo conhecido é **inteiramente estático**: a
        // definição não depende de valor de runtime nenhum (plano 19 §19.5). É um
        // caso limpo em que uma feature aparentemente cara sai de graça depois da
        // especialização — `reflect(User).name` residualiza como `"User"`.
        if (_types?.ResolutionOf<ReflectResolution>(node) is { Definition: { } definition } reflect
            && _prelude is not null)
        {
            return PECompletion.Normal(
                Reduce(_prelude.MakeTypeInfo(definition, reflect.Arguments), node));
        }

        var callee = Specialize(node.Callee, environment);

        if (!callee.FlowsThroughStatically)
        {
            return callee;
        }

        var arguments = ImmutableArray.CreateBuilder<CoreExpr>(node.Arguments.Length);

        foreach (var argument in node.Arguments)
        {
            var specialized = Specialize(argument, environment);

            if (!specialized.FlowsThroughStatically)
            {
                return specialized;
            }

            arguments.Add(_residualizer.Residualize(specialized.Result, argument.Span));
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.Call(
                node.Span,
                _residualizer.Residualize(callee.Result, node.Callee.Span),
                arguments.MoveToImmutable()),
            TypeOf(node)));
    }

    // ------------------------------------------------------ dados compostos

    private PECompletion SpecializeArray(CoreSpan node, StaticEnvironment environment)
    {
        var results = ImmutableArray.CreateBuilder<PEResult>(node.Elements.Length);

        foreach (var element in node.Elements)
        {
            var specialized = Specialize(element, environment);

            if (!specialized.FlowsThroughStatically)
            {
                return specialized;
            }

            results.Add(specialized.Result);
        }

        var elements = results.ToImmutable();

        if (_options.ConstantFolding && elements.All(r => r is StaticResult))
        {
            var values = elements.Cast<StaticResult>().Select(r => r.Value).ToImmutableArray();
            var elementType = TypeOf(node) is SpanType array
                ? array.Element
                : values.FirstOrDefault()?.Type ?? AnyType.Instance;

            return PECompletion.Normal(Reduce(new SpanValue(values, elementType), node));
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.Array(
                node.Span,
                [.. elements.Select((r, i) => _residualizer.Residualize(r, node.Elements[i].Span))]),
            TypeOf(node)));
    }

    /// <summary>
    /// <c>.[T; inicial; n]</c>.
    ///
    /// Dobra quando o inicializador é conhecido <b>e</b> o checker fixou a
    /// quantidade — aí o span inteiro é um valor, e o residual é a lista. Sem a
    /// quantidade fixa não há o que construir em compilação: o número só existe
    /// em execução.
    ///
    /// A repetição dobrada vira uma lista de verdade no residual, então há um teto:
    /// dobrar <c>.[Int; 0; 500000]</c> trocaria um nó por meio milhão de literais
    /// impressos, o que é pessimização, não otimização. Acima do teto a construção
    /// atravessa intacta — recusar-se a dobrar é sempre seguro.
    /// </summary>
    private PECompletion SpecializeSpanRepeat(CoreSpanRepeat node, StaticEnvironment environment)
    {
        var initializer = Specialize(node.Initializer, environment);

        if (!initializer.FlowsThroughStatically)
        {
            return initializer;
        }

        var size = Specialize(node.Size, environment);

        if (!size.FlowsThroughStatically)
        {
            return size;
        }

        if (_options.ConstantFolding
            && _types?.ResolutionOf<SpanRepeatResolution>(node) is { Known: { } known and <= MaxFoldedRepeat }
            && ValueOf(initializer.Result) is { } value
            && TypeOf(node) is SpanType span)
        {
            return PECompletion.Normal(Reduce(
                new SpanValue([.. Enumerable.Repeat(value, known)], span.Element),
                node));
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.SpanRepeat(
                node.Span,
                node.Element,
                _residualizer.Residualize(initializer.Result, node.Initializer.Span),
                _residualizer.Residualize(size.Result, node.Size.Span)),
            TypeOf(node)));
    }

    /// <summary>
    /// Indexação <b>total</b> dobra; parcial, não.
    ///
    /// Com o tamanho no tipo e o índice constante, o checker já provou os limites
    /// (plano 24 §24.5) e deixou uma <see cref="TotalIndexResolution"/>: o
    /// elemento sai nu, e o PE só precisa lê-lo do span estático.
    ///
    /// Sem essa prova o resultado é um <c>Option</c>, e um enum construído não
    /// volta a ser expressão sem citar o nome do enum — que pode estar sombreado no
    /// ponto de emissão. É o plano 14 que trata disso, junto com a eliminação de
    /// bounds check, onde a construção do envelope deixa de ser detalhe e passa a
    /// ser o ponto.
    /// </summary>
    private PECompletion SpecializeIndex(CoreIndex node, StaticEnvironment environment)
    {
        var target = Specialize(node.Target, environment);

        if (!target.FlowsThroughStatically)
        {
            return target;
        }

        var index = Specialize(node.Index, environment);

        if (!index.FlowsThroughStatically)
        {
            return index;
        }

        if (_options.ConstantFolding
            && _types?.ResolutionOf<TotalIndexResolution>(node) is { } total
            && ValueOf(target.Result) is SpanValue span)
        {
            return PECompletion.Normal(Reduce(span.Elements[total.Index], node));
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.Index(
                node.Span,
                _residualizer.Residualize(target.Result, node.Target.Span),
                _residualizer.Residualize(index.Result, node.Index.Span)),
            TypeOf(node)));
    }

    private PECompletion SpecializeField(CoreField node, StaticEnvironment environment)
    {
        // `T.m` não lê o alvo: o membro vive num `Let` sob o nome sintético, e o
        // "alvo" é o tipo, que não carrega valor (plano 21 §21.6). Especializar
        // isto é a mesma coisa que especializar uma variável — e é por isso que a
        // feature não custou nó novo em fase nenhuma.
        // Quando o valor não é conhecido, o que sobrevive é **este** nó, não uma
        // referência ao nome sintético: `User#grita` não é lexável, e emiti-lo
        // produziria um residual que não reparseia. `User.grita` é a forma
        // escrevível do mesmo acesso.
        if (_types?.ResolutionOf<MemberResolution>(node) is { } member
            && environment.TryLookup(member.SyntheticName, out var binding))
        {
            return PECompletion.Normal(binding.Value is Known known
                ? Reduce(known.Value, node)
                : Keep(node));
        }

        var target = Specialize(node.Target, environment);

        if (!target.FlowsThroughStatically)
        {
            return target;
        }

        // `s.length` sobre um span conhecido é uma constante. Só dobra com o valor
        // em mãos: o tamanho no tipo bastaria para o número, mas descartar o alvo
        // sem saber que ele é estático descartaria junto os efeitos dele.
        if (_options.ConstantFolding
            && _types?.ResolutionOf<SpanLengthResolution>(node) is not null
            && ValueOf(target.Result) is SpanValue span)
        {
            return PECompletion.Normal(Reduce(new IntValue(span.Elements.Length), node));
        }

        // O alvo pode ser conhecido sem ser escrevível — um struct de reflection é
        // o caso. Projetar um campo dele pode dar um valor que **é** escrevível, e
        // aí a leitura inteira some do residual.
        if (_options.ConstantFolding
            && ValueOf(target.Result) is StructValue instance
            && instance.Definition.IndexOfField(node.Name) is >= 0 and var index)
        {
            return PECompletion.Normal(Reduce(instance.Fields[index], node));
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.Field(
                node.Span,
                _residualizer.Residualize(target.Result, node.Target.Span),
                node.Name,
                node.NameSpan),
            TypeOf(node)));
    }

    private PECompletion SpecializeInstantiate(CoreInstantiate node, StaticEnvironment environment)
    {
        var target = Specialize(node.Target, environment);

        if (!target.FlowsThroughStatically)
        {
            return target;
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.Instantiate(
                node.Span,
                _residualizer.Residualize(target.Result, node.Target.Span),
                SpecializeArguments(node.Arguments, node.Span, environment)),
            TypeOf(node)));
    }

    /// <summary>
    /// Argumentos genéricos nus (<c>escala&lt;n&gt;</c>) precisam acompanhar a
    /// propagação.
    ///
    /// Um <c>CoreNameArgument</c> é uma <b>string</b>, não um <c>CoreVariable</c>:
    /// a substituição que troca `n` pelo valor não passa por ele, e o `Let` que
    /// declarava `n` some por ninguém mais lê-lo. O residual ficaria citando um
    /// nome que não existe. Trocar o nome pelo valor resolve os dois lados — e
    /// especializa a instanciação de quebra.
    ///
    /// Um nome de <b>tipo</b> atravessa intacto: definição de tipo não é valor
    /// conhecido no ambiente estático, então nunca cai neste caminho.
    /// </summary>
    private ImmutableArray<CoreGenericArgument> SpecializeArguments(
        ImmutableArray<CoreGenericArgument> arguments,
        SourceSpan span,
        StaticEnvironment environment)
    {
        var rewritten = ImmutableArray.CreateBuilder<CoreGenericArgument>(arguments.Length);
        var changed = false;

        foreach (var argument in arguments)
        {
            if (argument is CoreNameArgument name
                && environment.TryLookup(name.Name, out var binding)
                && binding.Value is Known known
                && Residualizer.CanResidualize(known.Value))
            {
                rewritten.Add(new CoreValueArgument(_residualizer.Residualize(known.Value, name.Span))
                {
                    Span = name.Span,
                });
                changed = true;
                continue;
            }

            rewritten.Add(argument);
        }

        return changed ? rewritten.MoveToImmutable() : arguments;
    }

    private PECompletion SpecializeConstruct(CoreConstruct node, StaticEnvironment environment)
    {
        var fields = ImmutableArray.CreateBuilder<CoreFieldInit>(node.Fields.Length);

        foreach (var field in node.Fields)
        {
            var specialized = Specialize(field.Value, environment);

            if (!specialized.FlowsThroughStatically)
            {
                return specialized;
            }

            fields.Add(field with { Value = _residualizer.Residualize(specialized.Result, field.Span) });
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.Construct(
                node.Span,
                node.TypeName,
                SpecializeArguments(node.TypeArguments, node.Span, environment),
                fields.MoveToImmutable(),
                node.TypeNameSpan),
            TypeOf(node)));
    }

    // -------------------------------------------------------------- match

    private PECompletion SpecializeMatch(CoreMatch node, StaticEnvironment environment)
    {
        var scrutinee = Specialize(node.Scrutinee, environment);

        if (!scrutinee.FlowsThroughStatically)
        {
            return scrutinee;
        }

        // Escrutinado conhecido: o braço é escolhido agora e os demais somem. Só
        // vale para padrões de literal e curinga — um valor conhecido nunca é um
        // enum construído (ver Residualizer.CanResidualize).
        if (scrutinee.Result is StaticResult value)
        {
            foreach (var arm in node.Arms)
            {
                if (TrySelect(arm.Pattern, value.Value, environment, out var inner))
                {
                    _branchesEliminated++;
                    return Specialize(arm.Body, inner);
                }
            }
        }

        var arms = ImmutableArray.CreateBuilder<CoreArm>(node.Arms.Length);
        var kind = PECompletionKind.Normal;
        var first = true;

        foreach (var arm in node.Arms)
        {
            var body = Specialize(arm.Body, WithPatternBindings(arm.Pattern, environment.Child()));

            kind = first ? body.Kind : PECompletion.Join(kind, body.Kind);
            first = false;

            arms.Add(arm with { Body = _residualizer.Residualize(body.Result, arm.Body.Span) });
        }

        var residual = _factory.Match(
            node.Span,
            _residualizer.Residualize(scrutinee.Result, node.Scrutinee.Span),
            arms.MoveToImmutable());

        return new PECompletion(kind, new DynamicResult(residual, TypeOf(node)));
    }

    /// <summary>
    /// <c>e is Variante(x)</c> (plano 25). Ramifica como <see cref="SpecializeIf"/>,
    /// mas <b>não dobra</b>: os dois ramos são especializados e o teste sobrevive.
    ///
    /// Não é conservadorismo escolhido — é que o PE nunca chega a conhecer um
    /// <c>EnumValue</c> hoje. Construir uma variante é uma chamada, e chamada é
    /// impura por conservadorismo (<see cref="Effects"/>); e um valor de enum
    /// tampouco sabe voltar a ser expressão (<c>Residualizer.CanResidualize</c>).
    /// É a mesma parede que <see cref="SpecializeMatch"/> encontra.
    ///
    /// Quando o M17–M19 ensinar o PE a avaliar construção de variante, dobrar o
    /// <c>is</c> passa a ser possível — e é aqui que entra: escolher o ramo pela
    /// etiqueta e, com ligação, propagar a carga adiante como valor conhecido.
    /// Escrevê-lo antes disso seria ramo morto que nenhum teste alcança.
    /// </summary>
    private PECompletion SpecializeIs(CoreIs node, StaticEnvironment environment)
    {
        var scrutinee = Specialize(node.Scrutinee, environment);

        if (!scrutinee.FlowsThroughStatically)
        {
            return scrutinee;
        }

        var thenBranch = Specialize(node.Then, environment.Child());
        var elseBranch = Specialize(node.Else, environment.Child());

        var residual = _factory.Is(
            node.Span,
            _residualizer.Residualize(scrutinee.Result, node.Scrutinee.Span),
            node.OwnerName,
            node.VariantName,
            node.BindingName,
            _residualizer.Residualize(thenBranch.Result, node.Then.Span),
            _residualizer.Residualize(elseBranch.Result, node.Else.Span),
            node.VariantSpan,
            node.BindingSpan);

        return new PECompletion(
            PECompletion.Join(thenBranch.Kind, elseBranch.Kind),
            new DynamicResult(residual, TypeOf(node)));
    }

    private static bool TrySelect(
        CorePattern pattern,
        Value value,
        StaticEnvironment environment,
        out StaticEnvironment inner)
    {
        switch (pattern)
        {
            case CoreWildcardPattern:
                inner = environment;
                return true;

            case CoreBindingPattern p:
                inner = environment.Extend(p.Name, new PEBinding(new Known(value)));
                return true;

            case CoreLiteralPattern p when Primitives.StructuralEquals(FromConstantValue(p.Value), value):
                inner = environment;
                return true;

            default:
                inner = environment;
                return false;
        }
    }

    private StaticEnvironment WithPatternBindings(CorePattern pattern, StaticEnvironment environment)
    {
        var result = environment;

        foreach (var name in BoundNames(pattern))
        {
            result = result.Extend(
                name,
                new PEBinding(new Unknown(AnyType.Instance), _factory.Variable(pattern.Span, name)));
        }

        return result;
    }

    private static IEnumerable<string> BoundNames(CorePattern pattern) => pattern switch
    {
        CoreBindingPattern p => [p.Name],
        CoreVariantPattern p => p.Arguments.SelectMany(BoundNames),
        _ => [],
    };

    // ------------------------------------------------- mutação e saltos

    /// <summary>
    /// Atribuição sempre sobrevive. Um <c>var</c> já entra no ambiente como
    /// desconhecido, então não há valor estático a invalidar aqui.
    /// </summary>
    private PECompletion SpecializeAssign(CoreAssign node, StaticEnvironment environment)
    {
        var value = Specialize(node.Value, environment);

        if (!value.FlowsThroughStatically)
        {
            return value;
        }

        return PECompletion.Normal(new DynamicResult(
            _factory.Assign(
                node.Span,
                node.Name,
                _residualizer.Residualize(value.Result, node.Value.Span),
                node.NameSpan,
                node.Path,
                node.PathSpans),
            PrimitiveType.Void));
    }

    /// <summary>
    /// O corpo é residualizado inteiro: especializar quantas vezes um <c>loop</c>
    /// itera exige <i>widening</i> ou combustível, e isso são os planos 17-19. O
    /// que se faz aqui é especializar as expressões dentro do corpo, que é seguro
    /// porque um <c>def</c> não muda entre voltas e um <c>var</c> já é
    /// desconhecido — mesmo raciocínio do <c>Labeled</c> que este nó substitui
    /// (plano 16, retirado no 26).
    /// </summary>
    private PECompletion SpecializeLoop(CoreLoop node, StaticEnvironment environment)
    {
        var body = Specialize(node.Body, environment.Child());

        var residual = _factory.Loop(
            node.Span,
            node.Label,
            _residualizer.Residualize(body.Result, node.Body.Span),
            node.LabelSpan);

        // Nunca `Returned`: um `break` pode desviar do `return` que o corpo
        // aparenta executar, então o retorno deste `loop` é sempre condicional —
        // mesma leitura conservadora que `Labeled` já tinha.
        return new PECompletion(
            body.Kind == PECompletionKind.Normal ? PECompletionKind.Normal : PECompletionKind.MayReturn,
            new DynamicResult(residual, TypeOf(node)));
    }

    /// <summary>
    /// A própria completion é sempre <c>Normal</c> — como <c>CoreGoto</c> antes
    /// dele (plano 16): o PE não modela "isto desvia", só "isto retorna". Quem
    /// protege a leitura conservadora é <see cref="SpecializeLoop"/>, que nunca
    /// deixa um grupo que contém um <c>break</c> declarar <c>Returned</c>.
    /// </summary>
    private PECompletion SpecializeBreak(CoreBreak node, StaticEnvironment environment)
    {
        if (node.Value is null)
        {
            return PECompletion.Normal(new DynamicResult(
                _factory.Break(node.Span, node.Label, null, node.LabelSpan), NeverType.Instance));
        }

        var value = Specialize(node.Value, environment);

        if (!value.FlowsThroughStatically)
        {
            return value;
        }

        var residual = _factory.Break(
            node.Span, node.Label, _residualizer.Residualize(value.Result, node.Value.Span), node.LabelSpan);

        return PECompletion.Normal(new DynamicResult(residual, NeverType.Instance));
    }

    // ----------------------------------------------------------- auxiliar

    /// <summary>
    /// Um valor só vira <see cref="StaticResult"/> se souber voltar a ser
    /// expressão. Normalizar aqui é o que permite ao resto do especializador tratar
    /// <c>Static</c> como "posso emitir isto" sem verificar de novo.
    /// </summary>
    /// <summary>
    /// Um valor conhecido vira resultado. Quando ele não tem forma sintática, o
    /// residual continua sendo a expressão original — mas o valor viaja junto em
    /// <see cref="DynamicResult.Opaque"/>, porque conhecê-lo ainda decide o que
    /// vem depois.
    /// </summary>
    private PEResult Reduce(Value value, CoreExpr original) =>
        Residualizer.CanResidualize(value)
            ? new StaticResult(value)
            : new DynamicResult(original, TypeOf(original)) { Opaque = value };

    /// <summary>O valor por trás de um resultado, quando o PE o conhece.</summary>
    private static Value? ValueOf(PEResult result) => result switch
    {
        StaticResult s => s.Value,
        DynamicResult { Opaque: { } value } => value,
        _ => null,
    };

    /// <summary>Nó que atravessa o PE intacto.</summary>
    private PEResult Keep(CoreExpr node) => new DynamicResult(node, TypeOf(node));

    private static PEResult FromConstant(ConstantValue constant) => new StaticResult(constant switch
    {
        ConstInt c => new IntValue(c.Value),
        ConstFloat c => new FloatValue(c.Value),
        ConstBool c => BoolValue.Of(c.Value),
        ConstStr c => new StrValue(c.Value),
        ConstUnit => VoidValue.Instance,
        _ => throw InternalCompilerException.Unreachable(constant),
    });

    private static Value FromConstantValue(ConstantValue constant) => ((StaticResult)FromConstant(constant)).Value;

    /// <summary>
    /// O tipo do nó, quando a tabela existe. <see cref="AnyType"/> quando não —
    /// o tipo aqui é informativo: o residual é re-checado do zero.
    /// </summary>
    private LapisType TypeOf(CoreExpr node)
    {
        if (_types is null)
        {
            return AnyType.Instance;
        }

        return _types.NodeTypes.TryGetValue(node.NodeId, out var type) ? type : AnyType.Instance;
    }

    /// <summary>
    /// O nome é redeclarado em algum ponto desta expressão? Substituir uma
    /// referência para dentro de um escopo que a redeclara capturaria o binding
    /// errado — o único jeito de a propagação mudar o significado do programa.
    /// </summary>
    private static bool Rebinds(CoreExpr expression, string name)
    {
        var finder = new RebindFinder(name);
        finder.Visit(expression);
        return finder.Found;
    }

    private sealed class RebindFinder(string name) : CoreWalker
    {
        public bool Found { get; private set; }

        protected override void OnNode(CoreExpr node)
        {
            Found |= node switch
            {
                CoreLet let => let.Name == name,
                CoreLambda lambda => lambda.Parameters.Any(p => p.Name == name)
                                     || lambda.TypeParameters.Any(p => p.Name == name),
                CoreMatch match => match.Arms.Any(a => BoundNames(a.Pattern).Contains(name)),
                CoreIs @is => @is.BindingName == name,
                _ => false,
            };
        }
    }

    private sealed class NodeCounter : CoreWalker
    {
        private int _count;

        public static int Count(CoreExpr expression)
        {
            var counter = new NodeCounter();
            counter.Visit(expression);
            return counter._count;
        }

        protected override void OnNode(CoreExpr node) => _count++;
    }
}

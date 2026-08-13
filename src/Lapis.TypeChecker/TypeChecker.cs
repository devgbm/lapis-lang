using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Core;
using Lapis.Ast.Printing;
using Lapis.Ast.Surface;
using Lapis.Ast.Typed;
using Lapis.Ast.Types;
using Lapis.Diagnostics;
using Lapis.Runtime;

namespace Lapis.TypeChecker;

/// <summary>
/// <c>Core AST → Typed Core AST</c> (spec §26).
///
/// Uma travessia única dirigida por sintaxe, com ambiente léxico. Sem unificação
/// global e sem geração de constraints: o objetivo é ser simples e previsível
/// (spec §47). Anotações são obrigatórias em parâmetros; tudo o mais flui de
/// baixo para cima.
/// </summary>
public sealed class TypeChecker
{
    /// <summary>O nome do intrínseco de reflection (plano 19 §19.2).</summary>
    public const string ReflectName = "reflect";

    private readonly DiagnosticBag _diagnostics;
    private readonly TypeResolver _types;
    private readonly Dictionary<int, LapisType> _nodeTypes = [];
    private readonly Dictionary<int, Resolution> _resolutions = [];
    private readonly Stack<FunctionContext> _functions = new();

    /// <summary>
    /// Pilha de <c>loop</c>s em checagem, do mais interno para o mais externo
    /// (plano 26, M16). Vive fora do <see cref="Scope"/> porque rótulos de loop
    /// são um espaço de nomes separado do de valores: <c>loop :x</c> e
    /// <c>def x</c> convivem.
    /// </summary>
    private Stack<LoopContext> _loops = new();

    /// <summary>
    /// Rótulos de loop de funções que envolvem a corrente. Não estão em escopo —
    /// um <c>break</c>/<c>continue</c> não atravessa fronteira de função —, mas
    /// saber que existem é o que separa "esse rótulo não existe" (<c>LAP0524</c>)
    /// de "esse rótulo é de outra função" (<c>LAP0525</c>).
    /// </summary>
    private readonly HashSet<string> _enclosingFunctionLoopLabels = new(StringComparer.Ordinal);

    /// <summary>
    /// Membros declarados por <c>def T.m</c>, indexados pela
    /// <see cref="TypeDefinition"/> e não pelo nome (plano 21 §21.5).
    ///
    /// Pela identidade, e não pelo nome, porque sombrear <c>Result</c> no programa
    /// do usuário não pode redirecionar os membros do <c>Result</c> do prelude —
    /// mesma razão de <see cref="PreludeScope"/> guardar a definição.
    ///
    /// O valor é uma <b>lista</b> porque o mesmo nome pode ter mais de uma
    /// declaração, com donos diferentes (plano 23 §23.4): <c>Result&lt;Int, ?&gt;.d</c>
    /// e <c>Result&lt;Bool, ?&gt;.d</c> convivem, e quem escolhe é o tipo do
    /// receptor. Padrões que se cruzam são <c>LAP0720</c> na declaração, então a
    /// lista nunca tem dois candidatos para o mesmo receptor.
    /// </summary>
    private readonly Dictionary<int, Dictionary<string, List<MemberInfo>>> _members = [];

    /// <summary>
    /// O tipo dono da declaração de membro que está sendo checada, enquanto o
    /// valor dela é checado. É o que permite a <c>fn(self)</c> receber um tipo
    /// sem que a Core carregue essa informação (plano 22 §22.1).
    ///
    /// Consumido pela **primeira** lambda que aparecer: uma `fn` aninhada no corpo
    /// de um membro não é membro.
    /// </summary>
    private MemberOwner? _pendingSelfOwner;

    /// <summary>
    /// O nome a que a próxima <c>CoreLambda</c> está sendo ligada, quando ela
    /// pode se referenciar (Q34). Consumido por <see cref="CheckLambdaBody"/>,
    /// que o declara no escopo do corpo assim que a assinatura existe.
    /// </summary>
    private string? _pendingSelfName;

    private PreludeScope? _prelude;

    /// <summary>
    /// Não-nulo quando o que está sendo checado é um <c>constraint</c>. É o que
    /// libera <c>throw</c> (<c>LAP0507</c> no resto) e o que põe as nativas de
    /// contexto em escopo.
    /// </summary>
    private CompileTimeScope? _compileTime;

    private int _nextBindingId;

    private TypeChecker(DiagnosticBag diagnostics)
    {
        _diagnostics = diagnostics;
        _types = new TypeResolver(diagnostics);
    }

    /// <param name="prelude">
    /// Definições do prelude. <c>null</c> apenas ao checar o próprio
    /// <c>prelude.ls</c>, que não usa indexação.
    /// </param>
    /// <param name="compileTime">
    /// Ambiente de compile time, quando o que se checa é um <c>constraint</c>
    /// (plano 18 §18.1). <c>null</c> é o caso normal — o programa do usuário —, e
    /// aí nem as nativas de contexto existem nem <c>throw</c> é permitido.
    /// </param>
    public static TypedProgram Check(
        CoreProgram program,
        PreludeScope? prelude,
        DiagnosticBag diagnostics,
        CompileTimeScope? compileTime = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var checker = new TypeChecker(diagnostics) { _prelude = prelude, _compileTime = compileTime };
        var scope = checker.CreateRootScope(prelude);

        checker.CheckExpression(program.Body, scope);

        return new TypedProgram(
            program,
            checker._nodeTypes.ToImmutableDictionary(),
            checker._resolutions.ToImmutableDictionary());
    }

    /// <summary>Escopo raiz: nativos (plano 09 §9.2) mais os bindings do prelude.</summary>
    private Scope CreateRootScope(PreludeScope? prelude)
    {
        var scope = Scope.Root();

        foreach (var native in Natives.All)
        {
            scope.Declare(new BindingInfo(
                NextBindingId(),
                native.Name,
                native.Signature,
                SourceSpan.Synthetic,
                BindingKind.Native));
        }

        foreach (var binding in prelude?.Bindings ?? [])
        {
            scope.Declare(new BindingInfo(
                NextBindingId(), binding.Name, binding.Type, SourceSpan.Synthetic, BindingKind.Native));
        }

        // As nativas de contexto só existem quando o que se checa é um
        // `constraint`. Num programa normal `contextPut` é um nome livre como
        // outro qualquer, e recebe o LAP0201 que merece.
        foreach (var binding in _compileTime?.Bindings ?? [])
        {
            scope.Declare(new BindingInfo(
                NextBindingId(), binding.Name, binding.Type, SourceSpan.Synthetic, BindingKind.Native));
        }

        // O programa do usuário roda num escopo filho: sombrear `Result` é
        // permitido e não muda a semântica de `[]` (plano 09 §9.4).
        return scope.Child();
    }

    // ------------------------------------------------------------ despacho

    /// <param name="expected">
    /// Tipo que o contexto exige, quando há. É a <b>única</b> informação que flui
    /// de cima para baixo neste checker (§47), e existe por um motivo estreito:
    /// <c>[]</c> não tem como dizer sozinho o que carrega (spec §18).
    /// </param>
    private LapisType CheckExpression(CoreExpr node, Scope scope, LapisType? expected = null)
    {
        var type = node switch
        {
            CoreLiteral n => n.Value.Type,
            CoreVariable n => CheckVariable(n, scope),
            CoreLet n => CheckLet(n, scope),
            CoreLambda n => CheckLambda(n, scope),
            CoreCall n => CheckCall(n, scope),
            CoreInstantiate n => CheckInstantiate(n, scope),
            CoreReturn n => CheckReturn(n, scope),
            CoreThrow n => CheckThrow(n, scope),
            CoreIf n => CheckIf(n, scope),
            CoreBinary n => CheckBinary(n, scope),
            CoreUnary n => CheckUnary(n, scope),
            CoreSpan n => CheckArray(n, scope, expected),
            CoreSpanRepeat n => CheckSpanRepeat(n, scope),
            CoreIndex n => CheckIndex(n, scope),
            CoreField n => CheckField(n, scope),
            CoreEnumDef n => CheckEnumDef(n, scope),
            CoreMatch n => CheckMatch(n, scope),
            CoreIs n => CheckIs(n, scope),
            CoreTypeDef n => CheckTypeDef(n, scope),
            CoreConstruct n => CheckConstruct(n, scope),
            CoreLoop n => CheckLoop(n, scope),
            CoreBreak n => CheckBreak(n, scope),
            CoreContinue n => CheckContinue(n),
            CoreAssign n => CheckAssign(n, scope),
            _ => throw InternalCompilerException.Unreachable(node, node.Span),
        };

        _nodeTypes[node.NodeId] = type;
        return type;
    }

    // ------------------------------------------------------------- nomes

    private LapisType CheckVariable(CoreVariable node, Scope scope)
    {
        if (scope.TryLookup(node.Name, out var binding))
        {
            ReportIfCrossesFunction(binding, node.Span);
            _resolutions[node.NodeId] = new VariableResolution(binding.Id);
            return binding.Type;
        }

        var notes = FindSuggestion(node.Name, scope) is { } suggestion
            ? new[] { new DiagnosticNote($"você quis dizer '{suggestion}'?") }
            : [];

        _diagnostics.ReportError(
            DiagnosticCodes.UnknownVariable, node.Span, $"variável '{node.Name}' não existe", notes);

        return ErrorType.Instance;
    }

    private LapisType CheckLet(CoreLet node, Scope scope)
    {
        // A anotação é resolvida antes do valor para poder descer até ele. Sem
        // isso `def a: Int[] = [];` falharia — e a mensagem de LAP0241 mandaria
        // fazer exatamente o que acabou de não funcionar.
        var declared = node.Annotation is null ? null : _types.Resolve(node.Annotation, scope);

        // `def T.m = fn(self) ...` — o dono precisa estar em mãos **antes** de o
        // valor ser checado, porque é dele que o `self` tira o tipo. É também a
        // única resolução do dono: `RegisterMember` reaproveita o resultado, para
        // que um dono inválido não vire dois diagnósticos.
        var owner = OwnerOf(node, scope);

        var previousSelfOwner = _pendingSelfOwner;
        _pendingSelfOwner = owner;

        // Recursão (Q34): o corpo pode citar o próprio nome. Só um `def` cujo
        // valor é **sintaticamente** uma lambda — de onde a assinatura sai sem
        // inferência (Q7 intacta), porque parâmetro é anotado por obrigação
        // (§26) e retorno omitido é `Void`.
        //
        // `def x = x + 1;` continua LAP0201, e deve continuar: não há assinatura
        // de onde tirar tipo. A Q8 estava certa sobre esse caso.
        var previousSelfName = _pendingSelfName;
        _pendingSelfName = !node.IsMutable && !node.IsSynthetic && node.Value is CoreLambda
            ? node.Name
            : null;

        LapisType valueType;

        try
        {
            valueType = CheckExpression(node.Value, scope, declared);
        }
        finally
        {
            _pendingSelfOwner = previousSelfOwner;
            _pendingSelfName = previousSelfName;
        }

        // Um `type`/`enum` não tem nome próprio (spec §14, §15): ele recebe o nome
        // do `def` que o liga, e é esse nome que aparece em diagnósticos e na
        // formatação de valores (`Result.Ok(20)`).
        if (!node.IsSynthetic && valueType is MetaType meta && meta.Definition.Name == "<anônimo>")
        {
            meta.Definition.Name = node.Name;
        }

        if (declared is not null)
        {
            if (!TypeRelations.IsAssignableTo(valueType, declared))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.TypeMismatch,
                    node.Value.Span,
                    $"esperado {declared.ToDisplayString()}, encontrado {valueType.ToDisplayString()}");
            }

            valueType = declared;
        }
        else if (node.IsMutable && valueType is SpanType { Size: FixedSize } widened)
        {
            // Um `var` sem anotação **alarga** o tamanho para `?` (plano 24 §24.7):
            // `s = .[1,2,3,4,5]` tem de continuar válido, e o tipo não pode
            // prometer um tamanho que a próxima atribuição desmente.
            //
            // Com anotação (`var f: [Int;3]`) o tamanho é mantido, e a
            // reatribuição passa a ser checada — é o que dá indexação total num
            // binding mutável.
            valueType = SpanType.Unknown(widened.Element);
        }

        // `def T.m` — o desugar já nomeou; aqui o membro entra na tabela do tipo
        // dono, que é onde `CheckField` vai procurá-lo (plano 21 §21.5).
        if (owner is { IsWellFormed: true } && MemberNames.Split(node.Name) is { } member)
        {
            RegisterMember(owner, member.Member, node, valueType);
        }

        // O nome só é visível no corpo — não no próprio valor. É isso que torna a
        // v0.2 não recursiva (Q8).
        //
        // Redefinição no mesmo bloco (LAP0202) é detectada no desugar, onde os
        // blocos ainda existem; aqui um `Let` interno sempre sombreia legitimamente.
        var inner = scope.Child();

        inner.Declare(new BindingInfo(
            NextBindingId(),
            node.Name,
            valueType,
            node.NameSpan,
            node.IsMutable ? BindingKind.Variable : BindingKind.Value)
        {
            // Um `var` nunca é constante de compilação: o valor de hoje não é o de
            // amanhã. É o que faz `Somefn<umVar>()` cair em LAP0294, como a Q18 exige.
            Constant = node.IsMutable ? null : ConstantOf(node.Value, valueType, scope),
            FunctionDepth = _functions.Count,
        });

        var valueReturns = ReturnAnalysis.DefinitelyReturns(node.Value);

        // Código após um `return` no mesmo encadeamento é inalcançável. Sem
        // `goto`/`label` (plano 26, Q32) não há mais exceção a fazer aqui: a
        // cadeia de `Let` é exatamente o que foi escrito, sem salto implícito
        // nenhum a descontar.
        if (valueReturns && node.Body is not CoreLiteral { Value: ConstUnit })
        {
            _diagnostics.ReportWarning(
                DiagnosticCodes.UnreachableAfterReturn, node.Body.Span, "código inalcançável após 'return'");
        }

        var bodyType = CheckExpression(node.Body, inner);

        // Se o valor sempre retorna, o corpo é inalcançável e o `Let` inteiro
        // diverge. Sem isso, `{ return 0; }` teria tipo Void (a cauda sintética)
        // em vez de Never, e não casaria com o outro ramo de um `if`.
        return valueReturns ? NeverType.Instance : bodyType;
    }

    /// <summary>
    /// O valor de um <c>def</c>, quando conhecido em tempo de compilação (Q18).
    ///
    /// A linha é deliberadamente reta: um literal, uma função literal, ou outro
    /// <c>def</c> que já carregue uma constante. Isto é <b>propagação</b>, não
    /// <i>folding</i> — <c>def n = 1 + 2;</c> não produz constante, porque dobrar
    /// a expressão é trabalho do partial evaluator (spec §58) e replicá-lo no
    /// checker significaria manter duas aritméticas em sincronia.
    /// </summary>
    private GenericArgument? ConstantOf(CoreExpr value, LapisType type, Scope scope) => value switch
    {
        CoreLiteral literal => new ConstArgument(literal.Value),

        // `s.length` sobre um `[T;N]` é a constante `N`, e por isso serve de
        // argumento const genérico (Q18).
        CoreField field when _resolutions.TryGetValue(field.NodeId, out var resolution)
            && resolution is SpanLengthResolution { Known: { } known } =>
            new ConstArgument(new ConstInt(known)),

        CoreLambda lambda when type is FunctionType signature =>
            new ConstFunctionArgument(CoreSourcePrinter.PrintExpressionCompact(lambda), signature),

        CoreVariable variable when scope.TryLookup(variable.Name, out var binding) => binding.Constant,

        _ => null,
    };

    /// <summary>
    /// O índice como constante, quando dá — literal escrito, ou um <c>def</c>
    /// ligado a um. É o que separa a indexação total da que devolve
    /// <c>Option</c> (plano 24 §24.5).
    ///
    /// Um <c>var</c> nunca chega aqui: ele não é constante de compilação (Q25), e
    /// é por isso que trocar <c>def i</c> por <c>var i</c> muda o tipo de
    /// <c>s[i]</c>.
    /// </summary>
    private static long? ConstIntOf(CoreExpr expression, Scope scope) => expression switch
    {
        CoreLiteral { Value: ConstInt literal } => literal.Value,

        CoreVariable variable when scope.TryLookup(variable.Name, out var binding)
            && binding.Constant is ConstArgument { Value: ConstInt bound } => bound.Value,

        _ => null,
    };

    // --------------------------------------------------------- funções

    /// <summary>
    /// O tipo de <c>self</c>: o dono do membro que está sendo declarado.
    ///
    /// A ausência de anotação é o gatilho, e ela é estreita de propósito — fora
    /// da primeira posição de um <c>def T.m</c>, um parâmetro sem anotação é
    /// <c>LAP0712</c>, porque não há de onde tirar o tipo.
    /// </summary>
    private LapisType ResolveSelf(CoreParameter parameter, int index, MemberOwner? owner)
    {
        if (owner is null || index != 0 || parameter.Name != MemberNames.Self)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.SelfOutsideMember,
                parameter.Span,
                $"o parâmetro '{parameter.Name}' requer anotação de tipo",
                new DiagnosticNote(
                    "só o primeiro parâmetro de um 'def T.m', chamado 'self', dispensa anotação"));

            return ErrorType.Instance;
        }

        // Sobre um dono genérico, `self` carrega o **padrão** escrito (plano 23
        // §23.7): `def Result<Int, ?>.m` dá `self: Result<Int, ?>`. A consequência
        // cai de graça da regra de atribuibilidade — o corpo só consegue fazer com
        // `self` o que não depende do argumento curinga, e `self.value` sobre
        // `Result<?, ?>` é erro de campo, que é a verdade.
        return new NamedType(owner.Definition, owner.Pattern);
    }

    /// <summary>
    /// O dono de uma declaração de membro: a definição e o alcance escrito
    /// (plano 23 §23.4). O padrão é vazio quando o dono não é genérico.
    /// </summary>
    /// <param name="IsWellFormed">
    /// Falso quando o padrão não se sustentou e foi substituído por curingas para
    /// seguir checando o corpo. O membro <b>não</b> entra na tabela — mas o
    /// <c>self</c> ainda recebe um tipo, e é isso que evita um <c>LAP0712</c> em
    /// cascata atrás de cada erro de dono.
    /// </param>
    private sealed record MemberOwner(
        TypeDefinition Definition,
        ImmutableArray<GenericArgument> Pattern,
        bool IsWellFormed = true);

    private LapisType CheckLambda(CoreLambda node, Scope scope)
    {
        var inner = scope.Child();
        var generics = DeclareTypeParameters(node.TypeParameters, inner, declareConstValues: true);

        try
        {
            return CheckLambdaBody(node, inner, generics.Parameters);
        }
        finally
        {
            _types.ExitTypeParameters(generics.Shadowed);
        }
    }

    private LapisType CheckLambdaBody(
        CoreLambda node,
        Scope inner,
        ImmutableArray<GenericParameter> typeParameters)
    {
        var parameterTypes = ImmutableArray.CreateBuilder<LapisType>(node.Parameters.Length);

        // O dono só vale para **esta** função: uma `fn` aninhada no corpo de um
        // membro não é membro, e o `self` dela não tem de onde vir.
        var owner = _pendingSelfOwner;
        _pendingSelfOwner = null;

        // Idem para o nome próprio: uma `fn` aninhada não é a função recursiva.
        var selfName = _pendingSelfName;
        _pendingSelfName = null;

        for (var i = 0; i < node.Parameters.Length; i++)
        {
            var parameter = node.Parameters[i];

            var type = parameter.Type is null
                ? ResolveSelf(parameter, i, owner)
                : _types.Resolve(parameter.Type, inner);

            parameterTypes.Add(type);

            if (inner.TryLookupLocal(parameter.Name, out var existing))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.DuplicateDefinition,
                    parameter.Span,
                    $"'{parameter.Name}' já foi definido neste escopo",
                    new DiagnosticNote("definição anterior", existing.Span));
            }

            inner.Declare(new BindingInfo(
                NextBindingId(), parameter.Name, type, parameter.Span, BindingKind.Parameter));
        }

        var returnType = _types.Resolve(node.ReturnType, inner);
        var signature = new FunctionType(parameterTypes.ToImmutable(), returnType, typeParameters);

        // O nome próprio entra **depois** dos parâmetros e só se nenhum deles já o
        // ocupou: parâmetro homônimo sombreia a função, que é a regra usual — e é
        // a mesma ordem que o evaluator aplica ao religar `SelfName`.
        if (selfName is not null && !inner.TryLookupLocal(selfName, out _))
        {
            inner.Declare(new BindingInfo(
                NextBindingId(), selfName, signature, node.Span, BindingKind.Value)
            {
                FunctionDepth = _functions.Count + 1,
            });
        }

        _functions.Push(new FunctionContext(returnType));

        // Um rótulo de loop é local à função, como `return`: os loops de fora
        // saem de escopo aqui e só voltam quando o corpo termina. Só rótulos
        // **nomeados** entram em `_enclosingFunctionLoopLabels` — um loop sem
        // rótulo não tem como ser referenciado de dentro da função aninhada.
        var outerLoops = _loops;
        var hidden = outerLoops
            .Where(l => l.Label is not null)
            .Select(l => l.Label!)
            .Where(_enclosingFunctionLoopLabels.Add)
            .ToList();
        _loops = new Stack<LoopContext>();

        try
        {
            CheckExpression(node.Body, inner);
        }
        finally
        {
            _functions.Pop();
            _loops = outerLoops;
            _enclosingFunctionLoopLabels.ExceptWith(hidden);
        }

        // Spec §12: função Void pode cair no fim do corpo; as demais, não.
        var isVoid = returnType is PrimitiveType { Kind: PrimitiveKind.Void };

        if (!isVoid && returnType is not ErrorType && !ReturnAnalysis.DefinitelyReturns(node.Body))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.MissingReturn,
                node.BodyEndSpan,
                "nem todos os caminhos de execução retornam um valor",
                new DiagnosticNote($"a função declara retorno {returnType.ToDisplayString()}"));
        }

        return signature;
    }

    /// <summary>
    /// Traz os parâmetros genéricos de uma declaração para escopo (Q1). Os de tipo
    /// entram como <see cref="TypeParameterType"/>; os const entram <b>também</b>
    /// como valores, porque dentro do corpo <c>N</c> é um valor do tipo declarado
    /// (spec §13) — quem sabe qual valor é o partial evaluator, não o checker.
    /// </summary>
    private GenericScope DeclareTypeParameters(
        ImmutableArray<CoreTypeParameter> declared,
        Scope scope,
        bool declareConstValues)
    {
        if (declared.IsDefaultOrEmpty)
        {
            return new GenericScope([], []);
        }

        var parameters = ImmutableArray.CreateBuilder<GenericParameter>(declared.Length);

        var shadowed = _types.EnterTypeParameters(
            declared.Where(p => !p.IsConst).Select(p => p.Name));

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var parameter in declared)
        {
            if (!seen.Add(parameter.Name))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.DuplicateDefinition,
                    parameter.Span,
                    $"parâmetro genérico '{parameter.Name}' declarado mais de uma vez");
            }

            if (parameter.ConstType is null)
            {
                parameters.Add(GenericParameter.OfType(parameter.Name));
                continue;
            }

            var constType = _types.Resolve(parameter.ConstType, scope);
            parameters.Add(new GenericParameter(parameter.Name, constType));

            if (declareConstValues)
            {
                // O parâmetro é um valor no corpo — e um valor *constante* (Q18):
                // parâmetros const só recebem argumentos conhecidos em compilação,
                // então repassá-lo adiante é legítimo. O valor em si é simbólico
                // até a instanciação de fora fechá-lo.
                scope.Declare(new BindingInfo(
                    NextBindingId(), parameter.Name, constType, parameter.Span, BindingKind.Parameter)
                {
                    Constant = new ConstParameterArgument(parameter.Name, constType),
                });
            }
        }

        return new GenericScope(parameters.ToImmutable(), shadowed);
    }

    /// <summary>Os parâmetros declarados e os nomes que eles sombrearam.</summary>
    private sealed record GenericScope(
        ImmutableArray<GenericParameter> Parameters,
        Dictionary<string, TypeParameterType?> Shadowed);

    /// <summary>
    /// <c>alvo&lt;A, B&gt;</c> — instanciação explícita (Q7). O checker apenas
    /// <b>substitui</b>: não monomorfiza corpo nenhum. Especializar corpos é
    /// trabalho do partial evaluator, e essa divisão é o objeto de pesquisa do
    /// projeto (plano 06 §6.8).
    /// </summary>
    private LapisType CheckInstantiate(CoreInstantiate node, Scope scope)
    {
        var target = CheckExpression(node.Target, scope);
        var raw = node.Arguments.Select(a => ReadGenericArgument(a, scope)).ToList();

        switch (target)
        {
            case ErrorType or NeverType:
                return target;

            // `Result<Int, IndexError>` — um tipo genérico aplicado, do qual `.Ok`
            // extrai um construtor já instanciado.
            case MetaType meta when meta.Arguments.IsDefaultOrEmpty:
                {
                    var definition = meta.Definition;
                    var arguments = GenericArguments.Resolve(
                        _diagnostics, definition.Name, definition.TypeParameters, raw, node.Span);

                    return arguments is null
                        ? ErrorType.Instance
                        : new MetaType(definition, arguments.Value);
                }

            case FunctionType signature when signature.IsGeneric:
                {
                    var arguments = GenericArguments.Resolve(
                        _diagnostics, DescribeCallee(node.Target), signature.TypeParameters, raw, node.Span);

                    if (arguments is null)
                    {
                        return ErrorType.Instance;
                    }

                    _resolutions[node.NodeId] = new InstantiateResolution(
                        signature.TypeParameters, arguments.Value);

                    return Instantiate(signature, arguments.Value);
                }

            default:
                _diagnostics.ReportError(
                    DiagnosticCodes.GenericArityMismatch,
                    node.Span,
                    $"{target.ToDisplayString()} não é genérico e não aceita argumentos genéricos");

                return ErrorType.Instance;
        }
    }

    /// <summary>Substitui os parâmetros de tipo pelos argumentos e apaga a genericidade.</summary>
    private static FunctionType Instantiate(FunctionType signature, ImmutableArray<GenericArgument> arguments)
    {
        var bindings = BuildSubstitution(signature.TypeParameters, arguments);

        return new FunctionType(
            [.. signature.Parameters.Select(p => TypeSubstitution.Apply(p, bindings))],
            TypeSubstitution.Apply(signature.Return, bindings),
            []);
    }

    /// <summary>
    /// Lê um argumento genérico em <b>posição de expressão</b>, onde uma função
    /// literal é um argumento legítimo (spec §13).
    /// </summary>
    private RawGenericArgument ReadGenericArgument(CoreGenericArgument argument, Scope scope)
    {
        switch (argument)
        {
            case CoreTypeArgument a:
                return RawGenericArgument.OfType(_types.Resolve(a.Type, scope), a.Span);

            case CoreNameArgument a:
                return ReadNameArgument(a, scope);

            case CoreValueArgument a:
                var type = CheckExpression(a.Value, scope);

                return a.Value switch
                {
                    CoreLiteral literal => RawGenericArgument.OfConstant(
                        new ConstArgument(literal.Value), a.Span),

                    CoreLambda lambda when type is FunctionType signature =>
                        RawGenericArgument.OfConstant(
                            new ConstFunctionArgument(CoreSourcePrinter.PrintExpressionCompact(lambda), signature),
                            a.Span),

                    _ => RawGenericArgument.RuntimeValue(a.Span),
                };

            default:
                throw new InternalCompilerException(
                    $"argumento genérico inesperado: {argument.GetType().Name}", argument.Span);
        }
    }

    private RawGenericArgument ReadNameArgument(CoreNameArgument argument, Scope scope)
    {
        if (TypeResolver.IsPrimitiveName(argument.Name) || _types.IsTypeParameter(argument.Name))
        {
            return RawGenericArgument.OfType(
                _types.Resolve(NamedTypeSyntax.Of(argument.Name, argument.Span), scope), argument.Span);
        }

        if (!scope.TryLookup(argument.Name, out var binding))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownVariable, argument.Span, $"'{argument.Name}' não existe");

            return RawGenericArgument.Error(argument.Span);
        }

        if (binding.Type is MetaType meta)
        {
            return RawGenericArgument.OfType(new NamedType(meta.Definition, meta.Arguments), argument.Span);
        }

        // Um `def` ligado a um literal é constante e serve de argumento (Q18). Um
        // parâmetro nunca é — inclusive o de um `fn<N: Int>`, cujo valor só aparece
        // quando o PE especializa a chamada de fora.
        return binding.Constant is { } constant
            ? RawGenericArgument.OfConstant(constant, argument.Span)
            : RawGenericArgument.RuntimeValue(argument.Span);
    }

    private static string DescribeCallee(CoreExpr callee) => callee switch
    {
        CoreVariable v => v.Name,
        CoreField f => f.Name,
        _ => "a função",
    };

    private LapisType CheckReturn(CoreReturn node, Scope scope)
    {
        if (_functions.Count == 0)
        {
            if (node.Value is not null)
            {
                CheckExpression(node.Value, scope);
            }

            _diagnostics.ReportError(
                DiagnosticCodes.ReturnOutsideFunction, node.Span, "'return' fora de uma função");

            return NeverType.Instance;
        }

        var expected = _functions.Peek().ReturnType;

        if (node.Value is null)
        {
            if (expected is not PrimitiveType { Kind: PrimitiveKind.Void } and not ErrorType)
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.EmptyReturnInNonVoid,
                    node.Span,
                    $"'return;' sem expressão em função que retorna {expected.ToDisplayString()}");
            }

            return NeverType.Instance;
        }

        var actual = CheckExpression(node.Value, scope);

        if (!TypeRelations.IsAssignableTo(actual, expected))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.ReturnTypeMismatch,
                node.Value.Span,
                $"'return' de {actual.ToDisplayString()} em função que retorna {expected.ToDisplayString()}");
        }

        // `return` é uma expressão de tipo bottom: cabe em qualquer posição (Q13).
        return NeverType.Instance;
    }

    /// <summary>
    /// <c>throw e</c> (spec de macros §8.2).
    ///
    /// Duas exigências, e as duas na mesma travessia: existir só em compile time
    /// (<c>LAP0507</c>) e carregar um <c>Str</c> (<c>LAP0508</c>). O valor é
    /// checado nos dois casos — mesmo fora de um <c>constraint</c>, um erro dentro
    /// dele continua sendo um erro que vale reportar.
    /// </summary>
    private LapisType CheckThrow(CoreThrow node, Scope scope)
    {
        var actual = CheckExpression(node.Value, scope);

        if (_compileTime is null)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.ThrowOutsideConstraint,
                node.Span,
                "'throw' só é válido dentro de 'constraint'",
                new DiagnosticNote(
                    "a linguagem não tem exceções de runtime; um erro esperado se representa com 'Result'"));
        }
        else if (actual is not PrimitiveType { Kind: PrimitiveKind.Str } and not ErrorType and not NeverType)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.ThrowExpectsStr,
                node.Value.Span,
                $"'throw' espera Str, encontrado {actual.ToDisplayString()}");
        }

        // Como `return`, `throw` tem tipo bottom: ele nunca produz valor (Q13).
        return NeverType.Instance;
    }

    private LapisType CheckCall(CoreCall node, Scope scope)
    {
        if (IsReflectIntrinsic(node.Callee, scope))
        {
            return CheckReflect(node, scope);
        }

        var calleeType = CheckExpression(node.Callee, scope);

        var argumentTypes = ImmutableArray.CreateBuilder<LapisType>(node.Arguments.Length);

        foreach (var argument in node.Arguments)
        {
            argumentTypes.Add(CheckExpression(argument, scope));
        }

        if (calleeType is ErrorType)
        {
            return ErrorType.Instance;
        }

        if (calleeType is NeverType || argumentTypes.Any(t => t is NeverType))
        {
            return NeverType.Instance;
        }

        if (calleeType is not FunctionType signature)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.NotCallable,
                node.Callee.Span,
                $"{calleeType.ToDisplayString()} não é uma função e não pode ser chamado");
            return ErrorType.Instance;
        }

        var arguments = argumentTypes.ToImmutable();

        // Q7: argumentos genéricos são sempre explícitos, nunca inferidos. Uma
        // assinatura ainda genérica aqui significa que a chamada não passou pelo
        // `<...>` — não há de onde deduzir os argumentos.
        if (signature.IsGeneric)
        {
            var names = string.Join(", ", signature.TypeParameters.Select(p => p.Name));

            _diagnostics.ReportError(
                DiagnosticCodes.GenericArityMismatch,
                node.Span,
                $"a função espera {signature.TypeParameters.Length} argumentos genéricos explícitos",
                new DiagnosticNote($"não há inferência: escreva '<{names}>' antes de '('"));

            return ErrorType.Instance;
        }

        var instantiated = signature;

        // Os argumentos genéricos, quando há, ficam no `Instantiate` que produziu
        // este callee — o `CallResolution` os repete para quem só olha a chamada.
        var typeArguments = node.Callee is CoreInstantiate callee
            && _resolutions.TryGetValue(callee.NodeId, out var resolution)
            && resolution is InstantiateResolution instantiation
                ? instantiation.Arguments
                : ImmutableArray<GenericArgument>.Empty;

        // `user.hello(x)` — o **receptor entra como argumento 0** (plano 22 §22.2).
        // A aridade e os tipos são checados com ele já na posição, então
        // `user.rename("x")` compara contra `fn(User, Str) Void`.
        var receiver = ReceiverOf(node);

        _resolutions[node.NodeId] = new CallResolution(typeArguments, instantiated)
        {
            Receiver = receiver,
        };

        var written = arguments.Length + (receiver is null ? 0 : 1);

        if (instantiated.Parameters.Length != written)
        {
            // A contagem inclui o receptor; a mensagem desconta, senão "esperados
            // 2 argumentos, fornecidos 1" seria mentira para quem escreveu um.
            var offset = receiver is null ? 0 : 1;

            _diagnostics.ReportError(
                DiagnosticCodes.ArgumentCountMismatch,
                node.Span,
                $"esperados {instantiated.Parameters.Length - offset} argumentos, "
                + $"fornecidos {arguments.Length}");

            return instantiated.Return;
        }

        if (receiver is { } self
            && !TypeRelations.IsAssignableTo(self.Type, instantiated.Parameters[0]))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.ArgumentTypeMismatch,
                self.Span,
                $"receptor: esperado {instantiated.Parameters[0].ToDisplayString()}, "
                + $"encontrado {self.Type.ToDisplayString()}");
        }

        var start = receiver is null ? 0 : 1;

        for (var i = 0; i < arguments.Length; i++)
        {
            if (!TypeRelations.IsAssignableTo(arguments[i], instantiated.Parameters[start + i]))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.ArgumentTypeMismatch,
                    node.Arguments[i].Span,
                    $"argumento {i + 1}: esperado {instantiated.Parameters[start + i].ToDisplayString()}, "
                    + $"encontrado {arguments[i].ToDisplayString()}");
            }
        }

        return instantiated.Return;
    }

    /// <summary>
    /// O receptor de uma chamada por instância, ou <c>null</c> quando a chamada é
    /// comum.
    ///
    /// O callee é um <c>CoreField</c> resolvido como membro de instância; o alvo
    /// dele é o receptor, e o tipo já foi calculado quando o campo foi checado.
    /// </summary>
    private CallReceiver? ReceiverOf(CoreCall node) =>
        node.Callee is CoreField field
        && _resolutions.TryGetValue(field.NodeId, out var resolved)
        && resolved is MemberResolution { Kind: MemberAccessKind.InstanceMethod }
        && _nodeTypes.TryGetValue(field.Target.NodeId, out var receiverType)
            ? new CallReceiver(field.Target, receiverType, field.Target.Span)
            : null;

    // ---------------------------------------------------------- reflection

    /// <summary>
    /// <c>reflect</c> é um <b>intrínseco</b>, não um binding (plano 19 §19.2).
    ///
    /// Precisa ser: o argumento tem de ser um tipo, e "um tipo" não é expressável
    /// na gramática de tipos — não há como escrever a assinatura de <c>reflect</c>
    /// em LapisLang. É o mesmo estatuto da indexação, que produz
    /// <c>Result&lt;T, IndexError&gt;</c> sem existir um <c>fn(T[], Int) Result</c>
    /// escrito em lugar nenhum (spec §21).
    ///
    /// Um binding do usuário chamado <c>reflect</c> <b>vence</b>: quem escreve
    /// <c>def reflect = fn(x: Int) Int { ... }</c> quis a sua função, e sombrear é
    /// permitido em toda parte (plano 09 §9.4).
    /// </summary>
    private static bool IsReflectIntrinsic(CoreExpr callee, Scope scope) =>
        callee is CoreVariable { Name: ReflectName } && !scope.TryLookup(ReflectName, out _);

    private LapisType CheckReflect(CoreCall node, Scope scope)
    {
        if (node.Arguments.Length != 1)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.ArgumentCountMismatch,
                node.Span,
                $"esperado 1 argumento, fornecidos {node.Arguments.Length}");

            return ErrorType.Instance;
        }

        var argument = node.Arguments[0];

        // Em compile time, os tipos do **programa** não estão em escopo — o
        // checker ainda não rodou sobre ele. O que existe é o nome, e os
        // metadados saem da tabela de declarações (§19.3). Tipos do prelude
        // (`Result`, `Option`) continuam pelo caminho normal, porque esses já
        // estão resolvidos.
        if (_compileTime is not null
            && argument is CoreVariable variable
            && !scope.TryLookup(variable.Name, out _))
        {
            if (!_compileTime.Declarations.ContainsKey(variable.Name))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.TypeNotDeclaredYet,
                    argument.Span,
                    $"o tipo '{variable.Name}' não foi declarado neste ponto",
                    new DiagnosticNote("uma macro só enxerga o que já foi declarado acima dela (Q8)"));

                return ErrorType.Instance;
            }

            _nodeTypes[argument.NodeId] = _prelude!.TypeInfoType;
            _resolutions[node.NodeId] = new ReflectResolution(null, variable.Name);

            return _prelude.TypeInfoType;
        }

        var argumentType = CheckExpression(argument, scope);

        if (argumentType is ErrorType or NeverType)
        {
            return argumentType;
        }

        // Um `MetaType` genérico **não instanciado** é aceito de propósito:
        // `reflect(Result)` descreve a declaração, com `typeParameterNames`
        // preenchido. É o que uma constraint precisa — nomes e aridade, não os
        // argumentos de uma instância.
        if (argumentType is not MetaType meta)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.ReflectExpectsType,
                argument.Span,
                $"'reflect' espera um tipo, encontrado {argumentType.ToDisplayString()}");

            return ErrorType.Instance;
        }

        _resolutions[node.NodeId] = new ReflectResolution(meta.Definition, Arguments: meta.Arguments);

        return RequirePrelude().TypeInfoType;
    }

    private PreludeScope RequirePrelude() =>
        _prelude ?? throw new InternalCompilerException("'reflect' exige o prelude carregado");

    // --------------------------------------------------- controle e operadores

    private LapisType CheckIf(CoreIf node, Scope scope)
    {
        var conditionType = CheckExpression(node.Condition, scope);

        if (conditionType is not PrimitiveType { Kind: PrimitiveKind.Bool }
            and not ErrorType and not NeverType)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.ConditionMustBeBool,
                node.Condition.Span,
                $"condição de 'if' deve ser Bool, encontrado {conditionType.ToDisplayString()}");
        }

        var thenType = CheckExpression(node.Then, scope.Child());
        var elseType = CheckExpression(node.Else, scope.Child());

        if (conditionType is NeverType)
        {
            return NeverType.Instance;
        }

        var joined = TypeRelations.Join(thenType, elseType);

        if (joined is null)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.IncompatibleBranches,
                node.Span,
                $"ramos de 'if' têm tipos incompatíveis: {thenType.ToDisplayString()} "
                + $"e {elseType.ToDisplayString()}");
            return ErrorType.Instance;
        }

        return joined;
    }

    // ----------------------------------------------------------- mutação

    /// <summary>
    /// <c>x = e</c>. Tipo <c>Void</c>: a atribuição não produz valor.
    /// </summary>
    private LapisType CheckAssign(CoreAssign node, Scope scope)
    {
        var valueType = CheckExpression(node.Value, scope);

        if (!scope.TryLookup(node.Name, out var binding))
        {
            var notes = FindSuggestion(node.Name, scope) is { } suggestion
                ? new[] { new DiagnosticNote($"você quis dizer '{suggestion}'?") }
                : [];

            _diagnostics.ReportError(
                DiagnosticCodes.UnknownVariable, node.NameSpan, $"variável '{node.Name}' não existe", notes);

            return PrimitiveType.Void;
        }

        _resolutions[node.NodeId] = new VariableResolution(binding.Id);

        if (!binding.IsMutable)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.NotAssignable,
                node.NameSpan,
                $"não é possível atribuir a '{node.Name}'",
                new DiagnosticNote("só um 'var' pode ser reatribuído; este é um 'def'", binding.Span));

            return PrimitiveType.Void;
        }

        if (ReportIfCrossesFunction(binding, node.NameSpan))
        {
            return PrimitiveType.Void;
        }

        // `u.endereco.rua = e;` — o caminho é percorrido pelos tipos, e o que
        // precisa caber é o **último** campo (plano 21 §21.3b).
        var target = binding.Type;

        for (var i = 0; i < node.Path.Length; i++)
        {
            if (WalkFieldForAssignment(node, i, target) is not { } next)
            {
                return PrimitiveType.Void;
            }

            target = next;
        }

        // O tipo do alvo não muda: uma atribuição precisa caber nele, como um
        // argumento precisa caber no parâmetro. "Caber" é a relação, não a
        // igualdade — é o que faz `var a = .[1]; a = .[1, 2];` valer, já que o
        // `var` foi declarado com tamanho `?` (Q29).
        if (!TypeRelations.IsAssignableTo(valueType, target))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.TypeMismatch,
                node.Value.Span,
                $"esperado {target.ToDisplayString()}, encontrado {valueType.ToDisplayString()}");
        }

        return PrimitiveType.Void;
    }

    /// <summary>
    /// Um segmento do caminho de <c>u.a.b = e;</c>, ou <c>null</c> quando já houve
    /// diagnóstico.
    ///
    /// Um membro na posição de campo recebe <c>LAP0707</c>, e não
    /// <c>LAP0250</c>: <c>hello</c> <b>existe</b> em <c>User</c>, só não é campo da
    /// instância, e "campo desconhecido" seria mentira.
    /// </summary>
    /// <summary>
    /// O dono de um <c>Let</c> de membro: a definição mais o padrão de alcance
    /// (plano 23 §23.4). <c>null</c> quando o <c>Let</c> não é membro, ou quando o
    /// dono não se sustenta — caso em que o diagnóstico já saiu daqui.
    ///
    /// O dono precisa ser um tipo **já declarado** — a ordem do topo é sequencial
    /// (Q8), então um membro antes do <c>type</c> recebe <c>LAP0704</c> pelo mesmo
    /// motivo que qualquer nome usado antes da declaração.
    /// </summary>
    private MemberOwner? OwnerOf(CoreLet node, Scope scope)
    {
        if (node.IsSynthetic || MemberNames.Split(node.Name) is not { } member)
        {
            return null;
        }

        var ownerSpan = node.OwnerSpan ?? node.NameSpan;
        var ownerName = MemberNames.OwnerName(member.Owner);

        if (!scope.TryLookup(ownerName, out var binding) || binding.Type is not MetaType meta)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.MemberOwnerMustBeAType,
                ownerSpan,
                $"'{ownerName}' não é um tipo declarado",
                new DiagnosticNote("o dono de um membro precisa ser um 'type' ou 'enum' já declarado"));

            return null;
        }

        // O `Owner` sintático só falta quando o `Let` foi fabricado sem passar pelo
        // desugar de `def T.m`; aí não há padrão a ler, e o dono é o tipo cru.
        var pattern = node.Owner is { } written
            ? _types.ResolveOwnerPattern(written, meta.Definition, scope)
            : [];

        return pattern is { } resolved
            ? new MemberOwner(meta.Definition, resolved)
            : new MemberOwner(meta.Definition, AllWildcards(meta.Definition), IsWellFormed: false);
    }

    private static ImmutableArray<GenericArgument> AllWildcards(TypeDefinition definition) =>
        [.. definition.TypeParameters.Select(_ => (GenericArgument)WildcardArgument.Instance)];

    private LapisType? WalkFieldForAssignment(CoreAssign node, int index, LapisType target)
    {
        var name = node.Path[index];
        var span = index < node.PathSpans.Length ? node.PathSpans[index] : node.NameSpan;

        if (target is ErrorType)
        {
            return null;
        }

        if (target is not NamedType { Definition.Kind: TypeDefinitionKind.Struct } instance)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownField,
                span,
                $"{target.ToDisplayString()} não possui o campo '{name}'");

            return null;
        }

        var definition = instance.Definition;
        var fieldIndex = definition.IndexOfField(name);

        if (fieldIndex >= 0)
        {
            return TypeSubstitution.Apply(
                definition.Fields[fieldIndex].Type,
                BuildSubstitution(definition.TypeParameters, instance.Arguments));
        }

        if (_members.TryGetValue(definition.Id, out var table) && table.ContainsKey(name))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.AssignToMember,
                span,
                $"'{name}' é um membro de {definition.Name} e não um campo da instância",
                new DiagnosticNote("membros são definitivos; só campos podem ser reatribuídos"));

            return null;
        }

        _diagnostics.ReportError(
            DiagnosticCodes.UnknownField,
            span,
            $"{definition.Name} não possui o campo '{name}'");

        return null;
    }

    /// <summary>
    /// Um <c>var</c> só existe dentro da função que o declarou (Q25).
    ///
    /// A restrição é o que dispensa decidir se uma closure captura por valor ou por
    /// referência: nenhuma closure captura <c>var</c>. Sem aliasing, o partial
    /// evaluator continua vendo uma closure como (código, ambiente imutável).
    /// </summary>
    private bool ReportIfCrossesFunction(BindingInfo binding, SourceSpan span)
    {
        if (!binding.IsMutable || binding.FunctionDepth == _functions.Count)
        {
            return false;
        }

        _diagnostics.ReportError(
            DiagnosticCodes.MutableCapturedByFunction,
            span,
            $"'{binding.Name}' é 'var' e não pode ser usado dentro de outra função",
            new DiagnosticNote("declarado aqui; uma função não captura 'var'", binding.Span));

        return true;
    }

    // ------------------------------------------------------------ saltos

    /// <summary>
    /// O tipo de um <c>loop</c> é a junção de todo <c>break</c> que o alcança
    /// (plano 26 §26.5) — acumulada em <see cref="LoopContext.Type"/> por
    /// <see cref="CheckBreak"/> enquanto o corpo é checado. Sem <c>break</c>
    /// alcançável, o tipo é <c>Never</c>: o laço, se termina, só termina por
    /// <c>return</c>/<c>throw</c> ou <c>break</c> de um laço externo.
    /// </summary>
    private LapisType CheckLoop(CoreLoop node, Scope scope)
    {
        var context = new LoopContext(node.Label);
        _loops.Push(context);

        try
        {
            CheckExpression(node.Body, scope.Child());
        }
        finally
        {
            _loops.Pop();
        }

        return context.Type ?? NeverType.Instance;
    }

    /// <summary>
    /// <c>Never</c>, como <c>return</c>/<c>throw</c> (Q13) — nenhuma regra de tipo
    /// nova. O efeito de verdade é lateral: junta <see cref="CoreBreak.Value"/> ao
    /// acumulador do <see cref="LoopContext"/> que o rótulo (ou a ausência dele)
    /// resolve, o que é o que dá ao <c>loop</c> o seu tipo.
    /// </summary>
    private LapisType CheckBreak(CoreBreak node, Scope scope)
    {
        var context = ResolveLoopLabel(node.Label, node.LabelSpan ?? node.Span);
        var valueType = node.Value is null ? PrimitiveType.Void : CheckExpression(node.Value, scope);

        if (context is not null && context.Type is not ErrorType)
        {
            if (context.Type is null)
            {
                context.Type = valueType;
            }
            else if (TypeRelations.Join(context.Type, valueType) is { } joined)
            {
                context.Type = joined;
            }
            else
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.IncompatibleBreakValues,
                    node.Span,
                    $"valores de 'break' no mesmo loop têm tipos incompatíveis: "
                    + $"{context.Type.ToDisplayString()} e {valueType.ToDisplayString()}");
                context.Type = ErrorType.Instance;
            }
        }

        return NeverType.Instance;
    }

    /// <summary>
    /// <c>Never</c>, como <see cref="CheckBreak"/> — mas sem valor: um
    /// <c>continue</c> reinicia a iteração, não contribui para o tipo do
    /// <c>loop</c>.
    /// </summary>
    private LapisType CheckContinue(CoreContinue node)
    {
        ResolveLoopLabel(node.Label, node.LabelSpan ?? node.Span);
        return NeverType.Instance;
    }

    /// <summary>
    /// O <see cref="LoopContext"/> que <paramref name="label"/> alcança — o
    /// <c>loop</c> mais próximo quando <c>null</c>, ou o que declara aquele
    /// rótulo. <c>null</c> quando o diagnóstico já foi reportado:
    /// <c>LAP0523</c> (fora de <c>loop</c>), <c>LAP0524</c> (rótulo desconhecido)
    /// ou <c>LAP0525</c> (rótulo de função externa).
    /// </summary>
    private LoopContext? ResolveLoopLabel(string? label, SourceSpan span)
    {
        if (label is null)
        {
            if (_loops.Count > 0)
            {
                return _loops.Peek();
            }

            _diagnostics.ReportError(
                DiagnosticCodes.BreakOrContinueOutsideLoop, span, "'break'/'continue' fora de um 'loop'");
            return null;
        }

        foreach (var loop in _loops)
        {
            if (loop.Label == label)
            {
                return loop;
            }
        }

        if (_enclosingFunctionLoopLabels.Contains(label))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.LoopLabelOutOfScope,
                span,
                $"o rótulo '{label}' pertence a uma função externa",
                new DiagnosticNote("um 'break'/'continue' não atravessa fronteira de função, assim como 'return'"));
            return null;
        }

        _diagnostics.ReportError(DiagnosticCodes.UnknownLoopLabel, span, $"o rótulo '{label}' não existe");
        return null;
    }

    /// <summary>
    /// Contexto de um <c>loop</c> em checagem: o rótulo, se houver, e o
    /// acumulador de tipo que <see cref="CheckBreak"/> preenche.
    /// </summary>
    private sealed class LoopContext(string? label)
    {
        public string? Label { get; } = label;

        public LapisType? Type { get; set; }
    }

    private LapisType CheckBinary(CoreBinary node, Scope scope)
    {
        var left = CheckExpression(node.Left, scope);
        var right = CheckExpression(node.Right, scope);

        if (left is ErrorType || right is ErrorType)
        {
            return ErrorType.Instance;
        }

        // Um operando que sempre retorna faz a expressão inteira divergir: nada
        // depois dele é avaliado, então não há tipo a conferir.
        if (left is NeverType || right is NeverType)
        {
            return NeverType.Instance;
        }

        if (node.Operator.IsEquality())
        {
            return CheckEquality(node, left, right);
        }

        if (left != right)
        {
            return ReportOperatorMismatch(node, left, right);
        }

        var isComparison = node.Operator.IsComparison();

        return left switch
        {
            PrimitiveType { Kind: PrimitiveKind.Int or PrimitiveKind.Float } =>
                isComparison ? PrimitiveType.Bool : left,

            // `+` concatena Str; `<` etc. comparam lexicograficamente.
            PrimitiveType { Kind: PrimitiveKind.Str } when isComparison => PrimitiveType.Bool,
            PrimitiveType { Kind: PrimitiveKind.Str } when node.Operator == BinaryOperator.Add => left,

            _ => ReportOperatorMismatch(node, left, right),
        };
    }

    private LapisType CheckEquality(CoreBinary node, LapisType left, LapisType right)
    {
        if (left != right)
        {
            return ReportOperatorMismatch(node, left, right);
        }

        if (!TypeRelations.IsComparable(left))
        {
            var code = left is FunctionType
                ? DiagnosticCodes.FunctionsNotComparable
                : DiagnosticCodes.OperatorNotApplicable;

            _diagnostics.ReportError(
                code,
                node.OperatorSpan,
                left is FunctionType
                    ? $"funções não podem ser comparadas com '{node.Operator.Symbol()}'"
                    : $"operador '{node.Operator.Symbol()}' não se aplica a {left.ToDisplayString()}");

            return PrimitiveType.Bool;
        }

        return PrimitiveType.Bool;
    }

    private LapisType ReportOperatorMismatch(CoreBinary node, LapisType left, LapisType right)
    {
        _diagnostics.ReportError(
            DiagnosticCodes.OperatorNotApplicable,
            node.OperatorSpan,
            $"operador '{node.Operator.Symbol()}' não se aplica a "
            + $"{left.ToDisplayString()} e {right.ToDisplayString()}");

        return ErrorType.Instance;
    }

    private LapisType CheckUnary(CoreUnary node, Scope scope)
    {
        var operand = CheckExpression(node.Operand, scope);

        if (operand is ErrorType)
        {
            return ErrorType.Instance;
        }

        if (operand is NeverType)
        {
            return NeverType.Instance;
        }

        var valid = node.Operator switch
        {
            UnaryOperator.Negate => operand is PrimitiveType { Kind: PrimitiveKind.Int or PrimitiveKind.Float },
            UnaryOperator.Not => operand is PrimitiveType { Kind: PrimitiveKind.Bool },
            _ => false,
        };

        if (!valid)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.OperatorNotApplicable,
                node.Span,
                $"operador '{node.Operator.Symbol()}' não se aplica a {operand.ToDisplayString()}");
            return ErrorType.Instance;
        }

        return operand;
    }

    // ------------------------------------------- arrays, índice, enums

    private LapisType CheckArray(CoreSpan node, Scope scope, LapisType? expected = null)
    {
        if (node.Elements.IsEmpty)
        {
            // Um span vazio diz o **tamanho** (zero), não o que carrega; quem diz
            // o elemento é a anotação, quando existe (spec §18).
            if (expected is SpanType expectedSpan)
            {
                return SpanType.Of(expectedSpan.Element, 0);
            }

            _diagnostics.ReportError(
                DiagnosticCodes.EmptyArrayNeedsAnnotation,
                node.Span,
                "span vazio requer anotação de tipo",
                new DiagnosticNote("anote o `def`, por exemplo `def a: [Int;0] = .[];`"));

            return ErrorType.Instance;
        }

        var first = CheckExpression(node.Elements[0], scope);

        for (var i = 1; i < node.Elements.Length; i++)
        {
            var element = CheckExpression(node.Elements[i], scope);

            if (first is ErrorType || element is ErrorType)
            {
                first = ErrorType.Instance;
                continue;
            }

            // Junção, não igualdade: `.[.[1], .[2, 3]]` é um span de spans de
            // tamanhos diferentes, e o elemento comum é `[Int;?]` — a mesma regra
            // que dá tipo a `if c { .[1] } else { .[1, 2] }`.
            if (TypeRelations.Join(first, element) is not { } joined)
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.HeterogeneousArray,
                    node.Elements[i].Span,
                    $"elementos de span devem ter o mesmo tipo: {first.ToDisplayString()} "
                    + $"e {element.ToDisplayString()}");
                return ErrorType.Instance;
            }

            first = joined;
        }

        // O tamanho vem do literal: `.[1,2,3]` é `[Int;3]`.
        return first is ErrorType ? ErrorType.Instance : SpanType.Of(first, node.Elements.Length);
    }

    /// <summary>
    /// <c>.[T; inicial; n]</c> — span por repetição.
    ///
    /// O tamanho segue a mesma noção de constante da Q18: literal, <c>def</c>
    /// ligado a literal, parâmetro const genérico. Quando o tamanho é constante o
    /// tipo é <c>[T;n]</c> e a indexação por índice literal volta a ser total —
    /// que é o ponto de a quantidade estar escrita. Quando não é, o tipo é
    /// <c>[T;?]</c>, exatamente como qualquer outro span cujo tamanho ninguém
    /// sabe.
    ///
    /// O elemento vem da <b>anotação escrita</b>, não do inicializador: em
    /// <c>.[Option&lt;Int&gt;; Option&lt;Int&gt;.None; n]</c> a variante nulária
    /// não determina o tipo sozinha (Q7 — não há inferência). O inicializador
    /// precisa caber no elemento, pela mesma relação de sempre.
    /// </summary>
    private LapisType CheckSpanRepeat(CoreSpanRepeat node, Scope scope)
    {
        var element = _types.Resolve(node.Element, scope);
        var initializer = CheckExpression(node.Initializer, scope, element);
        var size = CheckExpression(node.Size, scope);

        if (!TypeRelations.IsAssignableTo(initializer, element))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.TypeMismatch,
                node.Initializer.Span,
                $"esperado {element.ToDisplayString()}, encontrado {initializer.ToDisplayString()}",
                new DiagnosticNote("o valor inicial precisa caber no elemento escrito", node.Element.Span));

            return ErrorType.Instance;
        }

        if (size is not ErrorType && size != PrimitiveType.Int)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.IndexMustBeInt,
                node.Size.Span,
                $"a quantidade de um span deve ser Int, encontrado {size.ToDisplayString()}");

            return ErrorType.Instance;
        }

        if (element is ErrorType || size is ErrorType)
        {
            return ErrorType.Instance;
        }

        if (ConstIntOf(node.Size, scope) is not { } written)
        {
            // Quantidade só conhecida em execução: o span existe, mas o tipo não
            // fala do tamanho.
            _resolutions[node.NodeId] = new SpanRepeatResolution(null);
            return SpanType.Unknown(element);
        }

        // Quantidade negativa é span vazio, não erro.
        //
        // A tentação é reportar: um `-1` escrito à mão é quase certamente engano.
        // Mas a regra não sobreviveria ao partial evaluator — `0 - 1` não é
        // constante para o checker, e dobrar a subtração transformaria um programa
        // que compila num que não compila. Nenhuma transformação do PE pode mudar
        // se um programa é bem tipado.
        //
        // Então vale a escolha da divisão inteira por zero (Q9): a operação é
        // total, o resultado é o razoável, e o programa segue.
        var length = Math.Max(0, written);

        _resolutions[node.NodeId] = new SpanRepeatResolution(length <= int.MaxValue ? (int)length : null);
        return length <= int.MaxValue ? SpanType.Of(element, (int)length) : SpanType.Unknown(element);
    }

    /// <summary>
    /// Indexação (plano 24, revisando a spec §21).
    ///
    /// Com <b>tamanho e índice conhecidos</b> a operação é total e o tipo é o do
    /// elemento — nada de envelope. Fora dos limites é <c>LAP0244</c>, erro de
    /// compilação, checado pela mesma maquinaria que rejeita
    /// <c>def a: Str = 1;</c>.
    ///
    /// Em qualquer outro caso — <c>[T;?]</c>, ou índice dinâmico — o tipo é
    /// <c>Option&lt;T&gt;</c>. Deixou de ser <c>Result&lt;T, IndexError&gt;</c>
    /// (Q31): <c>IndexError.OutOfBounds</c> nunca carregou informação, e
    /// <c>Result</c> existe para o erro que <b>diz</b> alguma coisa.
    ///
    /// Provar <c>i &lt; n</c> para um <c>i</c> derivado de laço continua sendo
    /// trabalho do partial evaluator (plano 14), não deste checker.
    /// </summary>
    private LapisType CheckIndex(CoreIndex node, Scope scope)
    {
        var target = CheckExpression(node.Target, scope);
        var index = CheckExpression(node.Index, scope);

        if (index is not PrimitiveType { Kind: PrimitiveKind.Int } and not ErrorType and not NeverType)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.IndexMustBeInt,
                node.Index.Span,
                $"índice deve ser Int, encontrado {index.ToDisplayString()}");
        }

        if (target is ErrorType || index is ErrorType)
        {
            return ErrorType.Instance;
        }

        // `s[i]` sobre Str (Q35/A2a): devolve `Option<Char>`, como um `[T;?]`.
        // O tamanho não está no tipo, então nunca há indexação total aqui — a
        // falha aparece no tipo, que é o que a §30 exige (Q31).
        if (target is PrimitiveType { Kind: PrimitiveKind.Str })
        {
            _resolutions[node.NodeId] = new StrIntrinsicResolution(StrIntrinsic.Index);
            return RequirePrelude().OptionOf(PrimitiveType.Char);
        }

        if (target is not SpanType span)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.NotIndexable,
                node.Target.Span,
                $"{target.ToDisplayString()} não é indexável");
            return ErrorType.Instance;
        }

        if (span.Size is FixedSize size && ConstIntOf(node.Index, scope) is { } written)
        {
            if (written >= 0 && written < size.Value)
            {
                _resolutions[node.NodeId] = new TotalIndexResolution((int)written);
                return span.Element;
            }

            _diagnostics.ReportError(
                DiagnosticCodes.IndexOutOfBounds,
                node.Index.Span,
                $"índice {written} fora dos limites de {span.ToDisplayString()}",
                new DiagnosticNote($"o span tem {size.Value} elemento(s)", node.Target.Span));

            return ErrorType.Instance;
        }

        // As definições vêm do prelude resolvido, não de uma busca por nome: assim
        // sombrear `Option` no programa do usuário não muda a semântica de `[]`.
        var prelude = _prelude
            ?? throw new InternalCompilerException("indexação sem prelude carregado", node.Span);

        return prelude.OptionOf(span.Element);
    }

    /// <summary>
    /// Acesso a membro. Sobre um <c>MetaType</c> de enum, seleciona uma variante
    /// (<c>IndexError.OutOfBounds</c>) — a única forma de nomear variantes (Q3).
    /// </summary>
    /// <summary>O único membro de um span: quantos elementos ele tem.</summary>
    public const string LengthMember = "length";

    private LapisType CheckField(CoreField node, Scope scope)
    {
        var target = CheckExpression(node.Target, scope);

        if (target is ErrorType)
        {
            return ErrorType.Instance;
        }

        // `s.length` — constante quando o tamanho está no tipo, `Int` de runtime
        // quando é `[T;?]`. É a peça que faltava desde o M2: `array_length` estava
        // previsto no plano 09 e nunca existiu, e a falta dele impediu `@foreach`
        // e forçou um `match variants[0]` no caso de reflection.
        if (target is SpanType spanTarget && node.Name == LengthMember)
        {
            _resolutions[node.NodeId] = new SpanLengthResolution(
                spanTarget.Size is FixedSize fixedSize ? fixedSize.Value : null);

            return PrimitiveType.Int;
        }

        // `s.length` sobre Str (Q35/A2a). Sempre de execução: `Str` não carrega
        // tamanho no tipo — literal com tamanho é A2b —, então não há constante
        // a devolver, ao contrário de `[T;N]`.
        if (target is PrimitiveType { Kind: PrimitiveKind.Str } && node.Name == LengthMember)
        {
            _resolutions[node.NodeId] = new StrIntrinsicResolution(StrIntrinsic.Length);
            return PrimitiveType.Int;
        }

        // Instância de um `type`: campo primeiro, membro depois.
        if (target is NamedType { Definition.Kind: TypeDefinitionKind.Struct } structType)
        {
            if (structType.Definition.IndexOfField(node.Name) >= 0)
            {
                return CheckStructField(node, structType);
            }

            return CheckInstanceMember(node, structType);
        }

        // Uma instância de enum também recebe membros: `resultado.orDefault()`.
        if (target is NamedType named)
        {
            return CheckInstanceMember(node, named);
        }

        // `T.m` sobre um tipo que não é enum: só pode ser membro.
        if (target is MetaType nonEnum && nonEnum.Definition.Kind != TypeDefinitionKind.Enum)
        {
            return CheckStaticMember(node, nonEnum.Definition, nonEnum.Arguments);
        }

        if (target is not MetaType meta)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownField,
                node.NameSpan,
                $"{target.ToDisplayString()} não possui o campo '{node.Name}'");
            return ErrorType.Instance;
        }

        var definition = meta.Definition;
        var variantIndex = definition.IndexOfVariant(node.Name);

        if (variantIndex < 0)
        {
            // Variante primeiro, membro depois (plano 21 §21.5). A ordem não abre
            // precedência silenciosa: declarar um membro homônimo de variante é
            // LAP0703, então as duas leituras nunca coexistem.
            return CheckStaticMember(node, definition, meta.Arguments);
        }

        // Um enum genérico precisa dos argumentos de tipo aqui, e sem inferência
        // (Q7) só há um lugar de onde tirá-los: o `Enum<...>` que instanciou o
        // alvo. `Result.Ok(1)` é ambíguo; `Result<Int, IndexError>.Ok(1)` não.
        if (definition.IsGeneric && meta.Arguments.IsDefaultOrEmpty)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.CannotDetermineGenericArguments,
                node.NameSpan,
                $"não foi possível determinar os argumentos genéricos de '{definition.Name}'",
                new DiagnosticNote($"escreva '{definition.Name}<...>.{node.Name}'"));

            return ErrorType.Instance;
        }

        _resolutions[node.NodeId] = new VariantResolution(definition, variantIndex, meta.Arguments);

        var variant = definition.Variants[variantIndex];
        var enumInstance = new NamedType(definition, meta.Arguments);
        var bindings = BuildSubstitution(definition.TypeParameters, meta.Arguments);

        // Variante nulária é o próprio valor; com carga, é um construtor.
        return variant.Payload.IsDefaultOrEmpty
            ? enumInstance
            : FunctionType.Of(
                variant.Payload.Select(t => TypeSubstitution.Apply(t, bindings)), enumInstance);
    }

    /// <summary>
    /// Registra um membro declarado por <c>def T.m = e;</c> (plano 21 §21.5), sob
    /// o padrão de dono que <see cref="OwnerOf"/> já resolveu.
    /// </summary>
    private void RegisterMember(MemberOwner owner, string name, CoreLet node, LapisType type)
    {
        var ownerSpan = node.OwnerSpan ?? node.NameSpan;
        var definition = owner.Definition;

        // Um membro homônimo de variante tornaria `Color.Red` ambíguo, e a
        // ambiguidade seria resolvida por precedência silenciosa. Melhor recusar.
        if (definition.IndexOfVariant(name) >= 0)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.MemberShadowsVariant,
                ownerSpan,
                $"'{name}' já é variante de '{definition.Name}'");

            return;
        }

        var table = _members.TryGetValue(definition.Id, out var existing)
            ? existing
            : _members[definition.Id] = new Dictionary<string, List<MemberInfo>>(StringComparer.Ordinal);

        var candidates = table.TryGetValue(name, out var declared)
            ? declared
            : table[name] = [];

        // O **nome do primeiro parâmetro** é a assinatura (plano 22 §22.1): uma
        // função cujo primeiro parâmetro é `self` sem anotação é membro de
        // instância; qualquer outra é estática.
        //
        // É frágil, e é o preço de não ter sintaxe de método. `LAP0710`/`LAP0711`
        // é o que torna o erro legível quando alguém troca a forma sem querer.
        var kind = type switch
        {
            FunctionType when IsInstanceMethod(node.Value) => MemberAccessKind.InstanceMethod,
            FunctionType => MemberAccessKind.StaticMethod,
            _ => MemberAccessKind.Value,
        };

        var member = new MemberInfo(name, kind, type, node.Name, ownerSpan, owner.Pattern);

        // Padrão idêntico é redeclaração (LAP0702); padrão que só se cruza é
        // sobreposição (LAP0720). A distinção importa porque a primeira é um erro
        // de digitação e a segunda é uma decisão de alcance mal escrita.
        //
        // Duas declarações iguais no mesmo bloco já foram pegas pelo desugar; esta
        // guarda cobre blocos distintos, onde a sintaxe não bastava.
        foreach (var previous in candidates)
        {
            if (previous.OwnerPattern.SequenceEqual(member.OwnerPattern))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.DuplicateMember,
                    ownerSpan,
                    $"o membro '{name}' de "
                    + $"'{MemberInfo.OwnerToDisplayString(definition, member.OwnerPattern)}' já foi declarado",
                    new DiagnosticNote("declaração anterior", previous.Span));

                return;
            }

            if (previous.Overlaps(member))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.OverlappingMember,
                    ownerSpan,
                    $"'{name}' é declarado para "
                    + $"{MemberInfo.OwnerToDisplayString(definition, previous.OwnerPattern)} e para "
                    + $"{MemberInfo.OwnerToDisplayString(definition, member.OwnerPattern)}, que se sobrepõem",
                    new DiagnosticNote("declaração anterior", previous.Span));

                return;
            }
        }

        candidates.Add(member);
    }

    /// <summary>
    /// O membro de <paramref name="name"/> que vale para um dono com estes
    /// argumentos, ou <c>null</c> quando nenhuma declaração alcança.
    ///
    /// Nunca há dois: padrões que se cruzam são <c>LAP0720</c> na declaração.
    /// </summary>
    private MemberInfo? FindMember(
        TypeDefinition definition, string name, ImmutableArray<GenericArgument> arguments)
    {
        if (!_members.TryGetValue(definition.Id, out var table)
            || !table.TryGetValue(name, out var candidates))
        {
            return null;
        }

        // `Result.ok` sobre um genérico não diz os argumentos, e não há de onde
        // deduzi-los (Q7). Lido como "qualquer Result", ele alcança exatamente as
        // declarações que também não dizem — que é o que faz `def Result.ok`
        // funcionar sem escrever `Result<?, ?>.ok` (§23.8).
        var wanted = arguments.IsDefaultOrEmpty && definition.IsGeneric
            ? [.. definition.TypeParameters.Select(_ => (GenericArgument)WildcardArgument.Instance)]
            : arguments.IsDefault ? ImmutableArray<GenericArgument>.Empty : arguments;

        return candidates.FirstOrDefault(c => c.Accepts(wanted));
    }

    private static bool IsInstanceMethod(CoreExpr value) =>
        value is CoreLambda { Parameters: [{ Type: null, Name: MemberNames.Self }, ..] };

    /// <summary>
    /// <c>T.m</c> — a terceira leitura de <c>CheckField</c> sobre um
    /// <c>MetaType</c>, depois de campo e variante.
    ///
    /// O resultado é uma <see cref="MemberResolution"/>, como
    /// <c>VariantResolution</c> e <c>FieldResolution</c> já são: <b>nenhum nó novo
    /// na Core</b>, e o evaluator só precisa ler o nome sintético.
    /// </summary>
    private LapisType CheckStaticMember(
        CoreField node, TypeDefinition definition, ImmutableArray<GenericArgument> arguments)
    {
        if (FindMember(definition, node.Name, arguments) is { } member)
        {
            if (member.Kind == MemberAccessKind.InstanceMethod)
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.MemberRequiresInstance,
                    node.NameSpan,
                    $"o membro '{node.Name}' de {definition.Name} exige uma instância",
                    new DiagnosticNote("chame-o num valor: 'valor." + node.Name + "(...)'", member.Span));

                return ErrorType.Instance;
            }

            _resolutions[node.NodeId] = new MemberResolution(member.SyntheticName, member.Kind);
            return member.Type;
        }

        _diagnostics.ReportError(
            definition.Kind == TypeDefinitionKind.Enum
                ? DiagnosticCodes.UnknownVariant
                : DiagnosticCodes.UnknownMember,
            node.NameSpan,
            definition.Kind == TypeDefinitionKind.Enum
                ? $"{definition.Name} não possui a variante '{node.Name}'"
                : $"o tipo '{definition.Name}' não possui o membro '{node.Name}'");

        return ErrorType.Instance;
    }

    /// <summary>
    /// <c>receptor.m</c> sobre uma instância (plano 22 §22.2).
    ///
    /// O tipo devolvido é o da função <b>inteira</b>, com o receptor ainda na
    /// posição 0 — quem tira o receptor de lá é <see cref="CheckCall"/>, que é
    /// onde a chamada acontece.
    /// </summary>
    private LapisType CheckInstanceMember(CoreField node, NamedType instance)
    {
        var definition = instance.Definition;

        if (FindMember(definition, node.Name, instance.Arguments) is not { } member)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownField,
                node.NameSpan,
                $"{instance.ToDisplayString()} não possui o campo '{node.Name}'");

            return ErrorType.Instance;
        }

        // Sem essa separação, `User.hello` ficaria ambíguo entre "o membro" e "a
        // função não aplicada", e `user.hello()` e `User.hello(user)` seriam dois
        // caminhos para a mesma coisa.
        if (member.Kind != MemberAccessKind.InstanceMethod)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.MemberIsStatic,
                node.NameSpan,
                $"o membro '{node.Name}' de {definition.Name} é estático",
                new DiagnosticNote($"escreva '{definition.Name}.{node.Name}'", member.Span));

            return ErrorType.Instance;
        }

        _resolutions[node.NodeId] = new MemberResolution(member.SyntheticName, member.Kind);
        return member.Type;
    }

    private LapisType CheckStructField(CoreField node, NamedType instance)
    {
        var definition = instance.Definition;
        var index = definition.IndexOfField(node.Name);

        if (index < 0)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownField,
                node.NameSpan,
                $"{instance.ToDisplayString()} não possui o campo '{node.Name}'");
            return ErrorType.Instance;
        }

        // O tipo declarado do campo pode mencionar parâmetros do tipo; os
        // argumentos da instância os substituem.
        var bindings = BuildSubstitution(definition.TypeParameters, instance.Arguments);
        var type = TypeSubstitution.Apply(definition.Fields[index].Type, bindings);

        // Sobre um dono curinga o campo existe, mas o tipo dele não é escrevível:
        // `?` não liga nome nenhum, e é esse o preço declarado do plano 23 §23.6.
        // Deixar o parâmetro escapar seria pior — `T` apareceria em diagnósticos
        // num escopo onde `T` não existe.
        if (Wildcards(definition, instance.Arguments) is { Count: > 0 } wildcards
            && Mentions(type, wildcards))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownField,
                node.NameSpan,
                $"{instance.ToDisplayString()} não possui o campo '{node.Name}'",
                new DiagnosticNote(
                    "o tipo do campo depende de um argumento que a declaração escreveu como '?'"));

            return ErrorType.Instance;
        }

        _resolutions[node.NodeId] = new FieldResolution(index);

        return type;
    }

    /// <summary>Parâmetros do tipo ligados a <c>?</c> nesta instância.</summary>
    private static HashSet<string> Wildcards(
        TypeDefinition definition, ImmutableArray<GenericArgument> arguments)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < definition.TypeParameters.Length && i < arguments.Length; i++)
        {
            if (arguments[i] is WildcardArgument)
            {
                names.Add(definition.TypeParameters[i].Name);
            }
        }

        return names;
    }

    /// <summary>
    /// O tipo ainda menciona algum destes parâmetros? Depois da substituição, um
    /// parâmetro sobrevivente é um que não tinha argumento com que trocar.
    /// </summary>
    private static bool Mentions(LapisType type, HashSet<string> names) => type switch
    {
        TypeParameterType p => names.Contains(p.Name),
        SpanType s => Mentions(s.Element, names)
            || (s.Size is ConstSize c && names.Contains(c.Parameter)),
        FunctionType f => f.Parameters.Any(p => Mentions(p, names)) || Mentions(f.Return, names),
        NamedType n => n.Arguments.Any(a => Mentions(a, names)),
        MetaType m => m.Arguments.Any(a => Mentions(a, names)),
        _ => false,
    };

    private static bool Mentions(GenericArgument argument, HashSet<string> names) => argument switch
    {
        TypeArgument a => Mentions(a.Type, names),
        ConstParameterArgument a => names.Contains(a.Name),
        _ => false,
    };

    private LapisType CheckTypeDef(CoreTypeDef node, Scope scope)
    {
        // Parâmetros const de um `type` participam da identidade do tipo, mas não
        // do corpo: não há posição de valor entre as declarações de campo.
        var generics = DeclareTypeParameters(node.TypeParameters, scope, declareConstValues: false);

        var definition = new TypeDefinition(
            "<anônimo>", TypeDefinitionKind.Struct, generics.Parameters, node.Span);

        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var fields = ImmutableArray.CreateBuilder<FieldInfo>(node.Fields.Length);

            foreach (var field in node.Fields)
            {
                if (!seen.Add(field.Name))
                {
                    _diagnostics.ReportError(
                        DiagnosticCodes.DuplicateDefinition,
                        field.Span,
                        $"campo '{field.Name}' declarado mais de uma vez");
                }

                fields.Add(new FieldInfo(field.Name, _types.Resolve(field.Type, scope), field.Span));
            }

            definition.Fields = fields.ToImmutable();
        }
        finally
        {
            _types.ExitTypeParameters(generics.Shadowed);
        }

        _resolutions[node.NodeId] = new TypeDefinitionResolution(definition);

        return new MetaType(definition);
    }

    /// <summary>
    /// <c>.Nome { campo: valor }</c>. Todos os campos declarados devem ser
    /// inicializados, e nenhum campo desconhecido é aceito (spec §14).
    /// </summary>
    private LapisType CheckConstruct(CoreConstruct node, Scope scope)
    {
        if (!scope.TryLookup(node.TypeName, out var binding)
            || binding.Type is not MetaType { Definition.Kind: TypeDefinitionKind.Struct } meta)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.NotConstructible,
                node.TypeNameSpan,
                $"'{node.TypeName}' não é um tipo construível");

            foreach (var field in node.Fields)
            {
                CheckExpression(field.Value, scope);
            }

            return ErrorType.Instance;
        }

        var definition = meta.Definition;
        var raw = node.TypeArguments.Select(a => ReadGenericArgument(a, scope)).ToList();

        var resolved = GenericArguments.Resolve(
            _diagnostics, definition.Name, definition.TypeParameters, raw, node.TypeNameSpan);

        if (resolved is null)
        {
            foreach (var field in node.Fields)
            {
                CheckExpression(field.Value, scope);
            }

            return ErrorType.Instance;
        }

        var arguments = resolved.Value;
        var bindings = BuildSubstitution(definition.TypeParameters, arguments);
        var initialized = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in node.Fields)
        {
            var valueType = CheckExpression(field.Value, scope);
            var index = definition.IndexOfField(field.Name);

            if (index < 0)
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.ExtraField,
                    field.NameSpan,
                    $"campo '{field.Name}' não existe em {definition.Name}");
                continue;
            }

            if (!initialized.Add(field.Name))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.DuplicateFieldInitializer,
                    field.NameSpan,
                    $"campo '{field.Name}' inicializado mais de uma vez");
                continue;
            }

            var expected = TypeSubstitution.Apply(definition.Fields[index].Type, bindings);

            if (!TypeRelations.IsAssignableTo(valueType, expected))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.ArgumentTypeMismatch,
                    field.Value.Span,
                    $"campo '{field.Name}': esperado {expected.ToDisplayString()}, "
                    + $"encontrado {valueType.ToDisplayString()}");
            }
        }

        foreach (var field in definition.Fields)
        {
            if (!initialized.Contains(field.Name))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.MissingField,
                    node.Span,
                    $"campo '{field.Name}' ausente na construção de {definition.Name}");
            }
        }

        return new NamedType(definition, arguments);
    }

    private LapisType CheckEnumDef(CoreEnumDef node, Scope scope)
    {
        // Os parâmetros ficam visíveis enquanto as cargas das variantes são
        // resolvidas, e saem em seguida — eles não vazam para o resto do programa.
        var generics = DeclareTypeParameters(node.TypeParameters, scope, declareConstValues: false);

        var definition = new TypeDefinition(
            "<anônimo>", TypeDefinitionKind.Enum, generics.Parameters, node.Span);

        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var variants = ImmutableArray.CreateBuilder<VariantInfo>(node.Variants.Length);

            foreach (var variant in node.Variants)
            {
                if (!seen.Add(variant.Name))
                {
                    _diagnostics.ReportError(
                        DiagnosticCodes.DuplicateDefinition,
                        variant.Span,
                        $"variante '{variant.Name}' declarada mais de uma vez");
                }

                var payload = variant.Payload.Select(t => _types.Resolve(t, scope)).ToImmutableArray();
                variants.Add(new VariantInfo(variant.Name, payload, variant.Span));
            }

            definition.Variants = variants.ToImmutable();
        }
        finally
        {
            _types.ExitTypeParameters(generics.Shadowed);
        }

        _resolutions[node.NodeId] = new TypeDefinitionResolution(definition);

        return new MetaType(definition);
    }

    // ---------------------------------------------------------------- match

    private LapisType CheckMatch(CoreMatch node, Scope scope)
    {
        var scrutinee = CheckExpression(node.Scrutinee, scope);

        if (node.Arms.IsEmpty)
        {
            // O parser já reportou LAP0107.
            return ErrorType.Instance;
        }

        var coverage = new MatchCoverage();
        LapisType? joined = null;

        foreach (var arm in node.Arms)
        {
            var armScope = scope.Child();

            // Um braço depois de um coringa nunca executa.
            if (coverage.HasCatchAll)
            {
                _diagnostics.ReportWarning(
                    DiagnosticCodes.UnreachableArm, arm.Span, "braço inalcançável");
            }
            else if (!CheckPattern(arm.Pattern, scrutinee, armScope, coverage))
            {
                _diagnostics.ReportWarning(
                    DiagnosticCodes.UnreachableArm, arm.Span, "braço inalcançável: o caso já foi coberto");
            }

            var bodyType = CheckExpression(arm.Body, armScope);

            if (joined is null)
            {
                joined = bodyType;
                continue;
            }

            var next = TypeRelations.Join(joined, bodyType);

            if (next is null)
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.IncompatibleMatchArms,
                    arm.Body.Span,
                    $"braços de 'match' têm tipos incompatíveis: {joined.ToDisplayString()} "
                    + $"e {bodyType.ToDisplayString()}");
                joined = ErrorType.Instance;
            }
            else
            {
                joined = next;
            }
        }

        ReportIfNotExhaustive(node, scrutinee, coverage);

        return joined ?? ErrorType.Instance;
    }

    /// <summary>
    /// Q6: <c>match</c> é uma expressão e precisa produzir um valor em toda
    /// execução, logo tem de cobrir todos os casos.
    /// </summary>
    private void ReportIfNotExhaustive(CoreMatch node, LapisType scrutinee, MatchCoverage coverage)
    {
        if (coverage.HasCatchAll || scrutinee is ErrorType or NeverType)
        {
            return;
        }

        if (scrutinee is NamedType { Definition.Kind: TypeDefinitionKind.Enum } named)
        {
            var missing = named.Definition.Variants
                .Where((_, index) => !coverage.Variants.Contains(index))
                .Select(v => $"{named.Definition.Name}.{v.Name}")
                .ToList();

            if (missing.Count > 0)
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.NonExhaustiveMatch,
                    node.Span,
                    $"'match' não é exaustivo; faltam: {string.Join(", ", missing)}");
            }

            return;
        }

        // `Bool` tem exatamente dois valores, então literais bastam para esgotá-lo.
        // Int, Float e Str não, e por isso continuam exigindo um coringa.
        if (scrutinee is PrimitiveType { Kind: PrimitiveKind.Bool }
            && coverage.Literals.Contains(ConstBool.True)
            && coverage.Literals.Contains(ConstBool.False))
        {
            return;
        }

        _diagnostics.ReportError(
            DiagnosticCodes.NonExhaustiveMatch,
            node.Span,
            $"'match' sobre {scrutinee.ToDisplayString()} não é exaustivo; adicione um braço '_'");
    }

    /// <summary>
    /// Checa um padrão contra o tipo escrutinado e declara seus bindings em
    /// <paramref name="scope"/>. Devolve <c>false</c> quando o padrão é
    /// inalcançável por já ter sido coberto.
    /// </summary>
    private bool CheckPattern(CorePattern pattern, LapisType expected, Scope scope, MatchCoverage coverage)
    {
        switch (pattern)
        {
            case CoreWildcardPattern:
                coverage.HasCatchAll = true;
                return true;

            case CoreBindingPattern binding:
                coverage.HasCatchAll = true;
                scope.Declare(new BindingInfo(
                    NextBindingId(), binding.Name, expected, binding.Span, BindingKind.Value));
                return true;

            case CoreLiteralPattern literal:
                if (expected is not ErrorType && literal.Value.Type != expected)
                {
                    _diagnostics.ReportError(
                        DiagnosticCodes.PatternTypeMismatch,
                        literal.Span,
                        $"padrão incompatível com o tipo {expected.ToDisplayString()}");
                    return true;
                }

                return coverage.Literals.Add(literal.Value);

            case CoreVariantPattern variant:
                return CheckVariantPattern(variant, expected, scope, coverage);

            default:
                throw new InternalCompilerException($"padrão inesperado: {pattern.GetType().Name}");
        }
    }

    private bool CheckVariantPattern(
        CoreVariantPattern pattern,
        LapisType expected,
        Scope scope,
        MatchCoverage coverage)
    {
        if (expected is ErrorType)
        {
            return true;
        }

        if (expected is not NamedType { Definition.Kind: TypeDefinitionKind.Enum } named)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.PatternTypeMismatch,
                pattern.Span,
                $"padrão de variante não se aplica a {expected.ToDisplayString()}");
            return true;
        }

        var definition = named.Definition;

        if (!string.Equals(pattern.EnumName, definition.Name, StringComparison.Ordinal))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.PatternTypeMismatch,
                pattern.Span,
                $"padrão de '{pattern.EnumName}' não se aplica a {expected.ToDisplayString()}");
            return true;
        }

        var index = definition.IndexOfVariant(pattern.VariantName);

        if (index < 0)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownVariant,
                pattern.VariantSpan,
                $"{definition.Name} não possui a variante '{pattern.VariantName}'");
            return true;
        }

        var variant = definition.Variants[index];

        if (pattern.Arguments.Length != variant.Payload.Length)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.VariantArityMismatch,
                pattern.Span,
                $"variante '{variant.Name}' espera {variant.Payload.Length} argumentos, "
                + $"fornecidos {pattern.Arguments.Length}");

            // A variante conta como coberta mesmo com a aridade errada: o braço
            // existe e a intenção é clara. Sem isto, um erro de aridade arrastaria
            // um LAP0262 junto, e o autor consertaria dois problemas onde só há um.
            coverage.Variants.Add(index);
            return true;
        }

        // Os tipos da carga vêm da declaração e trazem os parâmetros de tipo do
        // enum; substituir pelos argumentos da instância é o que faz `v` ser `Int`
        // em `match r { Result.Ok(v) => ... }` com `r: Result<Int, IndexError>`.
        var bindings = BuildSubstitution(definition.TypeParameters, named.Arguments);

        // Sub-padrões têm sua própria cobertura: um coringa dentro de
        // `Result.Ok(_)` cobre a carga, não o `match` inteiro.
        var nested = new MatchCoverage();

        for (var i = 0; i < pattern.Arguments.Length; i++)
        {
            var payloadType = TypeSubstitution.Apply(variant.Payload[i], bindings);
            CheckPattern(pattern.Arguments[i], payloadType, scope, nested);
        }

        return coverage.Variants.Add(index);
    }

    /// <summary>
    /// <c>e is Variante</c> / <c>e is Variante(x)</c> (plano 25, fecha Q23).
    ///
    /// Aqui é onde a variante é <b>resolvida</b>: o desugar só carregou o que
    /// estava escrito, e quem sabe a qual enum ela pertence é o tipo do
    /// escrutinado (§25.5). Um dono escrito (<c>e is Option.Some</c>) não
    /// resolve nada — só é conferido contra o tipo, e é por isso que a forma
    /// sem qualificação não precisa de regra própria nem tem como ser ambígua.
    ///
    /// O tipo do nó é o join dos ramos, como em <see cref="CheckIf"/>: no teste
    /// puro os dois são literais e o join é <c>Bool</c>.
    /// </summary>
    private LapisType CheckIs(CoreIs node, Scope scope)
    {
        var scrutinee = CheckExpression(node.Scrutinee, scope);
        var thenScope = scope.Child();

        if (ResolveIsVariant(node, scrutinee) is { } resolved)
        {
            DeclareIsBinding(node, resolved.Definition, resolved.Variant, resolved.Arguments, thenScope);
        }
        else if (node.BindingName is not null)
        {
            // Sem variante resolvida não há tipo para a carga. Declarar o nome
            // como ErrorType evita um LAP0201 em cascata por cima do erro real.
            thenScope.Declare(new BindingInfo(
                NextBindingId(),
                node.BindingName,
                ErrorType.Instance,
                node.BindingSpan ?? node.Span,
                BindingKind.Value)
            {
                FunctionDepth = _functions.Count,
            });
        }

        var thenType = CheckExpression(node.Then, thenScope);
        var elseType = CheckExpression(node.Else, scope.Child());

        if (scrutinee is NeverType)
        {
            return NeverType.Instance;
        }

        var joined = TypeRelations.Join(thenType, elseType);

        if (joined is null)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.IncompatibleBranches,
                node.Span,
                $"ramos de 'is' têm tipos incompatíveis: {thenType.ToDisplayString()} "
                + $"e {elseType.ToDisplayString()}");

            return ErrorType.Instance;
        }

        return joined;
    }

    /// <summary>
    /// A variante que um <c>is</c> nomeia, resolvida pelo tipo do escrutinado.
    /// Devolve <c>null</c> quando não há como decidir; o <c>LAP0732</c> já saiu.
    /// </summary>
    private (TypeDefinition Definition, VariantInfo Variant, ImmutableArray<GenericArgument> Arguments)?
        ResolveIsVariant(CoreIs node, LapisType scrutinee)
    {
        if (scrutinee is ErrorType or NeverType)
        {
            return null;
        }

        if (scrutinee is not NamedType { Definition.Kind: TypeDefinitionKind.Enum } named)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownIsVariant,
                node.VariantSpan,
                $"'{node.VariantName}' não é variante de {scrutinee.ToDisplayString()}");

            return null;
        }

        var definition = named.Definition;

        // O dono escrito é conferido, não usado para resolver: quem manda é o
        // tipo. `r is Option.Ok` sobre um `Result` é erro aqui, e não silêncio.
        if (node.OwnerName is not null
            && !string.Equals(node.OwnerName, definition.Name, StringComparison.Ordinal))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownIsVariant,
                node.VariantSpan,
                $"'{node.OwnerName}.{node.VariantName}' não se aplica a {scrutinee.ToDisplayString()}");

            return null;
        }

        var index = definition.IndexOfVariant(node.VariantName);

        if (index < 0)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownIsVariant,
                node.VariantSpan,
                $"{definition.Name} não possui a variante '{node.VariantName}'");

            return null;
        }

        return (definition, definition.Variants[index], named.Arguments);
    }

    /// <summary>
    /// Declara a carga no escopo do ramo verdadeiro — o único lugar em que ela
    /// existe (§25.3). <c>is</c> liga <b>um</b> valor: variante nulária é
    /// <c>LAP0733</c> e variante de duas ou mais cargas é <c>LAP0734</c>, que
    /// manda escrever o <c>match</c> — a fronteira deliberada entre as duas
    /// construções (§25.5).
    /// </summary>
    private void DeclareIsBinding(
        CoreIs node,
        TypeDefinition definition,
        VariantInfo variant,
        ImmutableArray<GenericArgument> arguments,
        Scope thenScope)
    {
        if (node.BindingName is null)
        {
            return;
        }

        if (variant.Payload.Length != 1)
        {
            _diagnostics.ReportError(
                variant.Payload.IsEmpty
                    ? DiagnosticCodes.IsVariantHasNoPayload
                    : DiagnosticCodes.IsVariantHasMultiplePayloads,
                node.BindingSpan ?? node.Span,
                variant.Payload.IsEmpty
                    ? $"a variante '{variant.Name}' não carrega valor"
                    : $"a variante '{variant.Name}' carrega {variant.Payload.Length} valores; "
                      + "escreva um padrão de 'match'");
        }

        // Os tipos da carga vêm da declaração e trazem os parâmetros do enum;
        // substituir pelos argumentos da instância é o que faz `v` ser `Int` em
        // `o is Some(v)` com `o: Option<Int>` — mesma conta de CheckVariantPattern.
        var payload = variant.Payload.Length == 1
            ? TypeSubstitution.Apply(
                variant.Payload[0], BuildSubstitution(definition.TypeParameters, arguments))
            : ErrorType.Instance;

        thenScope.Declare(new BindingInfo(
            NextBindingId(),
            node.BindingName,
            payload,
            node.BindingSpan ?? node.Span,
            BindingKind.Value)
        {
            FunctionDepth = _functions.Count,
        });
    }

    /// <summary>O que os braços já cobriram, para exaustividade e alcançabilidade.</summary>
    private sealed class MatchCoverage
    {
        public bool HasCatchAll { get; set; }

        public HashSet<int> Variants { get; } = [];

        public HashSet<ConstantValue> Literals { get; } = [];
    }

    /// <summary>
    /// Mapeia cada parâmetro genérico no argumento correspondente, por nome. Os
    /// const entram junto: é por eles que uma constante simbólica se fecha (Q18).
    /// </summary>
    private static Dictionary<string, GenericArgument> BuildSubstitution(
        ImmutableArray<GenericParameter> parameters,
        ImmutableArray<GenericArgument> arguments)
    {
        var bindings = new Dictionary<string, GenericArgument>(StringComparer.Ordinal);

        for (var i = 0; i < parameters.Length && i < arguments.Length; i++)
        {
            bindings[parameters[i].Name] = arguments[i];
        }

        return bindings;
    }

    // ------------------------------------------------------------ auxiliar

    private BindingId NextBindingId() => new(_nextBindingId++);

    /// <summary>Nome visível mais próximo, por distância de edição ≤ 2.</summary>
    private static string? FindSuggestion(string name, Scope scope)
    {
        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in scope.VisibleNames())
        {
            if (candidate.StartsWith('$'))
            {
                continue;
            }

            var distance = EditDistance(name, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return bestDistance <= 2 ? best : null;
    }

    private static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    private sealed record FunctionContext(LapisType ReturnType);
}

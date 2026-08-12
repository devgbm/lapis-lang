using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Core;
using Lapis.Ast.Typed;
using Lapis.Ast.Types;
using Lapis.Diagnostics;
using Lapis.Runtime;
using Environment = Lapis.Runtime.Environment;

namespace Lapis.Evaluator;

public enum ExecutionStatus
{
    Completed,
    Aborted,
}

public sealed record EvaluationResult(
    Value Value,
    ExecutionStatus Status,
    string? Code = null,
    string? Message = null,
    SourceSpan? Span = null);

/// <summary>
/// <c>Typed Core AST × Environment → Value</c>.
///
/// Esta é a <b>implementação de referência da semântica</b> da LapisLang
/// (spec §36, §58). Prioridade absoluta: ser simples e óbvio. Onde clareza e
/// velocidade conflitam, clareza vence.
///
/// Ordem de avaliação fixada e observável: esquerda para direita, de dentro para
/// fora, sem exceções (plano 08 §8.3).
/// </summary>
public sealed class Evaluator
{
    private const int MaxCallDepth = 10_000;

    /// <summary>
    /// Orçamento de iterações do <b>programa inteiro</b>, não de cada laço.
    ///
    /// Até o M4 todo programa terminava por construção — sem recursão (Q8) e sem
    /// laços. Um <c>loop</c> com progresso acaba com isso (chegou com o salto
    /// para trás no M6, plano 26 trocou o mecanismo sem trocar a garantia), e um
    /// orçamento global é o que torna "todo programa termina ou reporta
    /// <c>LAP0303</c>" uma propriedade verificável, em vez de uma esperança.
    /// </summary>
    private const int MaxJumps = 1_000_000;

    private readonly TypedProgram _program;
    private readonly RuntimeContext _context;
    private readonly PreludeScope? _prelude;
    private readonly CompileTimeScope? _compileTime;
    private int _callDepth;
    private int _jumps;

    private Evaluator(
        TypedProgram program,
        PreludeScope? prelude,
        RuntimeContext context,
        CompileTimeScope? compileTime)
    {
        _program = program;
        _prelude = prelude;
        _compileTime = compileTime;
        _context = context;
        _context.Invoke = (callee, arguments) => InvokeFromNative(callee, arguments);
    }

    /// <param name="compileTime">
    /// Ambiente de compile time, quando o que se avalia é um <c>constraint</c>
    /// (plano 18 §18.1). O evaluator é <b>o mesmo</b> nas duas fases — o que muda
    /// é só o que está no escopo raiz.
    /// </param>
    public static EvaluationResult Run(
        TypedProgram program,
        PreludeScope? prelude,
        RuntimeContext context,
        CompileTimeScope? compileTime = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(context);

        var evaluator = new Evaluator(program, prelude, context, compileTime);
        var environment = CreateRootEnvironment(prelude, compileTime);
        var completion = evaluator.Evaluate(program.Program.Body, environment);

        return completion.Kind switch
        {
            CompletionKind.Abort => new EvaluationResult(
                VoidValue.Instance,
                ExecutionStatus.Aborted,
                completion.Code,
                ((StrValue)completion.Value).Value,
                completion.Span),

            // Um `Return` chegando ao topo significaria `return` fora de função,
            // que o checker rejeita (LAP0274).
            CompletionKind.Return => throw new InternalCompilerException(
                "'return' escapou para o topo do programa"),

            // Idem para break/continue sem loop correspondente: LAP0523 rejeita.
            CompletionKind.Break => throw new InternalCompilerException(
                "'break' escapou para o topo do programa"),

            CompletionKind.Continue => throw new InternalCompilerException(
                "'continue' escapou para o topo do programa"),

            _ => new EvaluationResult(completion.Value, ExecutionStatus.Completed),
        };
    }

    /// <summary>
    /// O <c>TypeInfo</c> que o checker resolveu. Duas fontes, uma construção
    /// (plano 19 §19.3): a resolvida monta o valor a partir da
    /// <c>TypeDefinition</c>; a sintática já o recebeu pronto da tabela de
    /// declarações, porque em compile time não há definição checada de onde tirá-lo.
    /// </summary>
    private Value EvaluateReflect(ReflectResolution reflect, CoreCall node)
    {
        if (reflect.Definition is { } definition)
        {
            return (_prelude ?? throw new InternalCompilerException("'reflect' exige o prelude", node.Span))
                .MakeTypeInfo(definition, reflect.Arguments);
        }

        return _compileTime?.Declarations.GetValueOrDefault(reflect.SyntacticName!)
            ?? throw new InternalCompilerException(
                $"'reflect({reflect.SyntacticName})' resolveu para uma declaração ausente", node.Span);
    }

    private static Environment CreateRootEnvironment(PreludeScope? prelude, CompileTimeScope? compileTime) =>
        Environment.Empty.ExtendAll(
        [
            .. Natives.All.Select(n => (n.Name, (Value)n)),
            .. (prelude?.Bindings ?? []).Select(b => (b.Name, b.Value)),
            .. (compileTime?.Bindings ?? []).Select(b => (b.Name, b.Value)),
        ]);

    // ------------------------------------------------------------ despacho

    private Completion Evaluate(CoreExpr node, Environment environment) => node switch
    {
        CoreLiteral n => Completion.Normal(FromConstant(n.Value)),
        CoreVariable n => EvaluateVariable(n, environment),
        CoreLet n => EvaluateLet(n, environment),
        CoreLambda n => EvaluateLambda(n, environment),
        CoreCall n => EvaluateCall(n, environment),
        CoreInstantiate n => EvaluateInstantiate(n, environment),
        CoreReturn n => EvaluateReturn(n, environment),
        CoreThrow n => EvaluateThrow(n, environment),
        CoreIf n => EvaluateIf(n, environment),
        CoreBinary n => EvaluateBinary(n, environment),
        CoreUnary n => EvaluateUnary(n, environment),
        CoreSpan n => EvaluateArray(n, environment),
        CoreSpanRepeat n => EvaluateSpanRepeat(n, environment),
        CoreIndex n => EvaluateIndex(n, environment),
        CoreField n => EvaluateField(n, environment),
        CoreEnumDef n => EvaluateEnumDef(n),
        CoreMatch n => EvaluateMatch(n, environment),
        CoreIs n => EvaluateIs(n, environment),
        CoreTypeDef n => EvaluateTypeDef(n),
        CoreConstruct n => EvaluateConstruct(n, environment),
        CoreLoop n => EvaluateLoop(n, environment),
        CoreBreak n => EvaluateBreak(n, environment),
        CoreContinue n => Completion.Continue(n.Label),
        CoreAssign n => EvaluateAssign(n, environment),
        _ => throw InternalCompilerException.Unreachable(node, node.Span),
    };

    private static Value FromConstant(ConstantValue constant) => constant switch
    {
        ConstInt c => new IntValue(c.Value),
        ConstFloat c => new FloatValue(c.Value),
        ConstBool c => BoolValue.Of(c.Value),
        ConstStr c => new StrValue(c.Value),
        ConstUnit => VoidValue.Instance,
        _ => throw InternalCompilerException.Unreachable(constant),
    };

    private static Completion EvaluateVariable(CoreVariable node, Environment environment)
    {
        if (environment.TryLookup(node.Name, out var value))
        {
            return Completion.Normal(value);
        }

        throw new InternalCompilerException(
            $"variável '{node.Name}' não encontrada em tempo de execução; "
            + "o type checker deveria ter rejeitado", node.Span);
    }

    private Completion EvaluateLet(CoreLet node, Environment environment)
    {
        var value = Evaluate(node.Value, environment);

        if (!value.IsNormal)
        {
            return value;
        }

        return Evaluate(node.Body, environment.Extend(node.Name, value.Value, node.IsMutable));
    }

    /// <summary>
    /// <c>x = e</c>. O slot já existe — quem o criou foi o <c>Let</c> mutável —,
    /// então a atribuição só troca o conteúdo. É por isso que o corpo de um join,
    /// que roda sempre no mesmo ambiente, enxerga o valor da volta anterior: é o
    /// que faz um laço avançar (Q25).
    /// </summary>
    private Completion EvaluateAssign(CoreAssign node, Environment environment)
    {
        var value = Evaluate(node.Value, environment);

        if (!value.IsNormal)
        {
            return value;
        }

        var assigned = value.Value;

        // `u.endereco.rua = e;` — **atualização funcional**, não mutação no lugar
        // (plano 21 §21.3b): o struct é reconstruído de dentro para fora e o slot
        // recebe o valor novo.
        //
        // É o que preserva tudo o que a Q25 comprou: todo `Value` continua
        // imutável, a única coisa mutável continua sendo o slot do ambiente, e não
        // há aliasing para o partial evaluator modelar. O preço é semântica de
        // valor, e é observável — `def b = a; a.name = "y";` deixa `b` com o valor
        // antigo.
        if (!node.Path.IsEmpty)
        {
            if (!environment.TryLookup(node.Name, out var current))
            {
                throw new InternalCompilerException(
                    $"atribuição a '{node.Name}', que não existe no ambiente", node.Span);
            }

            assigned = Rebuild(current, node.Path, 0, assigned, node.Span);
        }

        if (!environment.TryAssign(node.Name, assigned))
        {
            throw new InternalCompilerException(
                $"atribuição a '{node.Name}', que não existe no ambiente");
        }

        return Completion.Normal(VoidValue.Instance);
    }

    /// <summary>
    /// O valor de <paramref name="target"/> com o campo em
    /// <c>path[index..]</c> trocado por <paramref name="replacement"/>.
    ///
    /// Recursivo porque o caminho pode ser aninhado, e cada nível reconstrói o seu
    /// próprio struct: nenhum valor existente é alterado.
    /// </summary>
    private static Value Rebuild(
        Value target,
        ImmutableArray<string> path,
        int index,
        Value replacement,
        SourceSpan span)
    {
        if (index >= path.Length)
        {
            return replacement;
        }

        if (target is not StructValue instance)
        {
            throw new InternalCompilerException(
                "atribuição a campo de valor que não é instância de type; "
                + "o checker deveria ter rejeitado", span);
        }

        var field = instance.Definition.IndexOfField(path[index]);

        if (field < 0)
        {
            throw new InternalCompilerException(
                $"campo '{path[index]}' não existe em '{instance.Definition.Name}'; "
                + "o checker deveria ter rejeitado", span);
        }

        var inner = Rebuild(instance.Fields[field], path, index + 1, replacement, span);

        return instance with { Fields = instance.Fields.SetItem(field, inner) };
    }

    private Completion EvaluateLambda(CoreLambda node, Environment environment)
    {
        // A closure captura o ambiente do ponto de definição (spec §32).
        var signature = (FunctionType)_program.TypeOf(node);

        return Completion.Normal(new ClosureValue(node, environment, signature));
    }

    /// <summary>
    /// <c>alvo&lt;A, B&gt;</c>. Argumentos de <b>tipo</b> não têm efeito em execução:
    /// o checker já substituiu, e o valor não muda. Argumentos <b>const</b> têm:
    /// dentro do corpo, um parâmetro const é um valor de verdade (spec §13), e é
    /// aqui que ele entra no ambiente da closure.
    /// </summary>
    private Completion EvaluateInstantiate(CoreInstantiate node, Environment environment)
    {
        var target = Evaluate(node.Target, environment);

        if (!target.IsNormal)
        {
            return target;
        }

        if (_program.ResolutionOf<InstantiateResolution>(node) is not { } resolution
            || !resolution.Parameters.Any(p => p.IsConst))
        {
            return target;
        }

        if (target.Value is not ClosureValue closure)
        {
            throw new InternalCompilerException(
                "parâmetros const sobre algo que não é uma closure", node.Span);
        }

        var bindings = new List<(string, Value)>();

        for (var i = 0; i < resolution.Parameters.Length && i < node.Arguments.Length; i++)
        {
            if (!resolution.Parameters[i].IsConst)
            {
                continue;
            }

            // Escrito como literal ou função literal, o argumento é uma expressão a
            // avaliar; escrito como nome (Q18), é um `def` que o checker já provou
            // constante, e basta buscá-lo no ambiente.
            var value = node.Arguments[i] switch
            {
                CoreValueArgument argument => Evaluate(argument.Value, environment),

                CoreNameArgument named when environment.TryLookup(named.Name, out var bound) =>
                    Completion.Normal(bound),

                _ => throw new InternalCompilerException(
                    $"argumento const de '{resolution.Parameters[i].Name}' sem valor em execução",
                    node.Span),
            };

            if (!value.IsNormal)
            {
                return value;
            }

            bindings.Add((resolution.Parameters[i].Name, value.Value));
        }

        var signature = (FunctionType)_program.TypeOf(node);

        return Completion.Normal(closure with
        {
            Captured = closure.Captured.ExtendAll(bindings),
            Signature = signature,
        });
    }

    private Completion EvaluateReturn(CoreReturn node, Environment environment)
    {
        if (node.Value is null)
        {
            return Completion.Return(VoidValue.Instance);
        }

        var value = Evaluate(node.Value, environment);

        return value.IsNormal ? Completion.Return(value.Value) : value;
    }

    /// <summary>
    /// <c>throw</c> reaproveita <see cref="Completion.Abort"/> inteiro — o mesmo
    /// caminho de propagação que <c>LAP0302</c> usa desde o M1. Nenhuma exceção
    /// C#, nenhum caminho novo: é o que mantém a promessa de que a única forma de
    /// um programa parar cedo é um registro de completion.
    /// </summary>
    private Completion EvaluateThrow(CoreThrow node, Environment environment)
    {
        var value = Evaluate(node.Value, environment);

        if (!value.IsNormal)
        {
            return value;
        }

        return Completion.Abort(
            DiagnosticCodes.ConstraintRejected, node.Span, ((StrValue)value.Value).Value);
    }

    /// <summary>
    /// Avalia o corpo repetidamente até um <c>break</c> que este <c>loop</c>
    /// alcança — direto (<c>Completion.Break</c> capturado aqui) ou de graça
    /// (completion <c>Normal</c>, que é cair no fim do corpo, o mesmo que um
    /// <c>continue</c> implícito). Qualquer outra completion sobe intacta,
    /// exatamente como <c>Return</c> já sobe até a fronteira de chamada.
    ///
    /// <b>Iteração, não recursão</b>: uma volta a mais é mais uma passada deste
    /// <c>while</c> em C#, e a pilha não cresce — é o que mantém <c>@while</c> e
    /// laço aninhado viáveis sem risco de stack overflow no interpretador (plano
    /// 26, sucessor do plano 16 §16.6).
    /// </summary>
    private Completion EvaluateLoop(CoreLoop node, Environment environment)
    {
        while (true)
        {
            var completion = Evaluate(node.Body, environment);

            if (completion.Kind == CompletionKind.Break && Targets(node.Label, completion.Label))
            {
                return Completion.Normal(completion.Value);
            }

            var repeats = completion.IsNormal
                || (completion.Kind == CompletionKind.Continue && Targets(node.Label, completion.Label));

            if (!repeats)
            {
                return completion;
            }

            if (++_jumps > MaxJumps)
            {
                return Completion.Abort(
                    DiagnosticCodes.IterationLimitExceeded,
                    node.Span,
                    $"limite de iterações excedido (limite {MaxJumps})");
            }
        }
    }

    private Completion EvaluateBreak(CoreBreak node, Environment environment)
    {
        if (node.Value is null)
        {
            return Completion.Break(node.Label, VoidValue.Instance);
        }

        var value = Evaluate(node.Value, environment);
        return value.IsNormal ? Completion.Break(node.Label, value.Value) : value;
    }

    /// <summary>
    /// Um <c>break</c>/<c>continue</c> sem rótulo alcança o <c>loop</c> mais
    /// próximo — por isso <paramref name="completionLabel"/> nulo sempre bate.
    /// Um rotulado alcança só o <c>loop</c> com aquele rótulo, não importa
    /// quantos outros ele atravessa por cima.
    /// </summary>
    private static bool Targets(string? loopLabel, string? completionLabel) =>
        completionLabel is null || completionLabel == loopLabel;

    private Completion EvaluateIf(CoreIf node, Environment environment)
    {
        var condition = Evaluate(node.Condition, environment);

        if (!condition.IsNormal)
        {
            return condition;
        }

        var taken = condition.Value is BoolValue { Value: true } ? node.Then : node.Else;

        return Evaluate(taken, environment);
    }

    private Completion EvaluateBinary(CoreBinary node, Environment environment)
    {
        // Esquerda antes da direita, sempre — inclusive em `*` e `==`.
        var left = Evaluate(node.Left, environment);

        if (!left.IsNormal)
        {
            return left;
        }

        var right = Evaluate(node.Right, environment);

        if (!right.IsNormal)
        {
            return right;
        }

        // Nenhuma operação binária falha (Q9): não há caminho de aborto aqui.
        return Completion.Normal(Primitives.Apply(node.Operator, left.Value, right.Value));
    }

    private Completion EvaluateUnary(CoreUnary node, Environment environment)
    {
        var operand = Evaluate(node.Operand, environment);

        if (!operand.IsNormal)
        {
            return operand;
        }

        var value = node.Operator switch
        {
            UnaryOperator.Negate => Primitives.Negate(operand.Value),
            UnaryOperator.Not => Primitives.Not(operand.Value),
            _ => throw InternalCompilerException.Unreachable(node, node.Span),
        };

        return Completion.Normal(value);
    }

    private Completion EvaluateArray(CoreSpan node, Environment environment)
    {
        var elements = ImmutableArray.CreateBuilder<Value>(node.Elements.Length);

        // Elementos em ordem, como todo o resto (plano 08 §8.3).
        foreach (var element in node.Elements)
        {
            var evaluated = Evaluate(element, environment);

            if (!evaluated.IsNormal)
            {
                return evaluated;
            }

            elements.Add(evaluated.Value);
        }

        var elementType = ((SpanType)_program.TypeOf(node)).Element;

        return Completion.Normal(new SpanValue(elements.ToImmutable(), elementType));
    }

    /// <summary>
    /// <c>.[T; inicial; n]</c>.
    ///
    /// O inicializador é avaliado <b>uma vez</b> e o mesmo valor ocupa as <c>n</c>
    /// posições. Não há como observar o compartilhamento — não existe escrita em
    /// span (Q28) —, mas há como observar o número de avaliações: um
    /// inicializador que imprime imprime uma vez só. É a leitura que faz de
    /// <c>.[Int; 0; 8]</c> uma construção e não um laço escondido.
    ///
    /// Quantidade negativa produz span vazio, e não aborto: é a mesma escolha da
    /// divisão inteira por zero (Q9) — a operação é total, e o programa segue.
    /// </summary>
    private Completion EvaluateSpanRepeat(CoreSpanRepeat node, Environment environment)
    {
        var initializer = Evaluate(node.Initializer, environment);

        if (!initializer.IsNormal)
        {
            return initializer;
        }

        var size = Evaluate(node.Size, environment);

        if (!size.IsNormal)
        {
            return size;
        }

        if (size.Value is not IntValue count)
        {
            throw new InternalCompilerException(
                "quantidade de span que não é Int; o checker deveria ter rejeitado", node.Size.Span);
        }

        if (count.Value > MaxSpanRepeat)
        {
            return Completion.Abort(
                DiagnosticCodes.SpanTooLarge,
                node.Size.Span,
                $"span de {count.Value} elementos excede o limite de {MaxSpanRepeat}");
        }

        var length = (int)Math.Max(0, count.Value);
        var elementType = ((SpanType)_program.TypeOf(node)).Element;

        return Completion.Normal(new SpanValue(
            ImmutableArray.CreateRange(Enumerable.Repeat(initializer.Value, length)),
            elementType));
    }

    /// <summary>
    /// Orçamento de tamanho para a repetição com quantidade dinâmica, no mesmo
    /// espírito do orçamento de saltos: o que não pode é travar. O checker aplica
    /// o mesmo teto ao caso constante, e lá o diagnóstico é de compilação.
    /// </summary>
    private const long MaxSpanRepeat = 1_000_000;

    /// <summary>
    /// Indexação com checagem de limites (spec §41). Fora de limites <b>nunca</b>
    /// lança e nunca aborta: produz <c>Result.Err(IndexError.OutOfBounds)</c>
    /// (spec §30).
    ///
    /// O <c>Result</c> construído é o do prelude, resolvido por identidade — não
    /// por busca de nome — para que sombrear `Result` não mude a semântica de `[]`.
    /// </summary>
    private Completion EvaluateIndex(CoreIndex node, Environment environment)
    {
        var target = Evaluate(node.Target, environment);

        if (!target.IsNormal)
        {
            return target;
        }

        var index = Evaluate(node.Index, environment);

        if (!index.IsNormal)
        {
            return index;
        }

        if (target.Value is not SpanValue span || index.Value is not IntValue offset)
        {
            throw new InternalCompilerException(
                "indexação sobre valores inesperados; o checker deveria ter rejeitado", node.Span);
        }

        var outcome = Primitives.SpanGet(span, offset.Value);

        // O checker provou que o índice está dentro dos limites: o elemento sai
        // direto, sem envelope (plano 24 §24.5).
        if (_program.ResolutionOf<TotalIndexResolution>(node) is not null)
        {
            return Completion.Normal(outcome.IsInBounds
                ? outcome.Value!
                : throw new InternalCompilerException(
                    "indexação provada total saiu dos limites", node.Span));
        }

        var prelude = _prelude
            ?? throw new InternalCompilerException("indexação sem prelude carregado", node.Span);

        return Completion.Normal(outcome.IsInBounds
            ? prelude.MakeSome(outcome.Value!, span.ElementType)
            : prelude.MakeNone(span.ElementType));
    }

    private Completion EvaluateField(CoreField node, Environment environment)
    {
        // `s.length` — o valor sempre tem tamanho concreto, então a leitura é a
        // mesma com ou sem o tamanho no tipo. É o que a decisão de Q29 exige da
        // representação: o span carrega a quantidade junto do dado.
        if (_program.ResolutionOf<SpanLengthResolution>(node) is not null)
        {
            var span = Evaluate(node.Target, environment);

            return span.IsNormal
                ? Completion.Normal(new IntValue(((SpanValue)span.Value).Elements.Length))
                : span;
        }

        // `T.m` — membro de tipo (plano 21 §21.6). O alvo é o **tipo**, e um tipo
        // não carrega valor nenhum: o membro vive no ambiente sob o nome sintético
        // que o desugar emitiu, e ler o nome é tudo o que há para fazer.
        if (_program.ResolutionOf<MemberResolution>(node) is { } member)
        {
            if (!environment.TryLookup(member.SyntheticName, out var value))
            {
                throw new InternalCompilerException(
                    $"membro '{MemberNames.ToDisplayString(member.SyntheticName)}' "
                    + "não existe no ambiente", node.Span);
            }

            return Completion.Normal(value);
        }

        // Acesso a campo de instância: precisa avaliar o alvo.
        if (_program.ResolutionOf<FieldResolution>(node) is { } field)
        {
            var target = Evaluate(node.Target, environment);

            if (!target.IsNormal)
            {
                return target;
            }

            if (target.Value is not StructValue instance)
            {
                throw new InternalCompilerException(
                    "acesso a campo sobre valor que não é instância de type", node.Span);
            }

            return Completion.Normal(instance.Fields[field.FieldIndex]);
        }

        if (_program.ResolutionOf<VariantResolution>(node) is not { } resolution)
        {
            throw new InternalCompilerException(
                "acesso a membro sem resolução; o checker deveria ter rejeitado", node.Span);
        }

        var definition = resolution.Enum;
        var variant = definition.Variants[resolution.VariantIndex];

        var arguments = resolution.TypeArguments;

        // Variante nulária já é o valor; com carga, é um construtor a ser chamado.
        if (variant.Payload.IsDefaultOrEmpty)
        {
            return Completion.Normal(new EnumValue(definition, resolution.VariantIndex, [], arguments));
        }

        var signature = (FunctionType)_program.TypeOf(node);

        return Completion.Normal(
            new VariantConstructorValue(definition, resolution.VariantIndex, signature, arguments));
    }

    private Completion EvaluateEnumDef(CoreEnumDef node) => EvaluateDefinition(node);

    private Completion EvaluateTypeDef(CoreTypeDef node) => EvaluateDefinition(node);

    private Completion EvaluateDefinition(CoreExpr node)
    {
        if (_program.ResolutionOf<TypeDefinitionResolution>(node) is not { } resolution)
        {
            throw new InternalCompilerException("declaração de tipo sem definição resolvida", node.Span);
        }

        return Completion.Normal(new TypeValue(resolution.Definition));
    }

    /// <summary>
    /// Campos são avaliados na ordem em que aparecem no <b>código</b>, não na ordem
    /// de declaração do tipo — e depois reordenados para a posição declarada.
    /// </summary>
    private Completion EvaluateConstruct(CoreConstruct node, Environment environment)
    {
        if (_program.TypeOf(node) is not NamedType instance)
        {
            throw new InternalCompilerException(
                "construção sem tipo resolvido; o checker deveria ter rejeitado", node.Span);
        }

        var definition = instance.Definition;
        var fields = new Value[definition.Fields.Length];

        foreach (var initializer in node.Fields)
        {
            var evaluated = Evaluate(initializer.Value, environment);

            if (!evaluated.IsNormal)
            {
                return evaluated;
            }

            var index = definition.IndexOfField(initializer.Name);

            if (index < 0)
            {
                throw new InternalCompilerException(
                    $"campo '{initializer.Name}' não existe; o checker deveria ter rejeitado", node.Span);
            }

            fields[index] = evaluated.Value;
        }

        return Completion.Normal(
            new StructValue(definition, [.. fields], instance.Arguments));
    }

    /// <summary>
    /// Escrutinado avaliado <b>uma única vez</b>; braços testados em ordem, o
    /// primeiro que casa vence. O checker garante exaustividade (LAP0262), então
    /// "nenhum braço casou" só pode ser bug nosso.
    /// </summary>
    private Completion EvaluateMatch(CoreMatch node, Environment environment)
    {
        var scrutinee = Evaluate(node.Scrutinee, environment);

        if (!scrutinee.IsNormal)
        {
            return scrutinee;
        }

        foreach (var arm in node.Arms)
        {
            var bindings = new List<(string, Value)>();

            if (TryMatch(arm.Pattern, scrutinee.Value, bindings))
            {
                return Evaluate(arm.Body, environment.ExtendAll(bindings));
            }
        }

        throw new InternalCompilerException(
            "nenhum braço do 'match' casou; o checker deveria ter exigido exaustividade", node.Span);
    }

    /// <summary>
    /// <c>e is Variante(x)</c> (plano 25): compara a etiqueta e, quando ela casa,
    /// avalia o ramo verdadeiro com a carga ligada.
    ///
    /// O escrutinado é avaliado <b>uma vez</b> — é o que a forma do nó garante, e
    /// o motivo de ele carregar os ramos em vez de ser um <c>Bool</c> solto ao
    /// lado de uma extração.
    /// </summary>
    private Completion EvaluateIs(CoreIs node, Environment environment)
    {
        var scrutinee = Evaluate(node.Scrutinee, environment);

        if (!scrutinee.IsNormal)
        {
            return scrutinee;
        }

        if (scrutinee.Value is not EnumValue enumValue
            || !string.Equals(enumValue.Variant.Name, node.VariantName, StringComparison.Ordinal))
        {
            return Evaluate(node.Else, environment);
        }

        // A aridade já foi conferida pelo checker (LAP0733/LAP0734): se há
        // ligação, há exatamente uma carga.
        var body = node.BindingName is null
            ? environment
            : environment.Extend(node.BindingName, enumValue.Payload[0]);

        return Evaluate(node.Then, body);
    }

    private static bool TryMatch(CorePattern pattern, Value value, List<(string, Value)> bindings)
    {
        switch (pattern)
        {
            case CoreWildcardPattern:
                return true;

            case CoreBindingPattern binding:
                bindings.Add((binding.Name, value));
                return true;

            case CoreLiteralPattern literal:
                return Primitives.StructuralEquals(FromConstant(literal.Value), value);

            case CoreVariantPattern variant:
                if (value is not EnumValue enumValue
                    || !string.Equals(enumValue.Variant.Name, variant.VariantName, StringComparison.Ordinal))
                {
                    return false;
                }

                for (var i = 0; i < variant.Arguments.Length; i++)
                {
                    if (!TryMatch(variant.Arguments[i], enumValue.Payload[i], bindings))
                    {
                        return false;
                    }
                }

                return true;

            default:
                throw new InternalCompilerException($"padrão inesperado: {pattern.GetType().Name}");
        }
    }

    private Completion EvaluateCall(CoreCall node, Environment environment)
    {
        // `reflect` é intrínseco: o checker já decidiu sobre qual definição ele
        // fala, e aqui só resta montar o valor (plano 19 §19.4). Nem o callee nem
        // o argumento são avaliados — `reflect(User)` não *usa* `User`.
        if (_program.ResolutionOf<ReflectResolution>(node) is { } reflect)
        {
            return Completion.Normal(EvaluateReflect(reflect, node));
        }

        // `user.hello(x)` — o receptor é avaliado **uma vez**, e entra como
        // argumento 0 (plano 22 §22.2, §22.4).
        //
        // É o erro clássico de desugaring de método: reescrever para
        // `User#hello(user)` duplicaria a expressão do receptor, e
        // `proximo().hello()` chamaria `proximo()` duas vezes. Aqui ela é avaliada
        // no lugar em que está escrita, e o valor é passado adiante.
        var receiver = _program.ResolutionOf<CallResolution>(node)?.Receiver;
        Value? self = null;

        if (receiver is not null)
        {
            var evaluatedReceiver = Evaluate(receiver.Expression, environment);

            if (!evaluatedReceiver.IsNormal)
            {
                return evaluatedReceiver;
            }

            self = evaluatedReceiver.Value;
        }

        // O callee é avaliado antes dos argumentos; argumentos da esquerda para a
        // direita. Para um membro, avaliar o callee é ler um nome — o alvo do
        // `CoreField` não é tocado, que é o que impede a dupla avaliação.
        var callee = Evaluate(node.Callee, environment);

        if (!callee.IsNormal)
        {
            return callee;
        }

        var arguments = ImmutableArray.CreateBuilder<Value>(node.Arguments.Length + (self is null ? 0 : 1));

        if (self is not null)
        {
            arguments.Add(self);
        }

        foreach (var argument in node.Arguments)
        {
            var evaluated = Evaluate(argument, environment);

            if (!evaluated.IsNormal)
            {
                return evaluated;
            }

            arguments.Add(evaluated.Value);
        }

        return Apply(callee.Value, arguments.ToImmutable(), node.Span);
    }

    /// <summary>
    /// A única fronteira que converte <c>Return</c> de volta em <c>Normal</c>.
    /// </summary>
    private Completion Apply(Value callee, ImmutableArray<Value> arguments, SourceSpan span)
    {
        if (callee is NativeFunctionValue native)
        {
            return Completion.Normal(native.Implementation(arguments, _context));
        }

        if (callee is VariantConstructorValue constructor)
        {
            return Completion.Normal(new EnumValue(
                constructor.Definition, constructor.VariantIndex, arguments, constructor.TypeArguments));
        }

        if (callee is not ClosureValue closure)
        {
            throw new InternalCompilerException(
                $"tentativa de chamar {callee.Type.ToDisplayString()}; o checker deveria ter rejeitado", span);
        }

        if (closure.Lambda.Parameters.Length != arguments.Length)
        {
            throw new InternalCompilerException(
                $"aridade incorreta na chamada; o checker deveria ter rejeitado", span);
        }

        if (++_callDepth > MaxCallDepth)
        {
            _callDepth--;
            return Completion.Abort(
                DiagnosticCodes.CallDepthExceeded,
                span,
                $"profundidade de chamada excedida (limite {MaxCallDepth})");
        }

        try
        {
            var bindings = new (string, Value)[arguments.Length];

            for (var i = 0; i < arguments.Length; i++)
            {
                bindings[i] = (closure.Lambda.Parameters[i].Name, arguments[i]);
            }

            var completion = Evaluate(closure.Lambda.Body, closure.Captured.ExtendAll(bindings));

            return completion.Kind switch
            {
                CompletionKind.Return => Completion.Normal(completion.Value),

                // Cair no fim do corpo só é alcançável em função Void: o checker
                // garante (LAP0272) que as demais retornam em todos os caminhos.
                CompletionKind.Normal => Completion.Normal(VoidValue.Instance),

                // `break`/`continue` não atravessam fronteira de função
                // (LAP0523-LAP0525): chegar aqui significa que o checker deixou
                // passar.
                CompletionKind.Break => throw new InternalCompilerException(
                    "'break' escapou do corpo da função"),

                CompletionKind.Continue => throw new InternalCompilerException(
                    "'continue' escapou do corpo da função"),

                _ => completion,
            };
        }
        finally
        {
            _callDepth--;
        }
    }

    /// <summary>Ponte para primitivas nativas que precisem chamar de volta a linguagem.</summary>
    private Value InvokeFromNative(Value callee, ImmutableArray<Value> arguments)
    {
        var completion = Apply(callee, arguments, SourceSpan.Synthetic);

        return completion.IsNormal
            ? completion.Value
            : throw new InternalCompilerException("chamada a partir de nativo não completou normalmente");
    }
}

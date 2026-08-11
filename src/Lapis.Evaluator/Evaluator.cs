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
    /// Orçamento de saltos do <b>programa inteiro</b>, não de cada laço.
    ///
    /// Até o M4 todo programa terminava por construção — sem recursão (Q8) e sem
    /// laços. O salto para trás acaba com isso, e um orçamento global é o que
    /// torna "todo programa termina ou reporta <c>LAP0303</c>" uma propriedade
    /// verificável, em vez de uma esperança.
    /// </summary>
    private const int MaxJumps = 1_000_000;

    private readonly TypedProgram _program;
    private readonly RuntimeContext _context;
    private readonly PreludeScope? _prelude;
    private int _callDepth;
    private int _jumps;

    private Evaluator(TypedProgram program, PreludeScope? prelude, RuntimeContext context)
    {
        _program = program;
        _prelude = prelude;
        _context = context;
        _context.Invoke = (callee, arguments) => InvokeFromNative(callee, arguments);
    }

    public static EvaluationResult Run(TypedProgram program, PreludeScope? prelude, RuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(context);

        var evaluator = new Evaluator(program, prelude, context);
        var environment = CreateRootEnvironment(prelude);
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

            // Idem para um salto sem rótulo correspondente: LAP0520 o rejeita.
            CompletionKind.Goto => throw new InternalCompilerException(
                $"'goto {completion.Label}' escapou para o topo do programa"),

            _ => new EvaluationResult(completion.Value, ExecutionStatus.Completed),
        };
    }

    private static Environment CreateRootEnvironment(PreludeScope? prelude) =>
        Environment.Empty.ExtendAll(
        [
            .. Natives.All.Select(n => (n.Name, (Value)n)),
            .. (prelude?.Bindings ?? []).Select(b => (b.Name, b.Value)),
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
        CoreIf n => EvaluateIf(n, environment),
        CoreBinary n => EvaluateBinary(n, environment),
        CoreUnary n => EvaluateUnary(n, environment),
        CoreArray n => EvaluateArray(n, environment),
        CoreIndex n => EvaluateIndex(n, environment),
        CoreField n => EvaluateField(n, environment),
        CoreEnumDef n => EvaluateEnumDef(n),
        CoreMatch n => EvaluateMatch(n, environment),
        CoreTypeDef n => EvaluateTypeDef(n),
        CoreConstruct n => EvaluateConstruct(n, environment),
        CoreGoto n => Completion.Goto(n.Label),
        CoreGotoIf n => EvaluateGotoIf(n, environment),
        CoreLabeled n => EvaluateLabeled(n, environment),
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

        if (!environment.TryAssign(node.Name, value.Value))
        {
            throw new InternalCompilerException(
                $"atribuição a '{node.Name}', que não existe no ambiente");
        }

        return Completion.Normal(VoidValue.Instance);
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

    private Completion EvaluateGotoIf(CoreGotoIf node, Environment environment)
    {
        var condition = Evaluate(node.Condition, environment);

        if (!condition.IsNormal)
        {
            return condition;
        }

        return ((BoolValue)condition.Value).Value
            ? Completion.Goto(node.Label)
            : Completion.Normal(VoidValue.Instance);
    }

    /// <summary>
    /// Avalia a entrada; se ela terminar em salto para um dos joins deste grupo,
    /// avalia aquele corpo — que pode saltar de novo. Qualquer outra completion
    /// sobe, exatamente como <c>Return</c> sobe até a fronteira de chamada.
    ///
    /// O laço é <b>iteração, não recursão</b>: um salto para trás é mais uma volta
    /// deste <c>while</c>, e a pilha de C# não cresce. É o que torna
    /// <c>@while</c> viável sem risco de estouro no interpretador.
    /// </summary>
    private Completion EvaluateLabeled(CoreLabeled node, Environment environment)
    {
        var completion = Evaluate(node.Entry, environment);

        while (completion.Kind == CompletionKind.Goto && TryFindJoin(node, completion.Label, out var join))
        {
            if (++_jumps > MaxJumps)
            {
                return Completion.Abort(
                    DiagnosticCodes.JumpLimitExceeded,
                    node.Span,
                    $"limite de saltos excedido (limite {MaxJumps})");
            }

            // O ambiente é o do grupo, não o da entrada: nomes declarados entre o
            // salto e o rótulo não estão em escopo no destino — o salto pode
            // tê-los pulado (plano 16 §16.4).
            completion = Evaluate(join.Body, environment);
        }

        return completion;
    }

    /// <summary>
    /// O primeiro join com o nome procurado. Rótulo repetido no mesmo grupo é
    /// <c>LAP0522</c>; aqui a escolha só precisa ser determinística.
    /// </summary>
    private static bool TryFindJoin(CoreLabeled node, string? label, out CoreJoin join)
    {
        foreach (var candidate in node.Joins)
        {
            if (candidate.Name == label)
            {
                join = candidate;
                return true;
            }
        }

        join = null!;
        return false;
    }

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

    private Completion EvaluateArray(CoreArray node, Environment environment)
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

        var elementType = ((ArrayType)_program.TypeOf(node)).Element;

        return Completion.Normal(new ArrayValue(elements.ToImmutable(), elementType));
    }

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

        if (target.Value is not ArrayValue array || index.Value is not IntValue offset)
        {
            throw new InternalCompilerException(
                "indexação sobre valores inesperados; o checker deveria ter rejeitado", node.Span);
        }

        var prelude = _prelude
            ?? throw new InternalCompilerException("indexação sem prelude carregado", node.Span);

        var outcome = Primitives.ArrayGet(array, offset.Value);

        return Completion.Normal(outcome.IsInBounds
            ? prelude.MakeOk(outcome.Value!, array.ElementType)
            : prelude.MakeIndexError(array.ElementType));
    }

    private Completion EvaluateField(CoreField node, Environment environment)
    {
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
        // O callee é avaliado antes dos argumentos; argumentos da esquerda para a direita.
        var callee = Evaluate(node.Callee, environment);

        if (!callee.IsNormal)
        {
            return callee;
        }

        var arguments = ImmutableArray.CreateBuilder<Value>(node.Arguments.Length);

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

                // Um salto não atravessa fronteira de função (LAP0520/LAP0521):
                // chegar aqui significa que o checker deixou passar.
                CompletionKind.Goto => throw new InternalCompilerException(
                    $"'goto {completion.Label}' escapou do corpo da função"),

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

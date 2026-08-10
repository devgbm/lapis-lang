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

    private readonly TypedProgram _program;
    private readonly RuntimeContext _context;
    private int _callDepth;

    private Evaluator(TypedProgram program, RuntimeContext context)
    {
        _program = program;
        _context = context;
        _context.Invoke = (callee, arguments) => InvokeFromNative(callee, arguments);
    }

    public static EvaluationResult Run(TypedProgram program, RuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(context);

        var evaluator = new Evaluator(program, context);
        var environment = evaluator.CreateRootEnvironment();
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

            _ => new EvaluationResult(completion.Value, ExecutionStatus.Completed),
        };
    }

    private Environment CreateRootEnvironment() =>
        Environment.Empty.ExtendAll([.. Natives.All.Select(n => (n.Name, (Value)n))]);

    // ------------------------------------------------------------ despacho

    private Completion Evaluate(CoreExpr node, Environment environment) => node switch
    {
        CoreLiteral n => Completion.Normal(FromConstant(n.Value)),
        CoreVariable n => EvaluateVariable(n, environment),
        CoreLet n => EvaluateLet(n, environment),
        CoreLambda n => EvaluateLambda(n, environment),
        CoreCall n => EvaluateCall(n, environment),
        CoreReturn n => EvaluateReturn(n, environment),
        CoreIf n => EvaluateIf(n, environment),
        CoreBinary n => EvaluateBinary(n, environment),
        CoreUnary n => EvaluateUnary(n, environment),
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

        return Evaluate(node.Body, environment.Extend(node.Name, value.Value));
    }

    private Completion EvaluateLambda(CoreLambda node, Environment environment)
    {
        // A closure captura o ambiente do ponto de definição (spec §32).
        var signature = (FunctionType)_program.TypeOf(node);

        return Completion.Normal(new ClosureValue(node, environment, signature));
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

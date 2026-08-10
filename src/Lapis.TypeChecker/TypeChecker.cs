using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Core;
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
    private readonly DiagnosticBag _diagnostics;
    private readonly TypeResolver _types;
    private readonly Dictionary<int, LapisType> _nodeTypes = [];
    private readonly Dictionary<int, Resolution> _resolutions = [];
    private readonly Stack<FunctionContext> _functions = new();
    private int _nextBindingId;

    private TypeChecker(DiagnosticBag diagnostics)
    {
        _diagnostics = diagnostics;
        _types = new TypeResolver(diagnostics);
    }

    public static TypedProgram Check(CoreProgram program, DiagnosticBag diagnostics)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var checker = new TypeChecker(diagnostics);
        var scope = checker.CreateRootScope();

        checker.CheckExpression(program.Body, scope);

        return new TypedProgram(
            program,
            checker._nodeTypes.ToImmutableDictionary(),
            checker._resolutions.ToImmutableDictionary());
    }

    /// <summary>Escopo raiz com os nativos (plano 09 §9.2).</summary>
    private Scope CreateRootScope()
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

        return scope;
    }

    // ------------------------------------------------------------ despacho

    private LapisType CheckExpression(CoreExpr node, Scope scope)
    {
        var type = node switch
        {
            CoreLiteral n => n.Value.Type,
            CoreVariable n => CheckVariable(n, scope),
            CoreLet n => CheckLet(n, scope),
            CoreLambda n => CheckLambda(n, scope),
            CoreCall n => CheckCall(n, scope),
            CoreReturn n => CheckReturn(n, scope),
            CoreIf n => CheckIf(n, scope),
            CoreBinary n => CheckBinary(n, scope),
            CoreUnary n => CheckUnary(n, scope),
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
        var valueType = CheckExpression(node.Value, scope);

        if (node.Annotation is not null)
        {
            var declared = _types.Resolve(node.Annotation, node.Span);

            if (!TypeRelations.IsAssignableTo(valueType, declared))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.TypeMismatch,
                    node.Value.Span,
                    $"esperado {declared.ToDisplayString()}, encontrado {valueType.ToDisplayString()}");
            }

            valueType = declared;
        }

        // O nome só é visível no corpo — não no próprio valor. É isso que torna a
        // v0.2 não recursiva (Q8).
        //
        // Redefinição no mesmo bloco (LAP0202) é detectada no desugar, onde os
        // blocos ainda existem; aqui um `Let` interno sempre sombreia legitimamente.
        var inner = scope.Child();

        inner.Declare(new BindingInfo(NextBindingId(), node.Name, valueType, node.NameSpan, BindingKind.Value));

        var valueReturns = ReturnAnalysis.DefinitelyReturns(node.Value);

        // Código após um `return` no mesmo encadeamento é inalcançável.
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

    // --------------------------------------------------------- funções

    private LapisType CheckLambda(CoreLambda node, Scope scope)
    {
        var parameterTypes = ImmutableArray.CreateBuilder<LapisType>(node.Parameters.Length);
        var inner = scope.Child();

        foreach (var parameter in node.Parameters)
        {
            var type = _types.Resolve(parameter.Type, parameter.Span);
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

        var returnType = _types.Resolve(node.ReturnType, node.Span);
        var signature = new FunctionType(parameterTypes.ToImmutable(), returnType, []);

        _functions.Push(new FunctionContext(returnType));

        try
        {
            CheckExpression(node.Body, inner);
        }
        finally
        {
            _functions.Pop();
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

    private LapisType CheckCall(CoreCall node, Scope scope)
    {
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

        // Q7: argumentos genéricos são sempre explícitos, nunca inferidos. Como a
        // sintaxe de declaração de generics é do M4, nenhuma função genérica é
        // construível ainda — a checagem existe para o dia em que for.
        if (signature.IsGeneric)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.GenericArityMismatch,
                node.Span,
                $"a função espera {signature.TypeParameters.Length} argumentos genéricos explícitos");
            return ErrorType.Instance;
        }

        var instantiated = signature;
        _resolutions[node.NodeId] = new CallResolution([], instantiated);

        if (instantiated.Parameters.Length != arguments.Length)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.ArgumentCountMismatch,
                node.Span,
                $"esperados {instantiated.Parameters.Length} argumentos, fornecidos {arguments.Length}");
            return instantiated.Return;
        }

        for (var i = 0; i < arguments.Length; i++)
        {
            if (!TypeRelations.IsAssignableTo(arguments[i], instantiated.Parameters[i]))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.ArgumentTypeMismatch,
                    node.Arguments[i].Span,
                    $"argumento {i + 1}: esperado {instantiated.Parameters[i].ToDisplayString()}, "
                    + $"encontrado {arguments[i].ToDisplayString()}");
            }
        }

        return instantiated.Return;
    }

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

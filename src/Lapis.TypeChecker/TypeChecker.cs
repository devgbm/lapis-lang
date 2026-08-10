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
    private readonly DiagnosticBag _diagnostics;
    private readonly TypeResolver _types;
    private readonly Dictionary<int, LapisType> _nodeTypes = [];
    private readonly Dictionary<int, Resolution> _resolutions = [];
    private readonly Stack<FunctionContext> _functions = new();
    private PreludeScope? _prelude;
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
    public static TypedProgram Check(CoreProgram program, PreludeScope? prelude, DiagnosticBag diagnostics)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var checker = new TypeChecker(diagnostics) { _prelude = prelude };
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

        // O programa do usuário roda num escopo filho: sombrear `Result` é
        // permitido e não muda a semântica de `[]` (plano 09 §9.4).
        return scope.Child();
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
            CoreInstantiate n => CheckInstantiate(n, scope),
            CoreReturn n => CheckReturn(n, scope),
            CoreIf n => CheckIf(n, scope),
            CoreBinary n => CheckBinary(n, scope),
            CoreUnary n => CheckUnary(n, scope),
            CoreArray n => CheckArray(n, scope),
            CoreIndex n => CheckIndex(n, scope),
            CoreField n => CheckField(n, scope),
            CoreEnumDef n => CheckEnumDef(n, scope),
            CoreMatch n => CheckMatch(n, scope),
            CoreTypeDef n => CheckTypeDef(n, scope),
            CoreConstruct n => CheckConstruct(n, scope),
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

        // Um `type`/`enum` não tem nome próprio (spec §14, §15): ele recebe o nome
        // do `def` que o liga, e é esse nome que aparece em diagnósticos e na
        // formatação de valores (`Result.Ok(20)`).
        if (!node.IsSynthetic && valueType is MetaType meta && meta.Definition.Name == "<anônimo>")
        {
            meta.Definition.Name = node.Name;
        }

        if (node.Annotation is not null)
        {
            var declared = _types.Resolve(node.Annotation, scope);

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

        inner.Declare(new BindingInfo(NextBindingId(), node.Name, valueType, node.NameSpan, BindingKind.Value)
        {
            Constant = ConstantOf(node.Value, valueType, scope),
        });

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

    /// <summary>
    /// O valor de um <c>def</c>, quando conhecido em tempo de compilação (Q18).
    ///
    /// A linha é deliberadamente reta: um literal, uma função literal, ou outro
    /// <c>def</c> que já carregue uma constante. Isto é <b>propagação</b>, não
    /// <i>folding</i> — <c>def n = 1 + 2;</c> não produz constante, porque dobrar
    /// a expressão é trabalho do partial evaluator (spec §58) e replicá-lo no
    /// checker significaria manter duas aritméticas em sincronia.
    /// </summary>
    private static GenericArgument? ConstantOf(CoreExpr value, LapisType type, Scope scope) => value switch
    {
        CoreLiteral literal => new ConstArgument(literal.Value),

        CoreLambda lambda when type is FunctionType signature =>
            new ConstFunctionArgument(CoreSourcePrinter.PrintExpressionCompact(lambda), signature),

        CoreVariable variable when scope.TryLookup(variable.Name, out var binding) => binding.Constant,

        _ => null,
    };

    // --------------------------------------------------------- funções

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

        foreach (var parameter in node.Parameters)
        {
            var type = _types.Resolve(parameter.Type, inner);
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

        _resolutions[node.NodeId] = new CallResolution(typeArguments, instantiated);

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

    // ------------------------------------------- arrays, índice, enums

    private LapisType CheckArray(CoreArray node, Scope scope)
    {
        if (node.Elements.IsEmpty)
        {
            // Sem anotação não há como saber o tipo do elemento (spec §18).
            _diagnostics.ReportError(
                DiagnosticCodes.EmptyArrayNeedsAnnotation,
                node.Span,
                "array vazio requer anotação de tipo");
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

            if (element != first)
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.HeterogeneousArray,
                    node.Elements[i].Span,
                    $"elementos de array devem ter o mesmo tipo: {first.ToDisplayString()} "
                    + $"e {element.ToDisplayString()}");
                return ErrorType.Instance;
            }
        }

        return first is ErrorType ? ErrorType.Instance : new ArrayType(first);
    }

    /// <summary>
    /// A regra fundamental da spec §21: <c>T[][Int]</c> tem tipo
    /// <c>Result&lt;T, IndexError&gt;</c>, nunca <c>T</c>. A indexação pode falhar, e
    /// isso aparece no tipo.
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

        if (target is not ArrayType array)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.NotIndexable,
                node.Target.Span,
                $"{target.ToDisplayString()} não é indexável");
            return ErrorType.Instance;
        }

        // As definições vêm do prelude resolvido, não de uma busca por nome: assim
        // sombrear `Result` no programa do usuário não muda a semântica de `[]`.
        var prelude = _prelude
            ?? throw new InternalCompilerException("indexação sem prelude carregado", node.Span);

        return new NamedType(prelude.Result, prelude.IndexResultArguments(array.Element));
    }

    /// <summary>
    /// Acesso a membro. Sobre um <c>MetaType</c> de enum, seleciona uma variante
    /// (<c>IndexError.OutOfBounds</c>) — a única forma de nomear variantes (Q3).
    /// </summary>
    private LapisType CheckField(CoreField node, Scope scope)
    {
        var target = CheckExpression(node.Target, scope);

        if (target is ErrorType)
        {
            return ErrorType.Instance;
        }

        // Campo de uma instância de `type`.
        if (target is NamedType { Definition.Kind: TypeDefinitionKind.Struct } structType)
        {
            return CheckStructField(node, structType);
        }

        if (target is not MetaType meta || meta.Definition.Kind != TypeDefinitionKind.Enum)
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
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownVariant,
                node.NameSpan,
                $"{definition.Name} não possui a variante '{node.Name}'");
            return ErrorType.Instance;
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

        _resolutions[node.NodeId] = new FieldResolution(index);

        // O tipo declarado do campo pode mencionar parâmetros do tipo; os
        // argumentos da instância os substituem.
        var bindings = BuildSubstitution(definition.TypeParameters, instance.Arguments);

        return TypeSubstitution.Apply(definition.Fields[index].Type, bindings);
    }

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

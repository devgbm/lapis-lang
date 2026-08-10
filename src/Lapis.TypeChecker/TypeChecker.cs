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
            CoreReturn n => CheckReturn(n, scope),
            CoreIf n => CheckIf(n, scope),
            CoreBinary n => CheckBinary(n, scope),
            CoreUnary n => CheckUnary(n, scope),
            CoreArray n => CheckArray(n, scope),
            CoreIndex n => CheckIndex(n, scope),
            CoreField n => CheckField(n, scope),
            CoreEnumDef n => CheckEnumDef(n, scope),
            CoreMatch n => CheckMatch(n, scope),
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

        return new NamedType(
            prelude.Result,
            [array.Element, new NamedType(prelude.IndexError, [])]);
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

        _resolutions[node.NodeId] = new VariantResolution(definition, variantIndex);

        // Um enum genérico precisaria dos argumentos de tipo aqui, e sem inferência
        // (Q7) não há de onde tirá-los — a sintaxe para fornecê-los em posição de
        // expressão chega no M4. Enums não genéricos funcionam integralmente.
        if (definition.IsGeneric)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.CannotDetermineGenericArguments,
                node.NameSpan,
                $"não foi possível determinar os argumentos genéricos de '{definition.Name}'");
            return ErrorType.Instance;
        }

        var variant = definition.Variants[variantIndex];
        var instance = new NamedType(definition, []);

        // Variante nulária é o próprio valor; com carga, é um construtor.
        return variant.Payload.IsDefaultOrEmpty
            ? instance
            : FunctionType.Of(variant.Payload, instance);
    }

    private LapisType CheckEnumDef(CoreEnumDef node, Scope scope)
    {
        var typeParameters = node.TypeParameters
            .Select(name => new TypeParameterType(name))
            .ToImmutableArray();

        var definition = new TypeDefinition("<anônimo>", TypeDefinitionKind.Enum, typeParameters, node.Span);

        // Os parâmetros ficam visíveis enquanto as cargas das variantes são
        // resolvidas, e saem em seguida — eles não vazam para o resto do programa.
        foreach (var parameter in typeParameters)
        {
            _types.TypeParameters[parameter.Name] = parameter;
        }

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
            foreach (var parameter in typeParameters)
            {
                _types.TypeParameters.Remove(parameter.Name);
            }
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
        var bindings = BuildSubstitution(definition, named.Arguments);

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

    private static Dictionary<string, LapisType> BuildSubstitution(
        TypeDefinition definition,
        ImmutableArray<LapisType> arguments)
    {
        var bindings = new Dictionary<string, LapisType>(StringComparer.Ordinal);

        for (var i = 0; i < definition.TypeParameters.Length && i < arguments.Length; i++)
        {
            bindings[definition.TypeParameters[i].Name] = arguments[i];
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

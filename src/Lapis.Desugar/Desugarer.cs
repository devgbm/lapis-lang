using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Core;
using Lapis.Ast.Printing;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Desugar;

/// <summary>
/// <c>Surface AST → Core AST</c>, puramente sintático: sem resolver nomes, sem
/// consultar tipos, sem executar nada (spec §36).
///
/// Em particular, o desugar <b>não</b> faz constant folding — isso é trabalho do
/// partial evaluator (spec §58). A única exceção é a normalização de literais
/// negativos, que é o que faz <c>-10</c> ser um literal como a spec §6 descreve.
/// </summary>
public sealed class Desugarer
{
    private readonly CoreFactory _factory = new();
    private readonly FreshNameGenerator _names = new();
    private readonly DiagnosticBag _diagnostics;

    // Plano 25 §25.5: uma variante sem dono escrito (`e is Some`) precisa que
    // alguém diga a qual enum ela pertence. Índice por nome de variante, com
    // todo enum que a declara — o prelúdio (passado de fora, já resolvido) e o
    // próprio arquivo (recolhido abaixo, por uma varredura tão sintática quanto
    // ReportDuplicateDefinitions). Mais de uma entrada para o mesmo nome é
    // ambiguidade genuína, não erro de implementação — ver ResolveIsVariant.
    private readonly Dictionary<string, List<(string EnumName, int Arity)>> _variantIndex =
        new(StringComparer.Ordinal);

    private Desugarer(DiagnosticBag diagnostics) => _diagnostics = diagnostics;

    /// <summary>Uma variante conhecida, para resolver o dono implícito de um <c>is</c> (plano 25 §25.5).</summary>
    public readonly record struct KnownVariant(string EnumName, string VariantName, int Arity);

    public static CoreProgram Desugar(
        SourceFile file, DiagnosticBag diagnostics, IEnumerable<KnownVariant>? knownVariants = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var desugarer = new Desugarer(diagnostics);

        foreach (var known in knownVariants ?? [])
        {
            desugarer.AddKnownVariant(known.EnumName, known.VariantName, known.Arity);
        }

        desugarer.CollectFileVariants(file.Statements);
        desugarer.ReportDuplicateDefinitions(file.Statements);
        var body = desugarer.DesugarStatements(file.Statements, 0, tail: null, file.Span);

        return desugarer._factory.Program(body);
    }

    private void AddKnownVariant(string enumName, string variantName, int arity)
    {
        if (!_variantIndex.TryGetValue(variantName, out var candidates))
        {
            candidates = [];
            _variantIndex[variantName] = candidates;
        }

        // O mesmo (enum, variante) reaparecendo — o arquivo redeclarando algo que
        // o prelúdio já dá, por exemplo — não é uma segunda dona.
        if (!candidates.Any(c => c.EnumName == enumName))
        {
            candidates.Add((enumName, arity));
        }
    }

    /// <summary>
    /// Varre o arquivo procurando <c>def X = enum { ... }</c>, em qualquer bloco
    /// alcançável — não só o topo. Só isso: nenhuma outra informação sai daqui,
    /// e uma variante que esta varredura não encontra não vira suposição, vira
    /// <c>LAP0732</c> (§25.5) — o mesmo destino de uma que realmente não existe.
    /// </summary>
    private void CollectFileVariants(ImmutableArray<Statement> statements)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case DefStatement { Owner: null, Value: EnumExpression enumExpression } def:
                    foreach (var variant in enumExpression.Variants)
                    {
                        AddKnownVariant(def.Name, variant.Name, variant.Payload.Length);
                    }

                    break;

                case DefStatement def:
                    CollectFileVariants(def.Value);
                    break;

                case ExpressionStatement s:
                    CollectFileVariants(s.Expression);
                    break;

                case AssignStatement s:
                    CollectFileVariants(s.Value);
                    break;
            }
        }
    }

    private void CollectFileVariants(Expression expression)
    {
        switch (expression)
        {
            case BlockExpression block:
                CollectFileVariants(block.Statements);

                if (block.Tail is not null)
                {
                    CollectFileVariants(block.Tail);
                }

                break;

            case IfExpression n:
                CollectFileVariants(n.Then);

                if (n.Else is not null)
                {
                    CollectFileVariants(n.Else);
                }

                break;

            case LoopExpression n:
                CollectFileVariants(n.Body);
                break;

            case FunctionExpression n:
                CollectFileVariants(n.Body);
                break;

            case MatchExpression n:
                foreach (var arm in n.Arms)
                {
                    CollectFileVariants(arm.Body);
                }

                break;
        }
    }

    /// <summary>
    /// Redefinir um nome no <b>mesmo</b> bloco é <c>LAP0202</c>; sombrear num bloco
    /// interno é permitido (bindings são imutáveis, então não é mutação).
    ///
    /// A checagem mora aqui, e não no type checker, porque a Core não tem nó
    /// <c>Block</c> (Q10): uma vez virada cadeia de <c>Let</c>, "mesmo bloco" e
    /// "bloco interno" ficam indistinguíveis. A verificação é puramente sintática,
    /// então cabe nesta fase.
    /// </summary>
    private void ReportDuplicateDefinitions(ImmutableArray<Statement> statements)
    {
        Dictionary<string, DefStatement>? seen = null;

        foreach (var statement in statements)
        {
            if (statement is not DefStatement def)
            {
                continue;
            }

            seen ??= new Dictionary<string, DefStatement>(StringComparer.Ordinal);

            // A chave é o nome **sintético**: `def hello` e `def User.hello` são
            // símbolos distintos, porque o segundo só é alcançável através de um
            // tipo. Dois `def User.create` continuam colidindo, e o diagnóstico é
            // o LAP0702, mais específico que "já foi definido".
            var key = NameOf(def);

            if (seen.TryGetValue(key, out var previous))
            {
                var isMember = def.Owner is not null;

                _diagnostics.ReportError(
                    isMember ? DiagnosticCodes.DuplicateMember : DiagnosticCodes.DuplicateDefinition,
                    def.NameSpan,
                    isMember
                        ? $"o membro '{def.Name}' de '{MemberNames.Split(key)!.Value.Owner}' já foi declarado"
                        : $"'{def.Name}' já foi definido neste escopo",
                    new DiagnosticNote("declaração anterior", previous.NameSpan));
            }
            else
            {
                seen[key] = def;
            }
        }
    }

    /// <summary>
    /// Sequenciamento: <c>def x = e; resto</c> vira <c>Let(x, e, resto)</c> e
    /// <c>e; resto</c> vira <c>Let($tmpN, e, resto)</c>. A ausência de cauda produz
    /// <c>()</c> (spec §9).
    ///
    /// Puramente estrutural desde o plano 26 (Q32): sem <c>goto</c>/<c>label</c>
    /// não há decomposição em blocos básicos nenhuma a fazer — o que antes era
    /// particionamento em grupos de join points (plano 16 §16.4) hoje é só esta
    /// recursão simples. `loop`/`break`/`continue` viram nós de Core de forma
    /// igualmente direta em <see cref="DesugarExpression"/>.
    /// </summary>
    private CoreExpr DesugarStatements(
        ImmutableArray<Statement> statements,
        int index,
        Expression? tail,
        SourceSpan enclosingSpan)
    {
        if (index >= statements.Length)
        {
            return tail is not null
                ? DesugarExpression(tail)
                : _factory.Unit(EndOf(enclosingSpan));
        }

        var statement = statements[index];
        var rest = DesugarStatements(statements, index + 1, tail, enclosingSpan);

        return statement switch
        {
            DefStatement def => _factory.Let(
                def.Span,
                NameOf(def),
                def.Annotation,
                DesugarExpression(def.Value),
                rest,
                isSynthetic: false,
                def.NameSpan,
                def.IsMutable,
                def.OwnerSpan,
                def.Owner as NamedTypeSyntax),

            ExpressionStatement expression => _factory.Let(
                expression.Span,
                _names.Next(),
                annotation: null,
                DesugarExpression(expression.Expression),
                rest,
                isSynthetic: true),

            AssignStatement assign => _factory.Let(
                assign.Span,
                _names.Next(),
                annotation: null,
                DesugarAssign(assign),
                rest,
                isSynthetic: true),

            var other => throw InternalCompilerException.Unreachable(other, other.Span),
        };
    }

    /// <summary>
    /// O nome com que a declaração vive na Core. Um membro de tipo leva o nome
    /// sintético (plano 21 §21.4); o resto leva o próprio nome.
    ///
    /// O padrão do dono entra no nome tal como foi escrito, normalizado pelo
    /// printer (plano 23 §23.4): <c>def Result&lt;Int, ?&gt;.d</c> vira
    /// <c>Result&lt;Int, ?&gt;#d</c>. Sem ele, as duas declarações disjuntas que a
    /// §23.5 permite conviver dividiriam um nome só na Core.
    ///
    /// O desugar não resolve nada aqui — ele só nomeia. Se `Owner` não é um tipo
    /// declarado, quem reclama é o checker, que é quem sabe.
    /// </summary>
    private static string NameOf(DefStatement def) =>
        def.Owner is NamedTypeSyntax owner
            ? MemberNames.Of(SurfaceSExprPrinter.PrintType(owner), def.Name)
            : def.Name;

    private CoreExpr DesugarAssign(AssignStatement node) =>
        _factory.Assign(
            node.Span,
            node.Name,
            DesugarExpression(node.Value),
            node.NameSpan,
            node.Path,
            node.PathSpans);

    private CoreExpr DesugarExpression(Expression expression)
    {
        switch (expression)
        {
            case IntLiteral n:
                return _factory.Literal(n.Span, new ConstInt(n.Value));

            case FloatLiteral n:
                return _factory.Literal(n.Span, new ConstFloat(n.Value));

            case BoolLiteral n:
                return _factory.Literal(n.Span, n.Value ? ConstBool.True : ConstBool.False);

            case StrLiteral n:
                return _factory.Literal(n.Span, new ConstStr(n.Value));

            case UnitLiteral n:
                return _factory.Unit(n.Span);

            case IdentifierExpression n:
                return _factory.Variable(n.Span, n.Name);

            case UnaryExpression n:
                return DesugarUnary(n);

            case BinaryExpression n:
                return DesugarBinary(n);

            case BlockExpression n:
                ReportDuplicateDefinitions(n.Statements);
                return DesugarStatements(n.Statements, 0, n.Tail, n.Span);

            case IfExpression n:
                return DesugarIf(n);

            case LoopExpression n:
                return _factory.Loop(n.Span, n.Label, DesugarExpression(n.Body), n.LabelSpan);

            case BreakExpression n:
                return _factory.Break(
                    n.Span, n.Label, n.Value is null ? null : DesugarExpression(n.Value), n.LabelSpan);

            case ContinueExpression n:
                return _factory.Continue(n.Span, n.Label, n.LabelSpan);

            case ReturnExpression n:
                return _factory.Return(n.Span, n.Value is null ? null : DesugarExpression(n.Value));

            case ThrowExpression n:
                return _factory.Throw(n.Span, DesugarExpression(n.Value));

            case FunctionExpression n:
                return DesugarFunction(n);

            case CallExpression n:
                return _factory.Call(
                    n.Span,
                    DesugarExpression(n.Callee),
                    [.. n.Arguments.Select(DesugarExpression)]);

            case InstantiateExpression n:
                return _factory.Instantiate(
                    n.Span,
                    DesugarExpression(n.Target),
                    DesugarGenericArguments(n.Arguments));

            case SpanExpression n:
                return _factory.Array(n.Span, [.. n.Elements.Select(DesugarExpression)]);

            // A repetição **não** vira uma lista de `n` elementos aqui: `n` pode
            // não ser conhecido em compilação, e quando é, expandir mil zeros na
            // Core seria trocar um nó por um programa.
            case SpanRepeatExpression n:
                return _factory.SpanRepeat(
                    n.Span,
                    n.Element,
                    DesugarExpression(n.Initializer),
                    DesugarExpression(n.Size));

            // A checagem de limites é semântica do nó Index (spec §41), não uma
            // expansão feita aqui: expandi-la exigiria referenciar `Result` antes
            // da resolução de nomes e tornaria a eliminação de bounds check do
            // partial evaluator uma análise sobre `If` em vez de uma decisão sobre
            // o próprio acesso.
            case IndexExpression n:
                return _factory.Index(n.Span, DesugarExpression(n.Target), DesugarExpression(n.Index));

            case MemberExpression n:
                return _factory.Field(n.Span, DesugarExpression(n.Target), n.Name, n.NameSpan);

            case EnumExpression n:
                return _factory.EnumDef(
                    n.Span,
                    DesugarTypeParameters(n.TypeParameters),
                    [.. n.Variants.Select(v => new CoreVariantDecl(v.Name, v.Payload, v.Span))]);

            case MatchExpression n:
                return _factory.Match(
                    n.Span,
                    DesugarExpression(n.Scrutinee),
                    [.. n.Arms.Select(a => new CoreArm(DesugarPattern(a.Pattern), DesugarExpression(a.Body), a.Span))]);

            case IsExpression n:
                return DesugarIsExpression(n);

            case TypeExpression n:
                return _factory.TypeDef(
                    n.Span,
                    DesugarTypeParameters(n.TypeParameters),
                    [.. n.Fields.Select(f => new CoreFieldDecl(f.Name, f.Type, f.Span))]);

            case ConstructExpression n:
                return _factory.Construct(
                    n.Span,
                    n.TypeName,
                    DesugarGenericArguments(n.TypeArguments),
                    [.. n.Fields.Select(f => new CoreFieldInit(f.Name, DesugarExpression(f.Value), f.Span, f.NameSpan))],
                    n.TypeNameSpan);

            case ErrorExpression n:
                // O parser já reportou; um literal Void mantém a árvore bem-formada.
                return _factory.Unit(n.Span);

            default:
                throw InternalCompilerException.Unreachable(expression, expression.Span);
        }
    }

    /// <summary>
    /// <c>-10</c> vira <c>Literal(-10)</c>. Normalização, não otimização:
    /// <c>- -x</c> continua sendo duas negações.
    /// </summary>
    private CoreExpr DesugarUnary(UnaryExpression node)
    {
        if (node.Operator == UnaryOperator.Negate)
        {
            switch (node.Operand)
            {
                case IntLiteral literal:
                    return _factory.Literal(node.Span, new ConstInt(-literal.Value));

                case FloatLiteral literal:
                    return _factory.Literal(node.Span, new ConstFloat(-literal.Value));
            }
        }

        return _factory.Unary(node.Span, node.Operator, DesugarExpression(node.Operand));
    }

    /// <summary>
    /// <c>&amp;&amp;</c> e <c>||</c> viram <c>If</c>, o que mantém os dois fora da Core
    /// <b>e</b> preserva o curto-circuito — que é semanticamente relevante para o
    /// partial evaluator.
    /// </summary>
    private CoreExpr DesugarBinary(BinaryExpression node)
    {
        // Posição 2 da regra de escopo do `is` (plano 25 §25.3): operando
        // esquerdo de `&&`. `e is Some(v) && resto` liga `v` em `resto` — e
        // adiante, se `resto` for outro `&&` que também liga.
        if (node.Operator == BinaryOperator.AndAlso && HasIsBinding(node.Left))
        {
            return DesugarConditionWithBinding(
                node.Left,
                DesugarExpression(node.Right),
                _factory.Literal(node.Span, ConstBool.False),
                node.Span);
        }

        var left = DesugarExpression(node.Left);
        var right = DesugarExpression(node.Right);

        return node.Operator switch
        {
            BinaryOperator.AndAlso =>
                _factory.If(node.Span, left, right, _factory.Literal(node.Span, ConstBool.False)),

            BinaryOperator.OrElse =>
                _factory.If(node.Span, left, _factory.Literal(node.Span, ConstBool.True), right),

            _ => _factory.Binary(node.Span, node.Operator, left, right, node.OperatorSpan),
        };
    }

    /// <summary>O ramo <c>else</c> é sempre materializado, eliminando um caso de <c>null</c>.</summary>
    private CoreExpr DesugarIf(IfExpression node)
    {
        var then = DesugarExpression(node.Then);

        var otherwise = node.Else is not null
            ? DesugarExpression(node.Else)
            : _factory.Unit(node.Span);

        // Posição 1 da regra de escopo do `is` (plano 25 §25.3): condição de
        // `if`. Só desvia para o caminho de `is` quando há mesmo uma ligação a
        // dar escopo — o `if` comum continua exatamente como sempre foi.
        return HasIsBinding(node.Condition)
            ? DesugarConditionWithBinding(node.Condition, then, otherwise, node.Span)
            : _factory.If(node.Span, DesugarExpression(node.Condition), then, otherwise);
    }

    /// <summary>
    /// A condição de um <c>if</c> (ou o operando esquerdo de um <c>&amp;&amp;</c>,
    /// por <see cref="DesugarBinary"/>) contém, em alguma cadeia de <c>&amp;&amp;</c>
    /// que começa nela, um <c>is</c> com ligação? Puramente estrutural — não
    /// precisa saber se a variante existe, só onde <c>is</c> aparece.
    /// </summary>
    private static bool HasIsBinding(Expression condition) => condition switch
    {
        IsExpression { BindingName: not null } => true,
        BinaryExpression { Operator: BinaryOperator.AndAlso } b => HasIsBinding(b.Left) || HasIsBinding(b.Right),
        _ => false,
    };

    /// <summary>
    /// Desugar de uma condição que pode ligar via <c>is</c> (plano 25 §25.3):
    /// condição de <c>if</c> e, recursivamente, o lado direito de um <c>&amp;&amp;</c>
    /// cujo esquerdo já ligou. Quando não há <c>is</c> nenhum na cadeia, produz
    /// exatamente o <c>If</c> que sempre produziu — este método só existe para
    /// quem já confirmou <see cref="HasIsBinding"/>.
    /// </summary>
    private CoreExpr DesugarConditionWithBinding(
        Expression condition, CoreExpr thenBranch, CoreExpr elseBranch, SourceSpan span)
    {
        switch (condition)
        {
            case IsExpression { BindingName: not null } isExpression:
                return DesugarIsBinding(isExpression, thenBranch, elseBranch, span);

            case BinaryExpression { Operator: BinaryOperator.AndAlso } andExpression:
                return DesugarConditionWithBinding(
                    andExpression.Left,
                    DesugarConditionWithBinding(andExpression.Right, thenBranch, elseBranch, andExpression.Right.Span),
                    elseBranch,
                    span);

            default:
                return _factory.If(span, DesugarExpression(condition), thenBranch, elseBranch);
        }
    }

    /// <summary>
    /// <c>e is Variante(v)</c> numa posição que liga (plano 25 §25.2): vira
    /// <c>match</c> com dois braços — a variante, ligando <c>v</c> em
    /// <paramref name="thenBranch"/>, e o coringa levando a
    /// <paramref name="elseBranch"/>. É o coração da tradução inteira: a
    /// ligação e a prova nascem no mesmo braço.
    /// </summary>
    private CoreExpr DesugarIsBinding(IsExpression node, CoreExpr thenBranch, CoreExpr elseBranch, SourceSpan span)
    {
        var scrutinee = DesugarExpression(node.Scrutinee);
        var pattern = BuildIsVariantPattern(node, includeBinding: true);

        return _factory.Match(
            span,
            scrutinee,
            [
                new CoreArm(pattern, thenBranch, node.Span),
                new CoreArm(new CoreWildcardPattern { Span = span }, elseBranch, span),
            ]);
    }

    /// <summary>
    /// <c>is</c> em qualquer outra posição (plano 25 §25.2, §25.3): a forma sem
    /// ligação, que é <c>Bool</c> em qualquer lugar que <c>Bool</c> vai — e a
    /// forma com ligação fora das duas posições que a admitem, que é
    /// <c>LAP0730</c>. Nos dois casos o resultado é o mesmo <c>match</c> que
    /// devolve <c>true</c>/<c>false</c>; no segundo, a ligação é descartada, e
    /// quem a referenciar adiante ganha <c>LAP0201</c> como qualquer nome que
    /// não existe — ela nunca chegou a existir na Core.
    /// </summary>
    private CoreExpr DesugarIsExpression(IsExpression node)
    {
        if (node.BindingName is not null)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.IsBindingRequiresIfOrAnd,
                node.Span,
                "a ligação de 'is' só vale em condição de 'if' ou à esquerda de '&&'");
        }

        var scrutinee = DesugarExpression(node.Scrutinee);
        var pattern = BuildIsVariantPattern(node, includeBinding: false);

        return _factory.Match(
            node.Span,
            scrutinee,
            [
                new CoreArm(pattern, _factory.Literal(node.Span, ConstBool.True), node.Span),
                new CoreArm(new CoreWildcardPattern { Span = node.Span }, _factory.Literal(node.Span, ConstBool.False), node.Span),
            ]);
    }

    /// <summary>
    /// Monta o <see cref="CoreVariantPattern"/> de um <c>is</c>: resolve dono e
    /// aridade (<see cref="ResolveIsVariant"/>) e, se pedido, acrescenta o
    /// <see cref="CoreBindingPattern"/> da carga — só quando a variante carrega
    /// exatamente um valor (<c>LAP0733</c>/<c>LAP0734</c> cobrem os outros
    /// casos). Em qualquer falha, devolve um padrão sem ligação: o diagnóstico
    /// já saiu, e a árvore continua bem-formada para quem vier depois.
    /// </summary>
    private CorePattern BuildIsVariantPattern(IsExpression node, bool includeBinding)
    {
        var resolved = ResolveIsVariant(node);

        if (resolved is not { } found)
        {
            return new CoreVariantPattern(node.OwnerName ?? "?", node.VariantName, [])
            {
                Span = node.Span,
                VariantSpan = node.VariantSpan,
            };
        }

        if (!includeBinding || node.BindingName is null)
        {
            // Sem ligação, `is` testa só a etiqueta — a carga, se houver, é
            // ignorada. Como `match` exige aridade exata (LAP0264), o padrão
            // leva um coringa por posição de carga: `e is Some` sobre uma
            // variante de um valor vira o mesmo que `Option.Some(_)`, nunca
            // `Option.Some()`.
            return new CoreVariantPattern(
                found.EnumName,
                node.VariantName,
                [.. Enumerable.Repeat((CorePattern)new CoreWildcardPattern { Span = node.Span }, found.Arity)])
            {
                Span = node.Span,
                VariantSpan = node.VariantSpan,
            };
        }

        if (found.Arity == 0)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.IsVariantHasNoPayload,
                node.BindingSpan ?? node.Span,
                $"a variante '{node.VariantName}' não carrega valor");

            return new CoreVariantPattern(found.EnumName, node.VariantName, [])
            {
                Span = node.Span,
                VariantSpan = node.VariantSpan,
            };
        }

        if (found.Arity > 1)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.IsVariantHasMultiplePayloads,
                node.BindingSpan ?? node.Span,
                $"a variante '{node.VariantName}' carrega {found.Arity} valores; escreva um padrão de 'match'");

            return new CoreVariantPattern(found.EnumName, node.VariantName, [])
            {
                Span = node.Span,
                VariantSpan = node.VariantSpan,
            };
        }

        return new CoreVariantPattern(
            found.EnumName,
            node.VariantName,
            [new CoreBindingPattern(node.BindingName) { Span = node.BindingSpan ?? node.Span }])
        {
            Span = node.Span,
            VariantSpan = node.VariantSpan,
        };
    }

    /// <summary>
    /// O dono e a aridade de uma variante de <c>is</c>: se o dono foi escrito,
    /// confere que ele existe e a declara (<c>LAP0732</c> senão); se não foi,
    /// procura entre as conhecidas (<see cref="_variantIndex"/>) uma dona única
    /// — duas ou mais é ambiguidade genuína, e pede a mesma qualificação
    /// (§25.5). Devolve <c>null</c> quando não há como decidir; o diagnóstico
    /// já saiu.
    /// </summary>
    private (string EnumName, int Arity)? ResolveIsVariant(IsExpression node)
    {
        if (!_variantIndex.TryGetValue(node.VariantName, out var candidates) || candidates.Count == 0)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownIsVariant,
                node.VariantSpan,
                $"'{node.VariantName}' não é variante de nenhum enum conhecido");

            return null;
        }

        if (node.OwnerName is not null)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.EnumName == node.OwnerName)
                {
                    return candidate;
                }
            }

            _diagnostics.ReportError(
                DiagnosticCodes.UnknownIsVariant,
                node.VariantSpan,
                $"'{node.VariantName}' não é variante de {node.OwnerName}");

            return null;
        }

        if (candidates.Count > 1)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownIsVariant,
                node.VariantSpan,
                $"'{node.VariantName}' é ambíguo entre "
                + $"{string.Join(", ", candidates.Select(c => c.EnumName))}; qualifique com o nome do enum");

            return null;
        }

        return candidates[0];
    }

    /// <summary>
    /// O corpo <b>não</b> ganha <c>return</c> implícito (spec §12): o valor da
    /// função vem exclusivamente de um <c>Return</c> executado.
    /// </summary>
    private CoreExpr DesugarFunction(FunctionExpression node)
    {
        var parameters = node.Parameters
            .Select(p => new CoreParameter(p.Name, p.Type, p.Span))
            .ToImmutableArray();

        var body = DesugarExpression(node.Body);

        return _factory.Lambda(
            node.Span,
            DesugarTypeParameters(node.TypeParameters),
            parameters,
            node.ReturnType,
            body,
            EndOf(node.Body.Span));
    }

    private static ImmutableArray<CoreTypeParameter> DesugarTypeParameters(
        ImmutableArray<TypeParameterSyntax> parameters) =>
        [.. parameters.Select(p => new CoreTypeParameter(p.Name, p.ConstType, p.Span))];

    /// <summary>
    /// Um argumento genérico atravessa o desugar quase intacto: só o caso de valor
    /// vira Core, porque é o único que contém uma expressão. A escolha entre tipo e
    /// constante para um identificador nu é do checker, que conhece o escopo.
    /// </summary>
    private ImmutableArray<CoreGenericArgument> DesugarGenericArguments(
        ImmutableArray<GenericArgumentSyntax> arguments) =>
        [.. arguments.Select<GenericArgumentSyntax, CoreGenericArgument>(a => a switch
        {
            TypeArgumentSyntax t => new CoreTypeArgument(t.Type) { Span = t.Span },
            NameArgumentSyntax n => new CoreNameArgument(n.Name) { Span = n.Span },
            ValueArgumentSyntax v => new CoreValueArgument(DesugarExpression(v.Value)) { Span = v.Span },
            _ => throw InternalCompilerException.Unreachable(a, a.Span),
        })];

    /// <summary>
    /// Normalização de padrões. Totalmente sintática: com Q3, um identificador
    /// sozinho é sempre binding e <c>A.B</c> é sempre variante — o checker não
    /// precisa desempatar nada.
    /// </summary>
    private static CorePattern DesugarPattern(Pattern pattern) => pattern switch
    {
        WildcardPattern p => new CoreWildcardPattern { Span = p.Span },
        BindingPattern p => new CoreBindingPattern(p.Name) { Span = p.Span },
        LiteralPattern p => new CoreLiteralPattern(p.Value) { Span = p.Span },
        VariantPattern p => new CoreVariantPattern(
            p.EnumName,
            p.VariantName,
            [.. p.Arguments.Select(DesugarPattern)])
        {
            Span = p.Span,
            VariantSpan = p.VariantSpan,
        },
        _ => throw InternalCompilerException.Unreachable(pattern, pattern.Span),
    };

    /// <summary>
    /// Span de comprimento zero no fim de uma construção. Nós introduzidos apontam
    /// para a construção que os originou, nunca para lugar nenhum (plano 05 §5.3).
    /// </summary>
    private static SourceSpan EndOf(SourceSpan span) => new(Math.Max(span.End - 1, span.Start), 1);
}

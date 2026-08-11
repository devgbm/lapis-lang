using System.Collections.Immutable;
using Lapis.Ast;
using Lapis.Ast.Core;
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

    private Desugarer(DiagnosticBag diagnostics) => _diagnostics = diagnostics;

    public static CoreProgram Desugar(SourceFile file, DiagnosticBag diagnostics)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var desugarer = new Desugarer(diagnostics);
        desugarer.ReportDuplicateDefinitions(file.Statements);
        var body = desugarer.DesugarStatements(file.Statements, 0, tail: null, file.Span);

        return desugarer._factory.Program(body);
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

            if (seen.TryGetValue(def.Name, out var previous))
            {
                _diagnostics.ReportError(
                    DiagnosticCodes.DuplicateDefinition,
                    def.NameSpan,
                    $"'{def.Name}' já foi definido neste escopo",
                    new DiagnosticNote("definição anterior", previous.NameSpan));
            }
            else
            {
                seen[def.Name] = def;
            }
        }
    }

    /// <summary>
    /// Sequenciamento: <c>def x = e; resto</c> vira <c>Let(x, e, resto)</c> e
    /// <c>e; resto</c> vira <c>Let($tmpN, e, resto)</c>. A ausência de cauda produz
    /// <c>()</c> (spec §9).
    ///
    /// Quando a sequência contém <c>label</c>, ela é decomposta em blocos básicos
    /// primeiro (<see cref="DesugarWithLabels"/>).
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

        // Um `label` daqui para a frente muda a forma do que vem depois: o resto
        // da sequência vira um grupo de join points, não uma cadeia de `Let`.
        if (statement is LabelStatement or GotoStatement && HasLabelFrom(statements, index))
        {
            return DesugarWithLabels(statements, index, tail, enclosingSpan);
        }

        var rest = DesugarStatements(statements, index + 1, tail, enclosingSpan);

        return statement switch
        {
            DefStatement def => _factory.Let(
                def.Span,
                def.Name,
                def.Annotation,
                DesugarExpression(def.Value),
                rest,
                isSynthetic: false,
                def.NameSpan,
                def.IsMutable),

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

            // Um `goto` sem `label` correspondente neste bloco salta para um grupo
            // externo: aqui ele é só mais um statement, e a completion sobe.
            GotoStatement jump => _factory.Let(
                jump.Span,
                _names.Next(),
                annotation: null,
                DesugarGoto(jump),
                rest,
                isSynthetic: true),

            // `label` sem nenhum `goto`: o grupo existe mesmo assim, com um join
            // só alcançável pela queda natural — tratado por DesugarWithLabels.
            _ => throw InternalCompilerException.Unreachable(statement, statement.Span),
        };
    }

    private static bool HasLabelFrom(ImmutableArray<Statement> statements, int index)
    {
        for (var i = index; i < statements.Length; i++)
        {
            if (statements[i] is LabelStatement)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Decomposição em blocos básicos (plano 16 §16.4).
    ///
    /// A sequência a partir do primeiro salto é partida em segmentos por
    /// <c>label</c>; cada segmento vira um join, e um <c>label</c> encerra o
    /// segmento anterior com um <c>Goto</c> implícito — o que torna a decomposição
    /// um sufixo, e não uma cópia.
    ///
    /// O que veio antes do primeiro salto continua envolvendo o <c>Labeled</c> como
    /// <c>Let</c> comum, e portanto continua em escopo nos dois lados. Já o que é
    /// declarado <b>entre</b> o salto e o rótulo não está em escopo no destino —
    /// não é limitação, é a verdade: o salto pode ter pulado a declaração.
    /// </summary>
    private CoreExpr DesugarWithLabels(
        ImmutableArray<Statement> statements,
        int index,
        Expression? tail,
        SourceSpan enclosingSpan)
    {
        // Fronteiras dos segmentos: [index, l1), [l1+1, l2), ... até o fim.
        var labels = new List<int>();

        for (var i = index; i < statements.Length; i++)
        {
            if (statements[i] is LabelStatement)
            {
                labels.Add(i);
            }
        }

        var span = SourceSpan.FromBounds(statements[index].Span.Start, enclosingSpan.End);

        var entry = DesugarSegment(
            statements,
            index,
            labels[0],
            NameOf(statements[labels[0]]),
            statements[labels[0]].Span,
            tail: null,
            enclosingSpan);

        var joins = ImmutableArray.CreateBuilder<CoreJoin>(labels.Count);

        for (var i = 0; i < labels.Count; i++)
        {
            var label = (LabelStatement)statements[labels[i]];
            var last = i == labels.Count - 1;
            var end = last ? statements.Length : labels[i + 1];

            var body = DesugarSegment(
                statements,
                labels[i] + 1,
                end,
                last ? null : NameOf(statements[labels[i + 1]]),
                last ? enclosingSpan : statements[labels[i + 1]].Span,
                last ? tail : null,
                enclosingSpan);

            joins.Add(new CoreJoin(label.Label, body, label.Span) { NameSpan = label.LabelSpan });
        }

        return _factory.Labeled(span, entry, joins.MoveToImmutable());

        static string NameOf(Statement statement) => ((LabelStatement)statement).Label;
    }

    /// <summary>
    /// Um segmento: os statements em <c>[start, end)</c> terminados pelo salto
    /// implícito para <paramref name="fallThrough"/>, ou pela cauda do bloco quando
    /// é o último.
    /// </summary>
    private CoreExpr DesugarSegment(
        ImmutableArray<Statement> statements,
        int start,
        int end,
        string? fallThrough,
        SourceSpan fallThroughSpan,
        Expression? tail,
        SourceSpan enclosingSpan)
    {
        var terminator = fallThrough is not null
            ? _factory.Goto(fallThroughSpan, fallThrough, fallThroughSpan, isImplicit: true)
            : tail is not null
                ? DesugarExpression(tail)
                : _factory.Unit(EndOf(enclosingSpan));

        // De trás para a frente: cada statement envolve o que já foi montado.
        for (var i = end - 1; i >= start; i--)
        {
            terminator = statements[i] switch
            {
                DefStatement def => _factory.Let(
                    def.Span,
                    def.Name,
                    def.Annotation,
                    DesugarExpression(def.Value),
                    terminator,
                    isSynthetic: false,
                    def.NameSpan,
                    def.IsMutable),

                ExpressionStatement expression => _factory.Let(
                    expression.Span,
                    _names.Next(),
                    annotation: null,
                    DesugarExpression(expression.Expression),
                    terminator,
                    isSynthetic: true),

                AssignStatement assign => _factory.Let(
                    assign.Span,
                    _names.Next(),
                    annotation: null,
                    DesugarAssign(assign),
                    terminator,
                    isSynthetic: true),

                GotoStatement jump => _factory.Let(
                    jump.Span,
                    _names.Next(),
                    annotation: null,
                    DesugarGoto(jump),
                    terminator,
                    isSynthetic: true),

                var other => throw InternalCompilerException.Unreachable(other, other.Span),
            };
        }

        return terminator;
    }

    private CoreExpr DesugarAssign(AssignStatement node) =>
        _factory.Assign(node.Span, node.Name, DesugarExpression(node.Value), node.NameSpan);

    private CoreExpr DesugarGoto(GotoStatement node) =>
        node.Condition is null
            ? _factory.Goto(node.Span, node.Label, node.LabelSpan)
            : _factory.GotoIf(node.Span, node.Label, DesugarExpression(node.Condition), node.LabelSpan);

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

            case ReturnExpression n:
                return _factory.Return(n.Span, n.Value is null ? null : DesugarExpression(n.Value));

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

            case ArrayExpression n:
                return _factory.Array(n.Span, [.. n.Elements.Select(DesugarExpression)]);

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
        var condition = DesugarExpression(node.Condition);
        var then = DesugarExpression(node.Then);

        var otherwise = node.Else is not null
            ? DesugarExpression(node.Else)
            : _factory.Unit(node.Span);

        return _factory.If(node.Span, condition, then, otherwise);
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

using System.Collections.Immutable;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;
using Lapis.Lexer;

namespace Lapis.Macros;

/// <summary>
/// <c>Surface AST → Surface AST</c>: reconhece invocações <c>@nome</c>, casa
/// padrões e expande para sintaxe, com higiene (plano 17).
///
/// A expansão é um <b>endomorfismo</b> de propósito. Expandir direto para a Core
/// pareceria mais curto e seria pior: sumiria o <c>lapis expand</c>, o corpo do
/// <c>expand</c> teria de ser escrito em termos de <c>Let</c> em vez de código
/// normal, e a validação de contexto (expressão × statement) deixaria de existir.
/// </summary>
public sealed class MacroExpander
{
    /// <summary>
    /// Uma macro pode expandir para código que usa outras macros. Sem recursão
    /// (Q8) uma macro não se invoca, mas ciclos indiretos ainda são construíveis —
    /// mesma postura do limite de 200 do parser e dos 10.000 de profundidade de
    /// chamada.
    /// </summary>
    private const int MaxDepth = 64;

    private readonly MacroRegistry _registry = new();
    private readonly DiagnosticBag _diagnostics;
    private readonly IConstraintRunner? _constraints;
    private int _mark;

    private MacroExpander(DiagnosticBag diagnostics, IConstraintRunner? constraints)
    {
        _diagnostics = diagnostics;
        _constraints = constraints;
    }

    /// <summary>
    /// Expande o arquivo inteiro. Um arquivo sem <c>@</c> sai idêntico — é o que
    /// garante que esta fase não pode quebrar nada do que já existia.
    /// </summary>
    /// <param name="constraints">
    /// Quem executa os <c>constraint</c> (plano 18). <c>null</c> significa "esta
    /// expansão não tem compile time": macros sem <c>constraint</c> funcionam
    /// normalmente, e encontrar uma <b>com</b> constraint é erro interno, não
    /// silêncio.
    /// </param>
    /// <param name="preludeMacros">
    /// As macros do <c>prelude.ls</c> (plano 20), disponíveis em todo arquivo sem
    /// declaração. Uma macro homônima no arquivo as sombreia.
    /// </param>
    public static SourceFile Expand(
        SourceFile file,
        DiagnosticBag diagnostics,
        IConstraintRunner? constraints = null,
        IEnumerable<MacroDeclaration>? preludeMacros = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var expander = new MacroExpander(diagnostics, constraints);

        if (preludeMacros is not null)
        {
            expander._registry.RegisterPrelude(preludeMacros);
        }

        var statements = expander.ExpandStatements(file.Statements, depth: 0);

        return file with { Statements = statements };
    }

    // ------------------------------------------------------------ statements

    /// <summary>
    /// Macros são registradas na ordem em que aparecem e valem do ponto da
    /// declaração em diante — a mesma regra de <c>def</c> (Q8).
    /// </summary>
    private ImmutableArray<Statement> ExpandStatements(ImmutableArray<Statement> statements, int depth)
    {
        var result = ImmutableArray.CreateBuilder<Statement>(statements.Length);

        foreach (var statement in statements)
        {
            if (statement is MacroDeclaration declaration)
            {
                _registry.TryRegister(declaration, _diagnostics);

                // A declaração some do programa: ela não é código, é sintaxe.
                continue;
            }

            result.AddRange(ExpandStatement(statement, depth));
        }

        return result.ToImmutable();
    }

    private IEnumerable<Statement> ExpandStatement(Statement statement, int depth)
    {
        // Uma invocação em posição de statement pode render vários statements.
        if (statement is ExpressionStatement { Expression: MacroInvocation invocation })
        {
            return ExpandInvocation(invocation, depth, asExpression: false) is { } expanded
                ? expanded.Statements.Concat(TailAsStatement(expanded))
                : [statement];
        }

        return [Rewrite(statement, depth)];
    }

    private static IEnumerable<Statement> TailAsStatement(BlockExpression block) =>
        block.Tail is null ? [] : [new ExpressionStatement(block.Tail) { Span = block.Tail.Span }];

    // ----------------------------------------------------------- invocação

    /// <summary>
    /// Casa, valida o contexto e expande. Devolve <c>null</c> quando algo falhou —
    /// o diagnóstico já foi reportado e o nó original fica, para que o resto do
    /// arquivo continue sendo analisado.
    /// </summary>
    private BlockExpression? ExpandInvocation(MacroInvocation invocation, int depth, bool asExpression)
    {
        if (depth >= MaxDepth)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.MacroExpansionTooDeep,
                invocation.Span,
                $"expansão de macro profunda demais (limite {MaxDepth})",
                new DiagnosticNote("macros que se invocam em ciclo não terminam"));

            return null;
        }

        if (!_registry.TryLookup(invocation.Name, out var declaration))
        {
            _diagnostics.ReportError(
                DiagnosticCodes.UnknownMacro,
                invocation.NameSpan,
                $"a macro '@{invocation.Name}' não existe");

            return null;
        }

        var matches = new List<(MacroRule Rule, MatchResult Match)>();

        // Todas as regras são testadas, e o resultado tem de ser exatamente uma.
        // A proposta original pedia ordem **e** detecção de ambiguidade — as duas
        // não convivem, e falhar ruidosamente é o que a 0.2 já faz com `a < b < c`.
        foreach (var rule in declaration.Rules)
        {
            if (MacroMatcher.Match(rule.Pattern, invocation.Arguments) is { } match)
            {
                matches.Add((rule, match));
            }
        }

        if (matches.Count == 0)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.NoMacroRuleMatches,
                invocation.Span,
                $"nenhum padrão de '@{invocation.Name}' corresponde a esta invocação",
                [.. declaration.Rules.Select(r => new DiagnosticNote(
                    "padrão: " + Ast.Printing.SurfaceSExprPrinter.PrintPattern(r.Pattern), r.Span))]);

            return null;
        }

        if (matches.Count > 1)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.AmbiguousMacroRules,
                invocation.Span,
                $"mais de um padrão de '@{invocation.Name}' corresponde a esta invocação",
                [.. matches.Select(m => new DiagnosticNote(
                    "padrão: " + Ast.Printing.SurfaceSExprPrinter.PrintPattern(m.Rule.Pattern), m.Rule.Span))]);

            return null;
        }

        var (selected, bindings) = matches[0];

        if (!RunConstraint(selected, bindings, invocation))
        {
            return null;
        }

        var substitution = new Substitution(bindings.Bindings, ++_mark, invocation.Span, selected.Expansion);
        var body = (BlockExpression)substitution.Apply(selected.Expansion);

        if (asExpression && body.Tail is null)
        {
            _diagnostics.ReportError(
                DiagnosticCodes.MacroExpansionWrongContext,
                invocation.Span,
                $"'@{invocation.Name}' foi usada em posição de expressão, mas o 'expand' não produz um valor",
                new DiagnosticNote("um 'expand' em posição de expressão precisa terminar numa expressão", selected.Span));

            return null;
        }

        // O resultado pode conter novas invocações: expande de novo.
        return body with
        {
            Statements = ExpandStatements(body.Statements, depth + 1),
            Tail = body.Tail is null ? null : RewriteExpression(body.Tail, depth + 1),
        };
    }

    /// <summary>
    /// Roda o <c>constraint</c> da regra escolhida, entre o <c>match</c> e o
    /// <c>expand</c> (spec de macros §8). Devolve se a expansão pode prosseguir.
    ///
    /// Rejeitar aqui é <b>definitivo</b>: nenhuma outra regra é tentada. Uma
    /// regra que não casa diz "não é esta a forma"; uma constraint que rejeita diz
    /// "esta forma está errada", e tentar outra leitura depois disso só produziria
    /// um segundo erro pior (§7.1).
    /// </summary>
    private bool RunConstraint(MacroRule rule, MatchResult bindings, MacroInvocation invocation)
    {
        if (rule.Constraint is null)
        {
            return true;
        }

        if (_constraints is null)
        {
            throw new InternalCompilerException(
                $"'@{invocation.Name}' tem constraint, mas a expansão não recebeu executor");
        }

        var outcome = _constraints.Run(rule.Constraint, bindings, invocation.Span);

        switch (outcome.Status)
        {
            case ConstraintStatus.Accepted:
                return true;

            // A mensagem é do programa e o span é o da **invocação**: quem
            // escreveu `@post "/products"` precisa ver a sua linha, não a da
            // macro. Onde a rejeição nasceu vira nota.
            case ConstraintStatus.Rejected:
                _diagnostics.ReportError(
                    DiagnosticCodes.ConstraintRejected,
                    invocation.Span,
                    outcome.Message!,
                    outcome.Span is { } origin
                        ? [new DiagnosticNote($"rejeitado pela constraint de '@{invocation.Name}'", origin)]
                        : []);

                return false;

            // A constraint em si não compilou; os diagnósticos dela já foram
            // reportados por quem a rodou.
            default:
                return false;
        }
    }

    // ----------------------------------------------- reescrita da árvore

    /// <summary>
    /// Percorre a árvore procurando invocações. Tudo o mais atravessa intacto — é
    /// o que faz um programa sem <c>@</c> sair idêntico da expansão.
    /// </summary>
    private Statement Rewrite(Statement statement, int depth) => statement switch
    {
        DefStatement s => s with { Value = RewriteExpression(s.Value, depth) },
        AssignStatement s => s with { Value = RewriteExpression(s.Value, depth) },
        ExpressionStatement s => s with { Expression = RewriteExpression(s.Expression, depth) },
        GotoStatement { Condition: not null } s => s with { Condition = RewriteExpression(s.Condition, depth) },
        _ => statement,
    };

    private Expression RewriteExpression(Expression expression, int depth)
    {
        switch (expression)
        {
            case MacroInvocation invocation:
                var expanded = ExpandInvocation(invocation, depth, asExpression: true);

                if (expanded is null)
                {
                    return invocation;
                }

                // Um `expand` de statement único vira a própria expressão; vários
                // viram um bloco com cauda, que é expressão do mesmo jeito.
                return expanded.Statements.IsEmpty && expanded.Tail is not null
                    ? expanded.Tail
                    : expanded;

            case BlockExpression block:
                return block with
                {
                    Statements = ExpandStatements(block.Statements, depth),
                    Tail = block.Tail is null ? null : RewriteExpression(block.Tail, depth),
                };

            case UnaryExpression n:
                return n with { Operand = RewriteExpression(n.Operand, depth) };

            case BinaryExpression n:
                return n with
                {
                    Left = RewriteExpression(n.Left, depth),
                    Right = RewriteExpression(n.Right, depth),
                };

            case IfExpression n:
                return n with
                {
                    Condition = RewriteExpression(n.Condition, depth),
                    Then = (BlockExpression)RewriteExpression(n.Then, depth),
                    Else = n.Else is null ? null : RewriteExpression(n.Else, depth),
                };

            case ReturnExpression { Value: not null } n:
                return n with { Value = RewriteExpression(n.Value, depth) };

            case ThrowExpression n:
                return n with { Value = RewriteExpression(n.Value, depth) };

            case FunctionExpression n:
                return n with { Body = (BlockExpression)RewriteExpression(n.Body, depth) };

            case CallExpression n:
                return n with
                {
                    Callee = RewriteExpression(n.Callee, depth),
                    Arguments = [.. n.Arguments.Select(a => RewriteExpression(a, depth))],
                };

            case InstantiateExpression n:
                return n with { Target = RewriteExpression(n.Target, depth) };

            case ArrayExpression n:
                return n with { Elements = [.. n.Elements.Select(e => RewriteExpression(e, depth))] };

            case IndexExpression n:
                return n with
                {
                    Target = RewriteExpression(n.Target, depth),
                    Index = RewriteExpression(n.Index, depth),
                };

            case MemberExpression n:
                return n with { Target = RewriteExpression(n.Target, depth) };

            case MatchExpression n:
                return n with
                {
                    Scrutinee = RewriteExpression(n.Scrutinee, depth),
                    Arms = [.. n.Arms.Select(a => a with { Body = RewriteExpression(a.Body, depth) })],
                };

            case ConstructExpression n:
                return n with
                {
                    Fields = [.. n.Fields.Select(f => f with { Value = RewriteExpression(f.Value, depth) })],
                };

            default:
                return expression;
        }
    }
}

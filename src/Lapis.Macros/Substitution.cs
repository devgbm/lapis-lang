using System.Collections.Immutable;
using Lapis.Ast.Surface;
using Lapis.Lexer;

namespace Lapis.Macros;

/// <summary>
/// Substitui capturas pelo que foi casado, e renomeia o que a macro introduziu.
///
/// <b>Higiene</b> (spec de macros §9): cada expansão recebe uma marca, e todo
/// identificador que aparece no <c>expand</c> e <b>não</b> vem de captura é
/// renomeado para <c>nome@marca</c>. O <c>@</c> não é lexável dentro de um
/// identificador, então colisão com nome do usuário é impossível — mesma técnica
/// dos nomes sintéticos que o desugar já gera.
///
/// Identificadores <b>vindos de captura</b> não são tocados: eles carregam o
/// contexto léxico de quem invocou, que é exatamente o requisito.
/// </summary>
internal sealed class Substitution
{
    private readonly ImmutableDictionary<string, MacroBinding> _bindings;
    private readonly int _mark;
    private readonly Diagnostics.SourceSpan _invocation;

    /// <summary>
    /// Nomes que o <c>expand</c> <b>liga</b> — os `def`, `var` e `label` escritos
    /// dentro dele. São esses, e só esses, que a higiene renomeia.
    ///
    /// Um nome apenas <b>referenciado</b> (`print`, uma função do programa) fica
    /// intacto: ele não foi introduzido pela macro, e resolvê-lo é problema do
    /// escopo de quem invocou.
    /// </summary>
    private readonly HashSet<string> _introduced;

    private readonly Dictionary<string, string> _renamed = new(StringComparer.Ordinal);

    public Substitution(
        ImmutableDictionary<string, MacroBinding> bindings,
        int mark,
        Diagnostics.SourceSpan invocation,
        BlockExpression expansion)
    {
        _bindings = bindings;
        _mark = mark;
        _invocation = invocation;
        _introduced = BoundNames.Of(expansion);
        _introduced.ExceptWith(bindings.Keys);
    }

    public SurfaceNode Apply(SurfaceNode node) => node switch
    {
        Statement s => ApplyStatement(s),
        Expression e => ApplyExpression(e),
        _ => node,
    };

    private Statement ApplyStatement(Statement statement) => statement switch
    {
        // O nome de um `def` dentro do `expand` foi **introduzido** pela macro:
        // é ele que a higiene precisa renomear.
        DefStatement s => s with
        {
            Name = Rename(s.Name),
            Value = ApplyExpression(s.Value),
            Annotation = s.Annotation is null ? null : ApplyType(s.Annotation),
            Span = _invocation,
        },

        AssignStatement s => s with { Name = Rename(s.Name), Value = ApplyExpression(s.Value), Span = _invocation },

        ExpressionStatement s => s with { Expression = ApplyExpression(s.Expression), Span = _invocation },

        // Rótulos também são introduzidos pela macro, e `@unless` usada duas vezes
        // no mesmo bloco não pode declarar o mesmo `done` duas vezes.
        GotoStatement s => s with
        {
            Label = Rename(s.Label),
            Condition = s.Condition is null ? null : ApplyExpression(s.Condition),
            Span = _invocation,
        },

        LabelStatement s => s with { Label = Rename(s.Label), Span = _invocation },

        _ => statement,
    };

    private Expression ApplyExpression(Expression expression)
    {
        switch (expression)
        {
            // O caso central: um identificador que nomeia uma captura vira a
            // árvore capturada, com o span de onde ela foi escrita.
            case IdentifierExpression identifier
                when _bindings.TryGetValue(identifier.Name, out var binding):
                return AsExpression(binding, identifier);

            case IdentifierExpression identifier:
                return identifier with { Name = Introduce(identifier.Name), Span = _invocation };

            case BlockExpression block:
                return block with
                {
                    Statements = [.. block.Statements.SelectMany(ApplyStatementSpliced)],
                    Tail = block.Tail is null ? null : ApplyExpression(block.Tail),
                    Span = _invocation,
                };

            // Uma invocação aninhada ainda não tem árvore: o que se substitui aqui
            // são os **tokens**, que é a razão de cada captura guardar os seus.
            case MacroInvocation nested:
                return nested with { Arguments = SubstituteTokens(nested.Arguments), Span = _invocation };

            case UnaryExpression n:
                return n with { Operand = ApplyExpression(n.Operand), Span = _invocation };

            case BinaryExpression n:
                return n with
                {
                    Left = ApplyExpression(n.Left),
                    Right = ApplyExpression(n.Right),
                    Span = _invocation,
                };

            case IfExpression n:
                return n with
                {
                    Condition = ApplyExpression(n.Condition),
                    Then = (BlockExpression)ApplyExpression(n.Then),
                    Else = n.Else is null ? null : ApplyExpression(n.Else),
                    Span = _invocation,
                };

            case ReturnExpression n:
                return n with
                {
                    Value = n.Value is null ? null : ApplyExpression(n.Value),
                    Span = _invocation,
                };

            case ThrowExpression n:
                return n with { Value = ApplyExpression(n.Value), Span = _invocation };

            case FunctionExpression n:
                return n with { Body = (BlockExpression)ApplyExpression(n.Body), Span = _invocation };

            case CallExpression n:
                return n with
                {
                    Callee = ApplyExpression(n.Callee),
                    Arguments = [.. n.Arguments.Select(ApplyExpression)],
                    Span = _invocation,
                };

            case InstantiateExpression n:
                return n with { Target = ApplyExpression(n.Target), Span = _invocation };

            case ArrayExpression n:
                return n with { Elements = [.. n.Elements.Select(ApplyExpression)], Span = _invocation };

            case IndexExpression n:
                return n with
                {
                    Target = ApplyExpression(n.Target),
                    Index = ApplyExpression(n.Index),
                    Span = _invocation,
                };

            case MemberExpression n:
                return n with { Target = ApplyExpression(n.Target), Span = _invocation };

            case MatchExpression n:
                return n with
                {
                    Scrutinee = ApplyExpression(n.Scrutinee),
                    Arms = [.. n.Arms.Select(a => a with { Body = ApplyExpression(a.Body) })],
                    Span = _invocation,
                };

            case ConstructExpression n:
                return n with
                {
                    Fields = [.. n.Fields.Select(f => f with { Value = ApplyExpression(f.Value) })],
                    Span = _invocation,
                };

            default:
                return expression with { Span = _invocation };
        }
    }

    /// <summary>
    /// Um statement do <c>expand</c> pode virar <b>vários</b>: é o que acontece
    /// quando uma captura de bloco aparece sozinha, como o <c>body;</c> de
    /// <c>@unless</c>. Sem isto o bloco viraria um escopo aninhado, e um
    /// <c>def</c> lá dentro deixaria de ser visível depois.
    /// </summary>
    private IEnumerable<Statement> ApplyStatementSpliced(Statement statement)
    {
        if (statement is ExpressionStatement { Expression: IdentifierExpression name }
            && _bindings.TryGetValue(name.Name, out var binding)
            && binding is SingleBinding { Node: BlockExpression block })
        {
            return block.Statements.Concat(
                block.Tail is null ? [] : (Statement[])[new ExpressionStatement(block.Tail) { Span = block.Tail.Span }]);
        }

        return [ApplyStatement(statement)];
    }

    private Expression AsExpression(MacroBinding binding, IdentifierExpression original) => binding switch
    {
        SingleBinding { Node: Expression expression } => expression,

        // Um `Statement:s` ou um `Type:t` em posição de expressão não tem como
        // virar valor; deixar o identificador original faz o erro aparecer no
        // checker, com o nome que o autor escreveu.
        _ => original,
    };

    private TypeSyntax ApplyType(TypeSyntax type) => type switch
    {
        NamedTypeSyntax named when _bindings.TryGetValue(named.Name, out var binding)
            && binding is SingleBinding { Node: TypeSyntax captured } => captured,

        ArrayTypeSyntax n => n with { Element = ApplyType(n.Element) },

        FunctionTypeSyntax n => n with
        {
            Parameters = [.. n.Parameters.Select(ApplyType)],
            Return = ApplyType(n.Return),
        },

        _ => type,
    };

    /// <summary>
    /// Troca, token a token, cada identificador que nomeia uma captura pelos
    /// tokens que ela consumiu.
    /// </summary>
    private ImmutableArray<Token> SubstituteTokens(ImmutableArray<Token> tokens)
    {
        var result = ImmutableArray.CreateBuilder<Token>(tokens.Length);

        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.Identifier && _bindings.TryGetValue(token.Text, out var binding))
            {
                result.AddRange(binding.Tokens);
                continue;
            }

            result.Add(token);
        }

        return result.ToImmutable();
    }

    /// <summary>
    /// O nome de um <c>def</c>, de um <c>var</c> ou de um rótulo dentro do
    /// <c>expand</c>.
    ///
    /// Quando ele <b>é</b> uma captura, o nome vem de quem invocou — é o que faz
    /// <c>match Identifier:i ... expand { def i = ...; }</c> declarar o nome que o
    /// autor escreveu, e não a letra que a macro usou. Nos demais casos, higiene.
    /// </summary>
    private string Rename(string name) =>
        _bindings.TryGetValue(name, out var binding)
        && binding is SingleBinding { Node: IdentifierExpression captured }
            ? captured.Name
            : Introduce(name);

    /// <summary>
    /// <c>nome</c> → <c>nome@marca</c>, quando o <c>expand</c> liga aquele nome.
    /// Um nome já renomeado nesta expansão mantém o mesmo destino, para que
    /// <c>def temp</c> e o <c>temp</c> que o usa continuem sendo o mesmo nome.
    /// </summary>
    private string Introduce(string name)
    {
        if (!_introduced.Contains(name))
        {
            return name;
        }

        if (_renamed.TryGetValue(name, out var renamed))
        {
            return renamed;
        }

        var fresh = $"{name}@{_mark}";
        _renamed[name] = fresh;
        return fresh;
    }
}

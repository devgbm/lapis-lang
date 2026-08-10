using System.Collections.Immutable;
using Lapis.Diagnostics;

namespace Lapis.Ast.Surface;

/// <summary>
/// Surface AST — espelha a sintaxe escrita, saída do parser.
///
/// São <c>record</c> porque igualdade estrutural facilita os testes de parser
/// (plano 02 §2.1). A Core AST, ao contrário, usa classes com identidade.
/// </summary>
public abstract record SurfaceNode
{
    public required SourceSpan Span { get; init; }
}

// ---------------------------------------------------------------- arquivo

public sealed record SourceFile(ImmutableArray<Statement> Statements) : SurfaceNode;

// ------------------------------------------------------------- statements

public abstract record Statement : SurfaceNode;

/// <summary>A única forma de introduzir um nome (spec §2, §8).</summary>
public sealed record DefStatement(string Name, TypeSyntax? Annotation, Expression Value) : Statement
{
    public required SourceSpan NameSpan { get; init; }
}

public sealed record ExpressionStatement(Expression Expression) : Statement;

/// <summary>
/// <c>goto L;</c> ou <c>goto L if e;</c>.
///
/// É <c>Statement</c>, não <c>Expression</c>: um salto não produz valor, e
/// mantê-lo fora da gramática de expressão elimina <c>def x = goto L;</c> sem
/// precisar de regra (plano 16 §"Por que Statement e não Expression").
/// </summary>
public sealed record GotoStatement(string Label, Expression? Condition) : Statement
{
    public required SourceSpan LabelSpan { get; init; }
}

/// <summary>
/// <c>label L;</c> — o destino de um <c>goto</c>, local à função que o contém.
/// </summary>
public sealed record LabelStatement(string Label) : Statement
{
    public required SourceSpan LabelSpan { get; init; }
}

// ------------------------------------------------------------ expressions

public abstract record Expression : SurfaceNode;

public sealed record IntLiteral(long Value, string RawText) : Expression;

public sealed record FloatLiteral(double Value, string RawText) : Expression;

public sealed record BoolLiteral(bool Value) : Expression;

public sealed record StrLiteral(string Value) : Expression;

/// <summary>O literal <c>()</c>, de tipo <c>Void</c> (spec §6).</summary>
public sealed record UnitLiteral : Expression;

public sealed record IdentifierExpression(string Name) : Expression;

public sealed record UnaryExpression(UnaryOperator Operator, Expression Operand) : Expression;

public sealed record BinaryExpression(BinaryOperator Operator, Expression Left, Expression Right) : Expression
{
    public required SourceSpan OperatorSpan { get; init; }
}

public sealed record BlockExpression(ImmutableArray<Statement> Statements, Expression? Tail) : Expression;

public sealed record IfExpression(Expression Condition, BlockExpression Then, Expression? Else) : Expression;

public sealed record ReturnExpression(Expression? Value) : Expression;

public sealed record FunctionExpression(
    ImmutableArray<TypeParameterSyntax> TypeParameters,
    ImmutableArray<ParameterSyntax> Parameters,
    TypeSyntax? ReturnType,
    BlockExpression Body) : Expression;

public sealed record CallExpression(Expression Callee, ImmutableArray<Expression> Arguments) : Expression;

/// <summary>
/// <c>alvo&lt;A, B&gt;</c> — aplicação de argumentos genéricos em posição de
/// expressão: <c>identity&lt;Int&gt;</c>, <c>Result&lt;Int, IndexError&gt;</c>.
///
/// É pós-fixo e independente da chamada, o que faz uma única produção cobrir os
/// dois usos: <c>identity&lt;Int&gt;(10)</c> é <c>Call(Instantiate(...))</c> e
/// <c>Result&lt;Int, E&gt;.Ok(1)</c> é <c>Call(Member(Instantiate(...)))</c>.
/// </summary>
public sealed record InstantiateExpression(
    Expression Target,
    ImmutableArray<GenericArgumentSyntax> Arguments) : Expression;

public sealed record ArrayExpression(ImmutableArray<Expression> Elements) : Expression;

public sealed record IndexExpression(Expression Target, Expression Index) : Expression;

/// <summary>
/// <c>alvo.nome</c> — acesso a campo de <c>type</c> e acesso a variante de enum
/// (<c>Result.Ok</c>), que Q3 tornou a única forma de nomear uma variante.
/// </summary>
public sealed record MemberExpression(Expression Target, string Name) : Expression
{
    public required SourceSpan NameSpan { get; init; }
}

/// <summary>
/// <c>match e { padrão =&gt; corpo, ... }</c>. Deve ser exaustivo (Q6): como é uma
/// expressão, precisa produzir um valor em toda execução.
/// </summary>
public sealed record MatchExpression(
    Expression Scrutinee,
    ImmutableArray<MatchArm> Arms) : Expression;

/// <summary>Declaração de tipo. Não tem nome próprio: o nome vem do <c>def</c> (spec §14).</summary>
public sealed record TypeExpression(
    ImmutableArray<TypeParameterSyntax> TypeParameters,
    ImmutableArray<FieldSyntax> Fields) : Expression;

/// <summary>
/// <c>.Nome { campo: valor }</c> — construção de instância (Q2).
///
/// O ponto inicial não é decoração: como nenhuma outra expressão começa com
/// <c>.</c>, o parser distingue bloco de construção olhando um único token, e a
/// condição de <c>if</c>/<c>match</c> não precisa de regra contextual.
/// </summary>
public sealed record ConstructExpression(
    string TypeName,
    ImmutableArray<GenericArgumentSyntax> TypeArguments,
    ImmutableArray<FieldInitSyntax> Fields) : Expression
{
    public required SourceSpan TypeNameSpan { get; init; }
}

/// <summary>Declaração de enum. Não tem nome próprio: o nome vem do <c>def</c> (spec §15).</summary>
public sealed record EnumExpression(
    ImmutableArray<TypeParameterSyntax> TypeParameters,
    ImmutableArray<VariantSyntax> Variants) : Expression;

/// <summary>Marcador de erro de sintaxe, para manter a árvore bem-formada na recuperação.</summary>
public sealed record ErrorExpression : Expression;

// ----------------------------------------------------------------- partes

public sealed record ParameterSyntax(string Name, TypeSyntax Type) : SurfaceNode;

public sealed record VariantSyntax(string Name, ImmutableArray<TypeSyntax> Payload) : SurfaceNode;

public sealed record MatchArm(Pattern Pattern, Expression Body) : SurfaceNode;

public sealed record FieldSyntax(string Name, TypeSyntax Type) : SurfaceNode;

public sealed record FieldInitSyntax(string Name, Expression Value) : SurfaceNode
{
    public required SourceSpan NameSpan { get; init; }
}

// ---------------------------------------------------------------- padrões

/// <summary>
/// Q3 tornou os padrões não ambíguos: como variantes exigem qualificação
/// completa, um identificador sozinho é <b>sempre</b> um binding novo e
/// <c>Enum.Variante</c> é <b>sempre</b> um padrão de variante. O parser decide
/// sem consultar o escopo.
/// </summary>
public abstract record Pattern : SurfaceNode;

public sealed record WildcardPattern : Pattern;

public sealed record BindingPattern(string Name) : Pattern;

public sealed record VariantPattern(
    string EnumName,
    string VariantName,
    ImmutableArray<Pattern> Arguments) : Pattern
{
    public required SourceSpan VariantSpan { get; init; }
}

public sealed record LiteralPattern(ConstantValue Value) : Pattern;

// --------------------------------------------------------------- generics

/// <summary>
/// Parâmetro genérico de uma declaração (Q1). <c>ConstType</c> ausente significa
/// parâmetro de tipo (<c>T</c>); presente, parâmetro const (<c>N: Int</c>).
/// </summary>
public sealed record TypeParameterSyntax(string Name, TypeSyntax? ConstType) : SurfaceNode
{
    public static TypeParameterSyntax OfType(string name, SourceSpan span) =>
        new(name, null) { Span = span };
}

/// <summary>
/// Argumento genérico escrito no código. Sintaticamente, um argumento pode ser um
/// tipo ou um valor — e um identificador nu é <b>os dois</b>, porque
/// <c>Foo&lt;N&gt;</c> não diz se <c>N</c> nomeia um tipo ou uma constante.
/// A decisão é do checker, que conhece o escopo (Apêndice A §A.7).
/// </summary>
public abstract record GenericArgumentSyntax : SurfaceNode;

public sealed record TypeArgumentSyntax(TypeSyntax Type) : GenericArgumentSyntax;

public sealed record ValueArgumentSyntax(Expression Value) : GenericArgumentSyntax;

/// <summary>Um identificador nu, ainda sem decidir se é tipo ou constante.</summary>
public sealed record NameArgumentSyntax(string Name) : GenericArgumentSyntax;

// ------------------------------------------------------------ tipos (sintaxe)

public abstract record TypeSyntax : SurfaceNode;

public sealed record NamedTypeSyntax(string Name, ImmutableArray<GenericArgumentSyntax> Arguments) : TypeSyntax
{
    public static NamedTypeSyntax Of(string name, SourceSpan span) => new(name, []) { Span = span };
}

public sealed record ArrayTypeSyntax(TypeSyntax Element) : TypeSyntax;

public sealed record FunctionTypeSyntax(ImmutableArray<TypeSyntax> Parameters, TypeSyntax Return) : TypeSyntax;

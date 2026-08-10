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
    ImmutableArray<ParameterSyntax> Parameters,
    TypeSyntax? ReturnType,
    BlockExpression Body) : Expression;

public sealed record CallExpression(Expression Callee, ImmutableArray<Expression> Arguments) : Expression;

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

/// <summary>Declaração de enum. Não tem nome próprio: o nome vem do <c>def</c> (spec §15).</summary>
public sealed record EnumExpression(
    ImmutableArray<TypeParameterSyntax> TypeParameters,
    ImmutableArray<VariantSyntax> Variants) : Expression;

/// <summary>Marcador de erro de sintaxe, para manter a árvore bem-formada na recuperação.</summary>
public sealed record ErrorExpression : Expression;

// ----------------------------------------------------------------- partes

public sealed record ParameterSyntax(string Name, TypeSyntax Type) : SurfaceNode;

public sealed record VariantSyntax(string Name, ImmutableArray<TypeSyntax> Payload) : SurfaceNode;

public sealed record TypeParameterSyntax(string Name) : SurfaceNode;

// ------------------------------------------------------------ tipos (sintaxe)

public abstract record TypeSyntax : SurfaceNode;

public sealed record NamedTypeSyntax(string Name, ImmutableArray<TypeSyntax> Arguments) : TypeSyntax
{
    public static NamedTypeSyntax Of(string name, SourceSpan span) => new(name, []) { Span = span };
}

public sealed record ArrayTypeSyntax(TypeSyntax Element) : TypeSyntax;

public sealed record FunctionTypeSyntax(ImmutableArray<TypeSyntax> Parameters, TypeSyntax Return) : TypeSyntax;

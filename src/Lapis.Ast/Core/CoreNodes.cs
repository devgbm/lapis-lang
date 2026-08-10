using System.Collections.Immutable;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Ast.Core;

/// <summary>
/// Core AST — representação pequena e uniforme sobre a qual trabalham o type
/// checker, o evaluator e o partial evaluator.
///
/// São classes (não <c>record</c>) porque precisam de identidade estável: o
/// <see cref="NodeId"/> indexa as tabelas de tipos e resoluções do checker e o
/// tracing do PE, o que brigaria com igualdade estrutural. Comparação estrutural,
/// quando necessária nos testes, passa pelo <c>CoreSExprPrinter</c>
/// (plano 02 §2.2).
///
/// Não há nó <c>Block</c>: <see cref="CoreLet"/> sequencia (plano 02 §2.3, Q10).
/// </summary>
public abstract class CoreExpr
{
    protected CoreExpr(int nodeId, SourceSpan span)
    {
        NodeId = nodeId;
        Span = span;
    }

    public int NodeId { get; }

    public SourceSpan Span { get; }
}

public sealed class CoreLiteral(int nodeId, SourceSpan span, ConstantValue value) : CoreExpr(nodeId, span)
{
    public ConstantValue Value { get; } = value;
}

public sealed class CoreVariable(int nodeId, SourceSpan span, string name) : CoreExpr(nodeId, span)
{
    public string Name { get; } = name;
}

/// <summary>
/// <c>Let(name, value, body)</c>. O nome é visível apenas em <c>body</c> — não em
/// <c>value</c>, o que é o que torna a v0.2 não recursiva (Q8).
/// </summary>
public sealed class CoreLet(
    int nodeId,
    SourceSpan span,
    string name,
    TypeSyntax? annotation,
    CoreExpr value,
    CoreExpr body,
    bool isSynthetic) : CoreExpr(nodeId, span)
{
    public string Name { get; } = name;

    public TypeSyntax? Annotation { get; } = annotation;

    public CoreExpr Value { get; } = value;

    public CoreExpr Body { get; } = body;

    /// <summary>Verdadeiro quando o <c>Let</c> só existe para sequenciar um statement.</summary>
    public bool IsSynthetic { get; } = isSynthetic;

    public SourceSpan NameSpan { get; init; } = span;
}

public sealed record CoreParameter(string Name, TypeSyntax Type, SourceSpan Span);

public sealed class CoreLambda(
    int nodeId,
    SourceSpan span,
    ImmutableArray<CoreParameter> parameters,
    TypeSyntax? returnType,
    CoreExpr body) : CoreExpr(nodeId, span)
{
    public ImmutableArray<CoreParameter> Parameters { get; } = parameters;

    /// <summary>Ausente significa <c>Void</c> (spec §6).</summary>
    public TypeSyntax? ReturnType { get; } = returnType;

    public CoreExpr Body { get; } = body;

    /// <summary>Span da chave de fechamento do corpo, usado por <c>LAP0272</c>.</summary>
    public SourceSpan BodyEndSpan { get; init; } = span;
}

public sealed class CoreCall(
    int nodeId,
    SourceSpan span,
    CoreExpr callee,
    ImmutableArray<CoreExpr> arguments) : CoreExpr(nodeId, span)
{
    public CoreExpr Callee { get; } = callee;

    public ImmutableArray<CoreExpr> Arguments { get; } = arguments;
}

public sealed class CoreReturn(int nodeId, SourceSpan span, CoreExpr? value) : CoreExpr(nodeId, span)
{
    public CoreExpr? Value { get; } = value;
}

/// <summary>O ramo <c>Else</c> está sempre presente; o desugar insere <c>()</c>.</summary>
public sealed class CoreIf(
    int nodeId,
    SourceSpan span,
    CoreExpr condition,
    CoreExpr then,
    CoreExpr @else) : CoreExpr(nodeId, span)
{
    public CoreExpr Condition { get; } = condition;

    public CoreExpr Then { get; } = then;

    public CoreExpr Else { get; } = @else;
}

public sealed class CoreBinary(
    int nodeId,
    SourceSpan span,
    BinaryOperator op,
    CoreExpr left,
    CoreExpr right) : CoreExpr(nodeId, span)
{
    public BinaryOperator Operator { get; } = op;

    public CoreExpr Left { get; } = left;

    public CoreExpr Right { get; } = right;

    public SourceSpan OperatorSpan { get; init; } = span;
}

public sealed class CoreUnary(
    int nodeId,
    SourceSpan span,
    UnaryOperator op,
    CoreExpr operand) : CoreExpr(nodeId, span)
{
    public UnaryOperator Operator { get; } = op;

    public CoreExpr Operand { get; } = operand;
}

public sealed class CoreArray(
    int nodeId,
    SourceSpan span,
    ImmutableArray<CoreExpr> elements) : CoreExpr(nodeId, span)
{
    public ImmutableArray<CoreExpr> Elements { get; } = elements;
}

/// <summary>
/// <c>alvo[índice]</c>. A checagem de limites é semântica <b>deste nó</b> (spec §41),
/// não uma expansão do desugar — é o que permite ao partial evaluator decidir
/// sobre a checagem em vez de analisar um <c>If</c> gerado.
/// </summary>
public sealed class CoreIndex(
    int nodeId,
    SourceSpan span,
    CoreExpr target,
    CoreExpr index) : CoreExpr(nodeId, span)
{
    public CoreExpr Target { get; } = target;

    public CoreExpr Index { get; } = index;
}

/// <summary>Acesso a campo de struct e a variante de enum; o checker distingue.</summary>
public sealed class CoreField(
    int nodeId,
    SourceSpan span,
    CoreExpr target,
    string name) : CoreExpr(nodeId, span)
{
    public CoreExpr Target { get; } = target;

    public string Name { get; } = name;

    public SourceSpan NameSpan { get; init; } = span;
}

public sealed record CoreVariantDecl(string Name, ImmutableArray<TypeSyntax> Payload, SourceSpan Span);

public sealed class CoreEnumDef(
    int nodeId,
    SourceSpan span,
    ImmutableArray<string> typeParameters,
    ImmutableArray<CoreVariantDecl> variants) : CoreExpr(nodeId, span)
{
    public ImmutableArray<string> TypeParameters { get; } = typeParameters;

    public ImmutableArray<CoreVariantDecl> Variants { get; } = variants;
}

public sealed record CoreFieldDecl(string Name, TypeSyntax Type, SourceSpan Span);

public sealed class CoreTypeDef(
    int nodeId,
    SourceSpan span,
    ImmutableArray<string> typeParameters,
    ImmutableArray<CoreFieldDecl> fields) : CoreExpr(nodeId, span)
{
    public ImmutableArray<string> TypeParameters { get; } = typeParameters;

    public ImmutableArray<CoreFieldDecl> Fields { get; } = fields;
}

public sealed record CoreFieldInit(string Name, CoreExpr Value, SourceSpan Span, SourceSpan NameSpan);

public sealed class CoreConstruct(
    int nodeId,
    SourceSpan span,
    string typeName,
    ImmutableArray<TypeSyntax> typeArguments,
    ImmutableArray<CoreFieldInit> fields) : CoreExpr(nodeId, span)
{
    public string TypeName { get; } = typeName;

    public ImmutableArray<TypeSyntax> TypeArguments { get; } = typeArguments;

    public ImmutableArray<CoreFieldInit> Fields { get; } = fields;

    public SourceSpan TypeNameSpan { get; init; } = span;
}

public abstract record CorePattern
{
    public required SourceSpan Span { get; init; }
}

public sealed record CoreWildcardPattern : CorePattern;

public sealed record CoreBindingPattern(string Name) : CorePattern;

public sealed record CoreVariantPattern(
    string EnumName,
    string VariantName,
    ImmutableArray<CorePattern> Arguments) : CorePattern
{
    public required SourceSpan VariantSpan { get; init; }
}

public sealed record CoreLiteralPattern(ConstantValue Value) : CorePattern;

public sealed record CoreArm(CorePattern Pattern, CoreExpr Body, SourceSpan Span);

/// <summary>
/// <c>Match</c> permanece como primitiva da Core: desugará-lo exigiria primitivas
/// <c>enum_tag</c> e <c>enum_payload</c>, aumentando o runtime — contra a spec §58
/// ("runtime mínimo"). Um plano futuro pode inverter isso sem afetar nada acima
/// do desugar.
/// </summary>
public sealed class CoreMatch(
    int nodeId,
    SourceSpan span,
    CoreExpr scrutinee,
    ImmutableArray<CoreArm> arms) : CoreExpr(nodeId, span)
{
    public CoreExpr Scrutinee { get; } = scrutinee;

    public ImmutableArray<CoreArm> Arms { get; } = arms;
}

/// <summary>Um arquivo <c>.ls</c> inteiro reduzido a uma única expressão.</summary>
public sealed class CoreProgram(CoreExpr body, int nodeCount)
{
    public CoreExpr Body { get; } = body;

    /// <summary>Número de nós criados; usado para dimensionar as tabelas do checker.</summary>
    public int NodeCount { get; } = nodeCount;
}

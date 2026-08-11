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

    /// <summary>Declarado com <c>var</c>: pode ser reatribuído (Q25).</summary>
    public bool IsMutable { get; init; }

    public SourceSpan NameSpan { get; init; } = span;
}

/// <summary>
/// <c>x = e</c> — reatribuição do <c>Let</c> mutável que introduziu <c>x</c>.
/// Tipo <c>Void</c>: a atribuição não produz valor.
///
/// Sem nó de referência e sem célula na Core: quem resolve o nome é o mesmo
/// mecanismo léxico de <see cref="CoreVariable"/>. Isso só é possível porque um
/// <c>var</c> não atravessa fronteira de função (Q25) — não há aliasing para o
/// partial evaluator modelar, só um slot local que muda.
/// </summary>
public sealed class CoreAssign(
    int nodeId,
    SourceSpan span,
    string name,
    CoreExpr value) : CoreExpr(nodeId, span)
{
    public string Name { get; } = name;

    public CoreExpr Value { get; } = value;

    public SourceSpan NameSpan { get; init; } = span;
}

public sealed record CoreParameter(string Name, TypeSyntax Type, SourceSpan Span);

/// <summary>Parâmetro genérico declarado: <c>T</c> ou <c>N: Int</c> (Q1).</summary>
public sealed record CoreTypeParameter(string Name, TypeSyntax? ConstType, SourceSpan Span)
{
    public bool IsConst => ConstType is not null;
}

/// <summary>
/// Argumento genérico ainda não classificado (Apêndice A §A.7). Os três casos são
/// sintáticos: um tipo, um valor, ou um identificador nu que só o escopo desempata.
/// </summary>
public abstract record CoreGenericArgument
{
    public required SourceSpan Span { get; init; }
}

public sealed record CoreTypeArgument(TypeSyntax Type) : CoreGenericArgument;

public sealed record CoreValueArgument(CoreExpr Value) : CoreGenericArgument;

public sealed record CoreNameArgument(string Name) : CoreGenericArgument;

public sealed class CoreLambda(
    int nodeId,
    SourceSpan span,
    ImmutableArray<CoreTypeParameter> typeParameters,
    ImmutableArray<CoreParameter> parameters,
    TypeSyntax? returnType,
    CoreExpr body) : CoreExpr(nodeId, span)
{
    public ImmutableArray<CoreTypeParameter> TypeParameters { get; } = typeParameters;

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

/// <summary>
/// <c>alvo&lt;A, B&gt;</c> — aplicação de argumentos genéricos.
///
/// Permanece como nó da Core, e não é apagado pelo desugar, porque o partial
/// evaluator precisa ver onde cada especialização foi pedida: é exatamente a
/// fronteira entre o que o checker instancia e o que o PE monomorfiza (plano 13).
/// </summary>
public sealed class CoreInstantiate(
    int nodeId,
    SourceSpan span,
    CoreExpr target,
    ImmutableArray<CoreGenericArgument> arguments) : CoreExpr(nodeId, span)
{
    public CoreExpr Target { get; } = target;

    public ImmutableArray<CoreGenericArgument> Arguments { get; } = arguments;
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
    ImmutableArray<CoreTypeParameter> typeParameters,
    ImmutableArray<CoreVariantDecl> variants) : CoreExpr(nodeId, span)
{
    public ImmutableArray<CoreTypeParameter> TypeParameters { get; } = typeParameters;

    public ImmutableArray<CoreVariantDecl> Variants { get; } = variants;
}

public sealed record CoreFieldDecl(string Name, TypeSyntax Type, SourceSpan Span);

public sealed class CoreTypeDef(
    int nodeId,
    SourceSpan span,
    ImmutableArray<CoreTypeParameter> typeParameters,
    ImmutableArray<CoreFieldDecl> fields) : CoreExpr(nodeId, span)
{
    public ImmutableArray<CoreTypeParameter> TypeParameters { get; } = typeParameters;

    public ImmutableArray<CoreFieldDecl> Fields { get; } = fields;
}

public sealed record CoreFieldInit(string Name, CoreExpr Value, SourceSpan Span, SourceSpan NameSpan);

public sealed class CoreConstruct(
    int nodeId,
    SourceSpan span,
    string typeName,
    ImmutableArray<CoreGenericArgument> typeArguments,
    ImmutableArray<CoreFieldInit> fields) : CoreExpr(nodeId, span)
{
    public string TypeName { get; } = typeName;

    public ImmutableArray<CoreGenericArgument> TypeArguments { get; } = typeArguments;

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

/// <summary>Salto incondicional. Tipo <c>Never</c>: nada depois dele executa.</summary>
public sealed class CoreGoto(int nodeId, SourceSpan span, string label) : CoreExpr(nodeId, span)
{
    public string Label { get; } = label;

    public SourceSpan LabelSpan { get; init; } = span;

    /// <summary>
    /// Verdadeiro quando o salto é o que o desugar insere ao fechar um segmento,
    /// e não algo que alguém escreveu (plano 16 §16.4, regra 1).
    ///
    /// A distinção importa em dois lugares: o printer omite o salto implícito (é
    /// a inversa exata da decomposição) e <c>LAP0273</c> não o trata como código
    /// inalcançável — depois de um <c>return</c> vem um <c>label</c>, que é
    /// perfeitamente alcançável por salto.
    /// </summary>
    public bool IsImplicit { get; init; }
}

/// <summary>
/// Salto condicional. É <b>primitivo</b>, e não açúcar para
/// <c>If(cond, Goto(L), ())</c>, porque uma macro de controle construída sobre
/// <c>goto</c> não pode depender do <c>if</c> da linguagem — a construção seria
/// circular (plano 16 §16.2).
/// </summary>
public sealed class CoreGotoIf(
    int nodeId,
    SourceSpan span,
    string label,
    CoreExpr condition) : CoreExpr(nodeId, span)
{
    public string Label { get; } = label;

    public CoreExpr Condition { get; } = condition;

    public SourceSpan LabelSpan { get; init; } = span;
}

/// <summary>Um destino de salto dentro de um <see cref="CoreLabeled"/>.</summary>
public sealed record CoreJoin(string Name, CoreExpr Body, SourceSpan Span)
{
    public SourceSpan NameSpan { get; init; } = Span;
}

/// <summary>
/// Grupo de join points: avalia <see cref="Entry"/> e, se ela terminar em salto
/// para um dos <see cref="Joins"/>, avalia aquele corpo — que por sua vez pode
/// saltar de novo.
///
/// Join points, e não saltos de verdade, porque a Core não tem nó <c>Block</c>
/// (Q10): "pular para a instrução 7" não quer dizer nada numa cadeia de
/// <c>Let</c>. É a forma que compiladores funcionais usam há décadas para casar
/// fluxo não estruturado com escopo léxico — e é o que mantém substituição e
/// inlining textuais no partial evaluator.
///
/// Com salto para trás os joins podem se referenciar mutuamente: o grafo deixa
/// de ser um DAG, mas a estrutura não muda.
/// </summary>
public sealed class CoreLabeled(
    int nodeId,
    SourceSpan span,
    CoreExpr entry,
    ImmutableArray<CoreJoin> joins) : CoreExpr(nodeId, span)
{
    public CoreExpr Entry { get; } = entry;

    public ImmutableArray<CoreJoin> Joins { get; } = joins;
}

/// <summary>Um arquivo <c>.ls</c> inteiro reduzido a uma única expressão.</summary>
public sealed class CoreProgram(CoreExpr body, int nodeCount)
{
    public CoreExpr Body { get; } = body;

    /// <summary>Número de nós criados; usado para dimensionar as tabelas do checker.</summary>
    public int NodeCount { get; } = nodeCount;
}

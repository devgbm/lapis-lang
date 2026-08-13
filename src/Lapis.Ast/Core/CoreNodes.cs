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

    /// <summary>
    /// O span do tipo dono, quando o <c>Let</c> veio de <c>def T.m = e;</c>.
    ///
    /// Existe para o diagnóstico apontar o certo: <c>LAP0704</c> fala do
    /// <b>dono</b>, e um circunflexo sob o nome do membro seria um caret
    /// contradizendo a própria mensagem.
    /// </summary>
    public SourceSpan? OwnerSpan { get; init; }

    /// <summary>
    /// O dono <b>como escrito</b>, quando o <c>Let</c> veio de <c>def T.m = e;</c>
    /// — inclusive os argumentos genéricos de <c>def Result&lt;Int, ?&gt;.m</c>
    /// (plano 23 §23.4).
    ///
    /// O nome sintético já carrega o padrão em texto, mas texto não se resolve: o
    /// checker precisa dos argumentos como sintaxe para transformá-los em
    /// <c>GenericArgument</c> — e é dele que <c>self</c> tira o tipo.
    /// </summary>
    public NamedTypeSyntax? Owner { get; init; }
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

    /// <summary>
    /// Caminho percorrido a partir de <see cref="Name"/> — vazio na forma simples.
    ///
    /// <c>u.name = e</c> e <c>xs[i] = e</c> <b>não</b> mudam o valor no lugar:
    /// reconstroem-no e reatribuem o slot (plano 21 §21.3b, estendido pela Q36).
    /// Com isso todo <c>Value</c> continua imutável, a única coisa mutável
    /// continua sendo o slot do ambiente, e não há aliasing para o partial
    /// evaluator modelar — é a premissa da Q25 que a Q36 precisava preservar.
    /// </summary>
    public ImmutableArray<CoreAssignSegment> Path { get; init; } = [];
}

/// <summary>Um passo do caminho de <see cref="CoreAssign"/>: campo ou índice.</summary>
public abstract record CoreAssignSegment(SourceSpan Span);

public sealed record CoreFieldSegment(string Name, SourceSpan Span) : CoreAssignSegment(Span);

/// <summary>
/// <c>[i]</c> num caminho de atribuição (Q36).
///
/// Carrega uma <see cref="CoreExpr"/>, e por isso é o único segmento que as
/// travessias precisam visitar: o índice pode citar variáveis, ter efeito e ser
/// especializado como qualquer outra expressão.
/// </summary>
public sealed record CoreIndexSegment(CoreExpr Index, SourceSpan Span) : CoreAssignSegment(Span);

/// <summary>
/// Parâmetro de uma função. <see cref="Type"/> é <c>null</c> só para <c>self</c>
/// (plano 22 §22.1) — o tipo dele vem do dono do membro, e não da sintaxe.
/// </summary>
public sealed record CoreParameter(string Name, TypeSyntax? Type, SourceSpan Span);

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

/// <summary>
/// <c>throw e</c> (spec de macros §8.2). Sobrevive ao desugar como nó próprio
/// porque o evaluator precisa distinguir "a constraint rejeitou" de qualquer
/// outro aborto: a mensagem é <b>do programa</b>, e é ela que vira
/// <c>LAP0503</c>.
/// </summary>
public sealed class CoreThrow(int nodeId, SourceSpan span, CoreExpr value) : CoreExpr(nodeId, span)
{
    public CoreExpr Value { get; } = value;
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

public sealed class CoreSpan(
    int nodeId,
    SourceSpan span,
    ImmutableArray<CoreExpr> elements) : CoreExpr(nodeId, span)
{
    public ImmutableArray<CoreExpr> Elements { get; } = elements;
}

/// <summary>
/// <c>.[T; inicial; n]</c> — span por repetição.
///
/// Não desaparece no desugar virando <c>CoreSpan</c> de <c>n</c> elementos: a
/// quantidade pode não ser conhecida em compilação, e mesmo quando é, expandir
/// oito mil zeros na Core seria trocar um nó por um programa. É construção
/// própria, com semântica própria — o inicializador é avaliado <b>uma vez</b>,
/// o que é invisível para valores (não há mutação de span) e visível para
/// efeitos.
/// </summary>
public sealed class CoreSpanRepeat(
    int nodeId,
    SourceSpan span,
    TypeSyntax element,
    CoreExpr initializer,
    CoreExpr size) : CoreExpr(nodeId, span)
{
    public TypeSyntax Element { get; } = element;

    public CoreExpr Initializer { get; } = initializer;

    public CoreExpr Size { get; } = size;
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
/// <c>Match</c> segue primitiva da Core — <b>por ora</b>.
///
/// O que ele tem e <see cref="CoreIs"/> não tem é a <b>exaustividade</b>
/// (Q6/<c>LAP0262</c>): <c>match</c> é expressão e precisa produzir valor em toda
/// execução, e provar que os braços cobrem o enum exige saber o tipo do
/// escrutinado — coisa que uma macro, rodando antes do checker sobre a Surface,
/// não sabe. É o único motivo de ele continuar aqui (Q22).
///
/// O plano é sair: quando o sistema de macros souber provar exaustividade,
/// <c>match</c> vira <c>@match</c> no prelude, expandindo para uma cadeia de
/// <c>if</c>/<c>is</c> — que é exatamente a razão de <see cref="CoreIs"/> ser
/// primitiva em vez de açúcar sobre este nó. Até lá as duas formas coexistem:
/// <c>is</c> testa uma variante e não é exaustivo, <c>match</c> cobre todas e é.
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

/// <summary>
/// <c>e is Variante</c> / <c>e is Variante(x)</c> (plano 25, fecha Q23):
/// testar a variante de um enum e, quando há ligação, desembrulhar a carga.
///
/// <b>Por que carrega os dois ramos.</b> Um nó que só produzisse <c>Bool</c>
/// deixaria a ligação de <c>x</c> num nó <i>irmão</i> do teste, e dar escopo a
/// ela exigiria análise de dominância — a saída que a Q23 recusou por peso. Com
/// <see cref="Then"/> e <see cref="Else"/> aqui dentro, a ligação e a prova
/// nascem juntas (não existe posição em que <c>x</c> esteja em escopo e a
/// variante seja outra) e o escrutinado é avaliado <b>uma vez</b>.
///
/// A forma sem ligação é o mesmo nó com ramos literais:
/// <c>e is Some</c> ⇒ <c>Is(e, Some, então: true, senão: false)</c>.
///
/// <see cref="OwnerName"/> é o enum <b>escrito</b> (<c>e is Option.Some</c>) ou
/// <c>null</c> quando omitido: quem resolve a variante é o checker, a partir do
/// tipo do escrutinado (§25.5) — escrito, o dono só é conferido. Os argumentos
/// genéricos do dono (<c>Result&lt;Int, ?&gt;.Ok</c>) não chegam até aqui: os
/// tipos da carga vêm da instância real do escrutinado, como em <c>match</c>.
/// </summary>
public sealed class CoreIs(
    int nodeId,
    SourceSpan span,
    CoreExpr scrutinee,
    string? ownerName,
    string variantName,
    string? bindingName,
    CoreExpr then,
    CoreExpr otherwise) : CoreExpr(nodeId, span)
{
    public CoreExpr Scrutinee { get; } = scrutinee;

    public string? OwnerName { get; } = ownerName;

    public string VariantName { get; } = variantName;

    /// <summary>O nome ligado à carga no ramo verdadeiro, ou <c>null</c> no teste puro.</summary>
    public string? BindingName { get; } = bindingName;

    public CoreExpr Then { get; } = then;

    public CoreExpr Else { get; } = otherwise;

    public SourceSpan VariantSpan { get; init; } = span;

    public SourceSpan? BindingSpan { get; init; }
}

/// <summary>
/// <c>loop { ... }</c> (plano 26, M16). Um escopo léxico só, com uma única forma
/// de entrar — é o que substitui o grupo de join points que <c>goto</c>/<c>label</c>
/// precisavam (Q32): não há predecessor nenhum a considerar além deste.
///
/// <see cref="Label"/> existe só para <c>break</c>/<c>continue</c> de um <c>loop</c>
/// aninhado alcançarem este — sem ele, os dois sempre visam o laço mais próximo.
/// </summary>
public sealed class CoreLoop(
    int nodeId,
    SourceSpan span,
    string? label,
    CoreExpr body) : CoreExpr(nodeId, span)
{
    public string? Label { get; } = label;

    public CoreExpr Body { get; } = body;

    public SourceSpan? LabelSpan { get; init; }
}

/// <summary>
/// <c>break;</c>, <c>break e;</c>, opcionalmente rotulado. Tipo <c>Never</c> —
/// mesma mecânica de <c>return</c>/<c>throw</c> (Q13): não há regra de tipo nova,
/// só mais um nó na lista que já sabe propagar <c>Never</c>.
///
/// O tipo do <see cref="CoreLoop"/> que este <c>break</c> alcança é a junção de
/// todo <c>break</c> (<see cref="Value"/> ausente conta como <c>Void</c>) que o
/// alcança sem atravessar um <c>loop</c> aninhado sem rótulo — plano 26 §26.5.
/// </summary>
public sealed class CoreBreak(
    int nodeId,
    SourceSpan span,
    string? label,
    CoreExpr? value) : CoreExpr(nodeId, span)
{
    public string? Label { get; } = label;

    public CoreExpr? Value { get; } = value;

    public SourceSpan? LabelSpan { get; init; }
}

/// <summary>
/// <c>continue;</c>, opcionalmente rotulado. Tipo <c>Never</c>, como
/// <see cref="CoreBreak"/> — mas sem valor: um <c>continue</c> reinicia a
/// iteração, não sai do laço, então não contribui para o tipo do <c>loop</c>.
/// </summary>
public sealed class CoreContinue(int nodeId, SourceSpan span, string? label) : CoreExpr(nodeId, span)
{
    public string? Label { get; } = label;

    public SourceSpan? LabelSpan { get; init; }
}

/// <summary>Um arquivo <c>.ls</c> inteiro reduzido a uma única expressão.</summary>
public sealed class CoreProgram(CoreExpr body, int nodeCount)
{
    public CoreExpr Body { get; } = body;

    /// <summary>Número de nós criados; usado para dimensionar as tabelas do checker.</summary>
    public int NodeCount { get; } = nodeCount;
}

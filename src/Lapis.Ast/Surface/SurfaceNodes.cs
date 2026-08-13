using System.Collections.Immutable;
using System.Text;
using Lapis.Diagnostics;
using Lapis.Lexer;

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

/// <summary>A forma de introduzir um nome (spec §2, §8).</summary>
public sealed record DefStatement(string Name, TypeSyntax? Annotation, Expression Value) : Statement
{
    public required SourceSpan NameSpan { get; init; }

    /// <summary>
    /// <c>var</c> em vez de <c>def</c>: o nome pode ser reatribuído.
    ///
    /// É a mesma declaração porque tudo o mais é igual — só a permissão de
    /// reatribuir muda, e ela é uma propriedade do binding, não uma construção
    /// à parte.
    /// </summary>
    public bool IsMutable { get; init; }

    /// <summary>
    /// O tipo dono, quando a declaração é <c>def T.m = ...</c> (plano 21).
    ///
    /// É <see cref="TypeSyntax"/> e não <c>string</c> porque as extensions
    /// genéricas (plano 23) precisarão de <c>Result&lt;T&gt;.isOk</c>, e mudar a
    /// forma depois custaria reabrir parser, desugar e checker. No M13 só um nome
    /// sem argumentos é aceito; o resto é <c>LAP0704</c>.
    /// </summary>
    public TypeSyntax? Owner { get; init; }

    public SourceSpan? OwnerSpan { get; init; }
}

public sealed record ExpressionStatement(Expression Expression) : Statement;

/// <summary>
/// <c>x = e;</c> — reatribuição de um <c>var</c>.
///
/// É <c>Statement</c>, não <c>Expression</c>: atribuição não produz valor, e
/// mantê-la fora da gramática de expressão elimina de uma vez <c>if (x = 1)</c>
/// e a confusão entre <c>=</c> e <c>==</c>.
/// </summary>
public sealed record AssignStatement(string Name, Expression Value) : Statement
{
    public required SourceSpan NameSpan { get; init; }

    /// <summary>
    /// Os campos percorridos em <c>u.endereco.rua = e;</c> (plano 21 §21.3b).
    ///
    /// Vazio na forma simples. O receptor é sempre um <b>nome</b>, nunca uma
    /// expressão qualquer: <c>proximo().name = x</c> mutaria um temporário que
    /// ninguém mais vê.
    /// </summary>
    public ImmutableArray<string> Path { get; init; } = [];

    /// <summary>Um span por segmento de <see cref="Path"/>, para o diagnóstico apontar o certo.</summary>
    public ImmutableArray<SourceSpan> PathSpans { get; init; } = [];
}

// ------------------------------------------------------------------ macros

/// <summary>
/// <c>macro nome match &lt;padrão&gt; expand { ... };</c>
///
/// É <c>Statement</c>, e não valor ligado por <c>def</c>: uma macro <b>não é
/// first-class citizen</b> (Q19). Não existe em runtime, não é argumento, não é
/// retorno. Forçá-la a passar por <c>def</c> exigiria um tipo que só existe para
/// proibir tudo o que <c>def</c> normalmente permite.
///
/// Macros ocupam um espaço de nomes próprio: <c>macro log</c> e
/// <c>def log = fn ...</c> convivem, porque <c>@log</c> e <c>log</c> nunca se
/// confundem.
/// </summary>
public sealed record MacroDeclaration(string Name, ImmutableArray<MacroRule> Rules) : Statement
{
    public required SourceSpan NameSpan { get; init; }
}

/// <summary>
/// Uma regra: o trio <c>match</c> / <c>constraint</c>? / <c>expand</c>.
/// </summary>
/// <param name="Constraint">
/// Validação em tempo de compilação. Parseada desde já, mas só executada no
/// plano 18 — falha de <c>match</c> quer dizer "não é esta a forma"; falha de
/// <c>constraint</c>, "esta forma está errada" (spec de macros §7.1).
/// </param>
public sealed record MacroRule(
    MacroPattern Pattern,
    BlockExpression? Constraint,
    BlockExpression Expansion) : SurfaceNode;

/// <summary>Categorias sintáticas que uma captura pode pedir (spec de macros §5.1).</summary>
public enum SyntaxCategory
{
    Expression,
    Statement,
    Block,
    Type,
    Identifier,
    Literal,
    Int,
    Float,
    Str,
    Bool,
    Char,
}

public abstract record MacroPattern : SurfaceNode;

public sealed record PatternSequence(ImmutableArray<MacroPattern> Items) : MacroPattern;

/// <summary><c>Expression:e</c> — casa e liga o nome.</summary>
public sealed record PatternCapture(SyntaxCategory Category, string Name) : MacroPattern;

/// <summary>
/// Um token exato: o <c>in</c> de <c>match Identifier:i in Expression:c</c>.
///
/// Pertence à sintaxe <b>daquela macro</b> e não vira palavra reservada da
/// linguagem — é o que permite a uma biblioteca definir construções próprias sem
/// tocar no lexer.
/// </summary>
public sealed record PatternLiteral(string Text) : MacroPattern;

/// <summary>
/// <c>Item* separado por ,</c>. Cada captura de dentro liga uma <b>lista</b>, e as
/// listas são paralelas.
/// </summary>
public sealed record PatternRepeat(MacroPattern Item, string Separator) : MacroPattern;

/// <summary>
/// <c>@nome &lt;tokens&gt;</c>.
///
/// Guarda <b>tokens</b>, não árvore: o parser não sabe a forma de <c>@unless</c>
/// até a macro estar registrada, então ele delimita a invocação e entrega os
/// tokens crus. Quem parseia é o matcher, que conhece o padrão — é o que dá
/// sentido real a "macros definem sua própria sintaxe" (spec de macros §9).
///
/// É <c>Expression</c> porque uma invocação pode aparecer nas duas posições; a
/// validação de contexto acontece na expansão (<c>LAP0506</c>).
/// </summary>
public sealed record MacroInvocation(string Name, ImmutableArray<Token> Arguments) : Expression
{
    public required SourceSpan NameSpan { get; init; }
}

// ------------------------------------------------------------ expressions

public abstract record Expression : SurfaceNode;

public sealed record IntLiteral(long Value, string RawText) : Expression;

public sealed record FloatLiteral(double Value, string RawText) : Expression;

public sealed record BoolLiteral(bool Value) : Expression;

public sealed record StrLiteral(string Value) : Expression;

/// <summary>
/// <c>'a'</c> — um ponto de código (Q35).
///
/// Dentro do <c>match</c> de uma macro as mesmas aspas simples delimitam uma
/// pseudo-palavra-chave (Q38); é o parser que separa os dois casos pelo lugar
/// onde o token aparece, e só aqui o conteúdo precisa ser um caractere só.
/// </summary>
public sealed record CharLiteral(Rune Value) : Expression;

/// <summary>O literal <c>()</c>, de tipo <c>Void</c> (spec §6).</summary>
public sealed record UnitLiteral : Expression;

public sealed record IdentifierExpression(string Name) : Expression;

public sealed record UnaryExpression(UnaryOperator Operator, Expression Operand) : Expression;

public sealed record BinaryExpression(BinaryOperator Operator, Expression Left, Expression Right) : Expression
{
    public required SourceSpan OperatorSpan { get; init; }
}

public sealed record BlockExpression(ImmutableArray<Statement> Statements, Expression? Tail) : Expression;

/// <summary>
/// <c>if c { ... }</c>, ou <c>if c e;</c> (plano 26 §26.9, M16).
///
/// <c>Then</c>/<c>Else</c> deixaram de ser sempre <see cref="BlockExpression"/>:
/// um <c>if</c> sem chaves aceita qualquer expressão, **exceto** outro
/// <c>if</c> sem chaves — é o que evita o dangling-else sem precisar de regra
/// de precedência (LAP0527). A Core não sabe a diferença: <c>CoreIf.Then</c>
/// sempre foi <c>CoreExpr</c>.
/// </summary>
public sealed record IfExpression(Expression Condition, Expression Then, Expression? Else) : Expression;

/// <summary>
/// <c>loop { ... }</c>, opcionalmente rotulado (<c>loop :fora { ... }</c>) para
/// que um <c>break</c>/<c>continue</c> de um laço aninhado o alcance (plano 26,
/// M16). É <c>Expression</c>, não <c>Statement</c>: com <c>break</c> carregando
/// valor, <c>def x = loop { ... break 5; };</c> precisa fazer sentido.
/// </summary>
public sealed record LoopExpression(string? Label, BlockExpression Body) : Expression
{
    public SourceSpan? LabelSpan { get; init; }
}

/// <summary>
/// <c>break;</c>, <c>break 5;</c>, <c>break :fora;</c> ou <c>break :fora, 5;</c>
/// (plano 26, M16). O rótulo vem antes do valor, e o marcador <c>:</c> é o que
/// evita a ambiguidade entre "rótulo" e "valor" — os dois são identificador ou
/// expressão na mesma posição gramatical sem ele.
/// </summary>
public sealed record BreakExpression(string? Label, Expression? Value) : Expression
{
    public SourceSpan? LabelSpan { get; init; }
}

/// <summary>
/// <c>continue;</c> ou <c>continue :fora;</c> (plano 26, M16). Sem valor: um
/// <c>continue</c> reinicia a iteração, não sai do laço — não há o que devolver
/// ao lugar que perguntou pelo tipo do <c>loop</c>.
/// </summary>
public sealed record ContinueExpression(string? Label) : Expression
{
    public SourceSpan? LabelSpan { get; init; }
}

public sealed record ReturnExpression(Expression? Value) : Expression;

/// <summary>
/// <c>throw e</c> — interrompe a <b>compilação</b> com a mensagem <c>e</c>
/// (spec de macros §8.2).
///
/// Tem tipo <c>Never</c>, como <c>return</c> (Q13), e vale só dentro de um
/// <c>constraint</c>: fora dele é <c>LAP0507</c>. Não é uma exceção de runtime — a
/// 0.2 não as tem, e Q9 tornou a divisão total justamente para eliminar caminhos
/// de aborto.
/// </summary>
public sealed record ThrowExpression(Expression Value) : Expression;

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

/// <summary>
/// <c>.[1, 2, 3]</c> — construção de span.
///
/// Leva ponto pela mesma razão que <c>.User { }</c> (Q2): o parser distingue
/// valor de tipo com um token só, e <c>[</c> fica livre para ser sempre tipo.
/// </summary>
public sealed record SpanExpression(ImmutableArray<Expression> Elements) : Expression;

/// <summary>
/// <c>.[Int; 0; 8]</c> — construção de span por repetição: elemento, valor
/// inicial e quantidade.
///
/// A forma por lista não escala: um span de oito zeros não se escreve
/// programaticamente com <c>.[0, 0, ...]</c>, e a quantidade pode nem ser
/// conhecida. Os separadores são <c>;</c>, os mesmos de <c>[Int;8]</c> — a lista
/// usa <c>,</c>, então um token separa as duas leituras.
///
/// O elemento é escrito porque ele não sai do inicializador em todos os casos:
/// <c>.[Option&lt;Int&gt;; Option&lt;Int&gt;.None; n]</c> precisa dizer o tipo, já
/// que a variante nulária não o determina sozinha.
/// </summary>
public sealed record SpanRepeatExpression(
    TypeSyntax Element,
    Expression Initializer,
    Expression Size) : Expression;

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

/// <summary>
/// <c>e is Variante</c> ou <c>e is Variante(v)</c> — testar a variante do
/// escrutinado e, opcionalmente, desembrulhar a carga (plano 25, fecha Q23).
///
/// Não existe nó equivalente na Core: é açúcar sobre <c>match</c> (§25.2). A
/// ligação (<see cref="BindingName"/> não nulo) só produz escopo nas duas
/// posições da §25.3 — condição de <c>if</c> e operando esquerdo de
/// <c>&amp;&amp;</c> —, reconhecidas pelo desugar antes de descer para esta
/// expressão; em qualquer outra posição é <c>LAP0730</c>.
///
/// <see cref="OwnerName"/> é nulo quando a variante vem sem qualificação
/// (<c>e is Some</c>) — o enum é inferido pelo desugar a partir de quem
/// declara essa variante (§25.5), e <c>LAP0732</c> cobre tanto o nome
/// desconhecido quanto a ambiguidade entre dois enums com a mesma variante.
/// <see cref="OwnerTypeArguments"/> é aceito pela gramática (<c>Result&lt;Int,
/// ?&gt;.Ok(v)</c>) mas não é validado contra o escrutinado nesta primeira
/// implementação — os tipos da carga continuam vindo da instância real, como
/// em <c>match</c>.
/// </summary>
public sealed record IsExpression(
    Expression Scrutinee,
    string? OwnerName,
    ImmutableArray<GenericArgumentSyntax> OwnerTypeArguments,
    string VariantName,
    string? BindingName) : Expression
{
    public required SourceSpan VariantSpan { get; init; }

    public SourceSpan? OwnerSpan { get; init; }

    public SourceSpan? BindingSpan { get; init; }
}

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

/// <summary>
/// <c>x: Int</c> — ou <c>self</c>, sem anotação.
///
/// <see cref="Type"/> é anulável por causa de <c>self</c> e de mais nada: é a
/// única exceção à exigência de anotar parâmetro (spec §26), e o tipo dele vem do
/// dono do membro (plano 22 §22.1). Fora de um <c>def T.m</c>, um parâmetro sem
/// anotação continua sendo erro — só que agora é <c>LAP0712</c>, dito pelo
/// checker, que é quem sabe onde a função está.
/// </summary>
public sealed record ParameterSyntax(string Name, TypeSyntax? Type) : SurfaceNode;

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

/// <summary>
/// <c>?</c> como argumento genérico — o curinga do dono de um membro (plano 23).
///
/// O parser aceita em qualquer lista de argumentos; quem restringe à posição de
/// dono é o checker (<c>LAP0721</c>), porque só ele sabe onde a lista está.
/// </summary>
public sealed record WildcardArgumentSyntax : GenericArgumentSyntax;

// ------------------------------------------------------------ tipos (sintaxe)

public abstract record TypeSyntax : SurfaceNode;

public sealed record NamedTypeSyntax(string Name, ImmutableArray<GenericArgumentSyntax> Arguments) : TypeSyntax
{
    public static NamedTypeSyntax Of(string name, SourceSpan span) => new(name, []) { Span = span };
}

/// <summary><c>[Int;3]</c>, <c>[Int;?]</c>, <c>[Int;N]</c>.</summary>
public sealed record SpanTypeSyntax(TypeSyntax Element, SpanSizeSyntax Size) : TypeSyntax;

/// <summary>O tamanho como escrito: literal, <c>?</c>, ou o nome de um parâmetro const.</summary>
public abstract record SpanSizeSyntax : SurfaceNode;

public sealed record FixedSizeSyntax(long Value) : SpanSizeSyntax;

public sealed record UnknownSizeSyntax : SpanSizeSyntax;

public sealed record NamedSizeSyntax(string Name) : SpanSizeSyntax;

public sealed record FunctionTypeSyntax(ImmutableArray<TypeSyntax> Parameters, TypeSyntax Return) : TypeSyntax;

# Plano 17 — Macro Engine

**Projeto:** `Lapis.Macros` (novo), `Lapis.Parser`, `Lapis.Cli`
**Milestone:** M8
**Spec:** [`lapislang-macros-0.1.md` §3–§7, §9](../spec/lapislang-macros-0.1.md)
**Depende de:** 04 (parser), 02 (Surface AST)

---

## Objetivo

`Surface AST → Surface AST`: reconhecer invocações `@nome`, casar padrões, expandir
para sintaxe, com higiene. Sem `constraint` (plano 18) e sem reflection (plano 19)
— este plano entrega a máquina puramente sintática.

## Escopo

**Entra:** declaração de macro, padrões, capturas, expansão, higiene, expansão
recursiva com limite, diagnósticos `LAP0501`–`LAP0506`.

**Fica de fora:** `constraint` e `throw` (plano 18); reflection (19); as macros de
controle (20). Uma macro sem `constraint` é utilizável ao fim deste plano.

---

## O que será construído

### 17.1 Projeto novo: `Lapis.Macros`

Entre `Lapis.Parser` e `Lapis.Desugar` no grafo de dependências, que é travado por
`ArchitectureTests`:

```text
Lapis.Macros → Lapis.Ast, Lapis.Diagnostics
Lapis.Desugar → Lapis.Ast, Lapis.Diagnostics          (inalterado)
Lapis.Cli → ... + Lapis.Macros
```

`Lapis.Macros` **não** referencia `Lapis.TypeChecker` nem `Lapis.Evaluator`: a
expansão é anterior às duas. O plano 18 quebra isso de propósito e explica como.

### 17.2 Surface AST

```csharp
// Declaração nomeada — Q19: macro não é first-class citizen e não passa por `def`.
sealed record MacroDeclaration(string Name, ImmutableArray<MacroRule> Rules) : Statement
{
    public required SourceSpan NameSpan { get; init; }
}

sealed record MacroRule(
    MacroPattern Pattern,
    BlockExpression? Constraint,     // plano 18
    ImmutableArray<Statement> Expansion,
    Expression? ExpansionTail) : SurfaceNode;

abstract record MacroPattern : SurfaceNode;
  sealed record PatternSequence(ImmutableArray<MacroPattern> Items) : MacroPattern;
  sealed record PatternCapture(SyntaxCategory Category, string Name) : MacroPattern;
  sealed record PatternLiteral(string Text) : MacroPattern;
  sealed record PatternRepeat(MacroPattern Item, string Separator) : MacroPattern;

enum SyntaxCategory { Expression, Statement, Block, Type, Identifier, Literal, Int, Float, Str, Bool, Pattern }

sealed record MacroInvocation(
    string Name,
    ImmutableArray<Token> Arguments) : Expression
{
    public required SourceSpan NameSpan { get; init; }
}
```

`MacroDeclaration` é um `Statement`, não uma `Expression`: Q19 decidiu que uma macro
**não é first-class citizen**. Ela não existe em runtime, não é argumento, não é
retorno — e é a única exceção ao princípio §2 ("todo nome vem de `def`"), justamente
porque `def` liga valores.

Macros ocupam um **espaço de nomes próprio**: `macro log` e `def log = fn ...`
convivem, porque `@log` e `log` nunca se confundem.

**`MacroInvocation` guarda tokens, não árvores.** É a decisão central deste plano:
o parser não sabe a forma de `@unless` até a macro estar registrada, então ele
delimita a invocação e entrega os tokens crus. Quem parseia é o matcher, que
conhece o padrão.

### 17.3 Delimitação da invocação, no parser

O parser precisa saber onde `@nome ...` termina sem conhecer a macro. Regra:

> A invocação consome tokens até o `;` de fechamento no nível 0 de aninhamento, ou
> até o `}` que fecha o último bloco iniciado no nível 0.

Isto cobre as duas formas reais:

```c
@log value;                        // termina no ';'
@unless x > 0 { ... }              // termina no '}'
```

Parênteses, colchetes e chaves são contados, então `@foo f(a; b)` não engana o
delimitador — embora `;` dentro de parênteses não seja sintaxe válida de qualquer
modo.

### 17.4 `MacroRegistry`

```csharp
sealed class MacroRegistry
{
    bool TryRegister(string name, MacroExpression macro, SourceSpan span);
    bool TryLookup(string name, out MacroExpression macro);
}
```

Macros são visíveis **do ponto da declaração em diante**, no arquivo — a mesma
regra de `def` (Q8). Uma pré-passagem varre os `MacroDeclaration` de topo e registra
na ordem em que aparecem.

### 17.5 `MacroMatcher`

```csharp
sealed record MatchResult(ImmutableDictionary<string, MacroBinding> Bindings);

abstract record MacroBinding;
  sealed record SingleBinding(SurfaceNode Node) : MacroBinding;
  sealed record RepeatBinding(ImmutableArray<SurfaceNode> Nodes) : MacroBinding;

static MatchOutcome Match(MacroPattern pattern, ImmutableArray<Token> tokens, DiagnosticBag sink);
```

O matcher roda um `Parser` sobre os tokens da invocação, guiado pelo padrão:

| Item do padrão | Ação |
|---|---|
| `PatternCapture(Expression, e)` | `ParseExpression()` |
| `PatternCapture(Block, b)` | `ParseBlock()` |
| `PatternCapture(Identifier, x)` | exige um `Identifier` |
| `PatternCapture(Str, s)` | exige um `StringLiteral` |
| `PatternLiteral("in")` | exige o token exato |
| `PatternRepeat(item, ",")` | repete até acabarem os tokens; um grupo liga listas paralelas |
| `PatternCapture(Pattern, a)` | `Enum.Variante`, literal ou `_` |

**Diagnósticos suprimidos durante a tentativa**, exatamente como no backtracking de
Q5 (parser §4.4): uma regra que não casa não é erro, é a próxima regra.

Ao fim, todos os tokens têm de estar consumidos — sobra é falha de match.

### 17.6 Resolução de regras

Todas as regras são testadas:

| Casam | Resultado |
|---|---|
| 0 | `LAP0501` |
| 1 | expande |
| 2+ | `LAP0502`, listando as regras ambíguas nas notas |

A proposta original pedia ordem *e* detecção de ambiguidade — incompatíveis, já que
detectar ambiguidade obriga a testar todas. Escolhemos falhar ruidosamente, como a
0.2 já faz com `a < b < c` (`LAP0114`).

### 17.7 `MacroExpander`

Substitui cada referência a uma captura pela árvore capturada, e reescreve
identificadores introduzidos:

```csharp
sealed class MacroExpander
{
    ImmutableArray<Statement> Expand(MacroRule rule, MatchResult match, SourceSpan invocation);
}
```

**Contexto de expansão** (§6.1): o resultado tem de caber onde a macro foi
invocada.

| Invocada como | `expand` produz | Senão |
|---|---|---|
| statement | statements | — |
| expressão | um `BlockExpression` com cauda, ou uma expressão | `LAP0506` |

### 17.8 Higiene

Cada expansão recebe uma **marca** (um inteiro crescente). Um identificador que
aparece no `expand` e **não** vem de captura é renomeado para `nome@marca`.

```c
macro example
    match Block:body
    expand { def temp = 10; body; };
```

```c
def temp = "meu";
@example { print(temp); }     // "meu": o `temp` da macro virou `temp@1`
```

`@` não é lexável em identificadores, então colisão é impossível — mesma técnica
dos nomes sintéticos que o desugar já gera e que `FindSuggestion` filtra.

Identificadores **vindos de captura** não são tocados: eles carregam o contexto
léxico do programa que invocou, que é o requisito de §27 da proposta.

### 17.9 Spans

Todo nó produzido carrega dois spans:

```csharp
sealed record ExpansionOrigin(SourceSpan Invocation, SourceSpan InMacro);
```

O primeiro é onde o usuário escreveu `@unless`; o segundo, onde dentro do `expand`
o nó nasceu. Diagnósticos reportam o primeiro e citam o segundo em nota — quem lê
o erro escreveu a invocação, não a macro.

### 17.10 CLI

`lapis expand arquivo.ls` imprime a Surface AST depois da expansão, no mesmo
formato de `lapis ast`. É a ferramenta de depuração sem a qual macros viram
adivinhação.

---

## Decisões de design

### Por que os tokens, e não a árvore

Uma invocação `@foreach user in users { }` não parseia com a gramática da
linguagem — `user in users` não é expressão. O parser não pode produzir árvore
antes de conhecer o padrão. Guardar tokens e parsear no matcher é a única ordem que
funciona, e é o que dá a §9 da spec ("macros definem sua própria sintaxe") um
significado real.

### Por que a expansão é `Surface → Surface`

A alternativa seria expandir direto para a Core. Isso pareceria mais direto e seria
pior: perderia-se `lapis expand`, o `expand` teria de ser escrito em termos de
`Let` em vez de código normal, e a validação de contexto (expressão vs. statement)
deixaria de existir. Endomorfismo mantém a fase inspecionável.

### Limite de profundidade

Uma macro pode expandir para código que usa outras macros. Sem recursão (Q8) uma
macro não pode se invocar, mas ciclos indiretos entre macros ainda são
construíveis. Limite de **64** níveis, com `LAP0505` — a mesma postura do limite de
200 do parser e dos 10.000 de profundidade de chamada.

---

## Testes necessários

### Parsing de macros

| Teste | Fonte | Esperado |
|---|---|---|
| `Macro_SingleRule` | `macro m match Expression:e expand { e };` | 1 regra |
| `Macro_MultipleRules` | duas cláusulas `match`/`expand` | 2 regras |
| `Macro_PatternWithLiteral` | `match Identifier:i in Expression:c Block:b` | `PatternLiteral("in")` |
| `Macro_PatternWithRepeat` | `Type:t* separado por ,` | `PatternRepeat` |
| `Macro_PatternWithGroupRepeat` | `(Pattern:a Block:b)* separado por ,` | listas paralelas |
| `Macro_UnknownCategory` | `match Foo:x` | `LAP0504` |
| `Macro_RequiresExpand` | `match` sem `expand` | `LAP0101` |
| `Macro_IsNotAValue` | `def m = macro ...;` | `LAP0510` |
| `Macro_NamespaceIsSeparate` | `macro log` e `def log = fn ...` | convivem |

### Delimitação da invocação

| Teste | Fonte | Asserção |
|---|---|---|
| `Invocation_EndsAtSemicolon` | `@log value;` | tokens = `value` |
| `Invocation_EndsAtBlock` | `@unless c { a(); }` | inclui o bloco |
| `Invocation_CountsNesting` | `@foo { { } }` | não termina na chave interna |
| `Invocation_InsideCall` | `f(@foo a, b)` | para na vírgula |

### Matching

| Teste | Fonte | Esperado |
|---|---|---|
| `Match_ExpressionCapture` | `@square x + 1` | captura `x + 1` inteiro |
| `Match_BlockCapture` | `@unless c { a(); }` | expressão e bloco separados |
| `Match_LiteralToken` | `@foreach i in xs { }` | casa |
| `Match_LiteralToken_Wrong` | `@foreach i from xs { }` | `LAP0501` |
| `Match_NoRule` | `@increment 10` com `match Identifier:n` | `LAP0501` |
| `Match_Ambiguous` | `Expression:e` e `Identifier:x` para `@foo x` | `LAP0502` |
| `Match_LeftoverTokens` | sobra de tokens | `LAP0501` |
| `Match_FailedAttempt_ReportsNothing` | regra que falha | zero diagnósticos da tentativa |

### Expansão

| Teste | Fonte | Saída |
|---|---|---|
| `Expand_Statement` | `@log value;` | `print(value);` |
| `Expand_Expression` | `def r = @square x;` | `(x * x)` |
| `Expand_Nested` | macro que usa outra macro | expande as duas |
| `Expand_DepthLimit` | ciclo indireto | `LAP0505` |
| `Expand_WrongContext` | statements em posição de expressão | `LAP0506` |
| `Expand_PreservesSpans` | erro no código expandido | span da invocação |

### Higiene

| Teste | Fonte | Asserção |
|---|---|---|
| `Hygiene_IntroducedNameDoesNotCapture` | macro define `temp`, programa também | não colidem |
| `Hygiene_CapturedNameKeepsMeaning` | `@example value` | refere o `value` do programa |
| `Hygiene_TwoExpansions_DoNotCollide` | mesma macro duas vezes | marcas distintas |
| `Hygiene_NoDuplicateDefinition` | macro com `def` usada duas vezes no bloco | sem `LAP0202` |

### Propriedade

| Teste | Asserção |
|---|---|
| `Expander_NeverThrows` | property sobre fontes arbitrárias |
| `Expansion_Terminates` | property: sempre termina ou reporta `LAP0505` |
| `NoMacros_IsIdentity` | programa sem `@` sai idêntico da expansão |

O último é o que garante que este plano **não pode quebrar M1–M4**.

---

## Critérios de conclusão

- [x] `NoMacros_IsIdentity` verde — a suíte inteira anterior intacta.
- [x] `@unless`, `@square`, `@log` e a forma de `@foreach` funcionando ponta a ponta.
- [x] Ambiguidade e ausência de match com código e span.
- [x] Higiene testada nos dois sentidos (introduzido e capturado).
- [x] `lapis expand` operante.
- [x] `Lapis.Macros` sem referência a `TypeChecker` ou `Evaluator`.

---

## O que a implementação mudou no plano

### `Lapis.Ast` passou a enxergar `Lapis.Lexer`

§17.2 define `MacroInvocation` guardando `ImmutableArray<Token>` — e `Token` mora
em `Lapis.Lexer`. A dependência inversa (`Lexer → Ast`) existia no `.csproj` e
**nunca foi usada**: o lexer não referencia nada de `Lapis.Ast`. Removida ela, a
direção certa aparece sozinha.

É uma afirmação arquitetural, não um atalho: a Surface AST é a saída do parser, e
uma invocação de macro carrega sintaxe **não parseada** por design (§9 da spec de
macros). Tokens são parte da representação de superfície.

### Higiene precisou de um nome *lexável*

§17.8 diz que `@` não é lexável em identificadores, "então colisão é impossível".
Isso era verdade antes de `@` virar o sigilo de invocação — e continuou verdade
para o *lexer*, o que criou um problema novo: `def temp@1 = ...` é impresso pelo
`CoreSourcePrinter`, e o round-trip exige que o impresso **reparseie**. Sem isso o
`lapis pe` deixaria de emitir programa executável para qualquer fonte com macro.

O lexer passou a aceitar `@dígitos` como sufixo de identificador, e só aí: `@` no
início continua sendo a invocação. O custo é que a garantia enfraquece de
"impossível" para "reservado" — um nome como `temp@1` é escrevível à mão. Não há
como manter as duas coisas: um nome que a expansão produz e o printer imprime tem
de ser um nome que o lexer lê.

### Um `def` cujo nome é captura usa o nome capturado

`match Identifier:i ... expand { def i = ...; }` precisa declarar o nome que o
autor escreveu, não a letra que a macro usou. A higiene se aplica ao que a macro
**introduz**; um nome que veio de captura é do programa.

### Higiene renomeia o que a macro *liga*, não tudo

A leitura literal de §17.8 ("todo identificador que não vem de captura") renomeia
`print` também. O que a macro introduz são os `def`, `var` e `label` escritos
dentro do `expand`; o resto são referências livres, que precisam resolver no
escopo de quem invocou.

### A invocação é gulosa até o delimitador

Consequência direta de §17.3, e vale registrar porque surpreende: em
`expand { (@square e * @square e) }` a primeira invocação engole
`e * @square e`. É o mesmo mecanismo que faz `@square x + 1` capturar `x + 1`
inteiro (§4.1 da spec) — quem quer o contrário parenteza:
`((@square e) * (@square e))`.

---

## O que ficou de fora, e por quê

**Repetição em `expand`.** O padrão `Type:t* separado por ,` casa e liga listas
paralelas — está implementado e testado. Usar essas listas no `expand` exigiria
uma sintaxe de splice (`campo...`), que a spec §5.2 menciona mas nenhum dos testes
deste plano exercita, e que precisa de regra de emenda em cada posição onde uma
lista pode aparecer (statements, argumentos de chamada, elementos de array).

Nenhuma das macros que o plano exige — `@unless`, `@square`, `@log`, `@foreach` —
usa repetição. Entra junto com a primeira macro que precisar dela.

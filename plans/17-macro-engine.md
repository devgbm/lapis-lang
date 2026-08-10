# Plano 17 — Macro Engine

**Projeto:** `Lapis.Macros` (novo), `Lapis.Parser`, `Lapis.Cli`
**Milestone:** M11
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
sealed record MacroExpression(ImmutableArray<MacroRule> Rules) : Expression;

sealed record MacroRule(
    MacroPattern Pattern,
    BlockExpression? Constraint,     // plano 18
    ImmutableArray<Statement> Expansion,
    Expression? ExpansionTail) : SurfaceNode;

abstract record MacroPattern : SurfaceNode;
  sealed record PatternSequence(ImmutableArray<MacroPattern> Items) : MacroPattern;
  sealed record PatternCapture(SyntaxCategory Category, string Name) : MacroPattern;
  sealed record PatternLiteral(string Text) : MacroPattern;
  sealed record PatternRepeat(PatternCapture Item, string Separator) : MacroPattern;

enum SyntaxCategory { Expression, Statement, Block, Type, Identifier, Literal, Int, Float, Str, Bool }

sealed record MacroInvocation(
    string Name,
    ImmutableArray<Token> Arguments) : Expression
{
    public required SourceSpan NameSpan { get; init; }
}
```

`MacroExpression` é uma expressão porque a spec adotou `def x = macro ...` (Q19) —
mesma forma de `fn`, `type` e `enum`.

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
regra de `def` (Q8). Uma pré-passagem varre os `DefStatement` de topo cujo valor é
`MacroExpression` e registra na ordem em que aparecem.

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
| `PatternRepeat(item, ",")` | repete até acabarem os tokens |

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
def example = macro
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
| `Macro_SingleRule` | `def m = macro match Expression:e expand { e };` | 1 regra |
| `Macro_MultipleRules` | duas cláusulas `match`/`expand` | 2 regras |
| `Macro_PatternWithLiteral` | `match Identifier:i in Expression:c Block:b` | `PatternLiteral("in")` |
| `Macro_PatternWithRepeat` | `MatchArm:arms* separado por ,` | `PatternRepeat` |
| `Macro_UnknownCategory` | `match Foo:x` | `LAP0504` |
| `Macro_RequiresExpand` | `match` sem `expand` | `LAP0101` |

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

- [ ] `NoMacros_IsIdentity` verde — a suíte inteira anterior intacta.
- [ ] `@unless`, `@square`, `@log` e `@foreach` funcionando ponta a ponta.
- [ ] Ambiguidade e ausência de match com código e span.
- [ ] Higiene testada nos dois sentidos (introduzido e capturado).
- [ ] `lapis expand` operante.
- [ ] `Lapis.Macros` sem referência a `TypeChecker` ou `Evaluator`.

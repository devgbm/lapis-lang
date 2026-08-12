# Plano 26 — Controle de fluxo estruturado: `if`/`loop`/`break`/`continue`

**Projeto:** `Lapis.Ast`, `Lapis.Parser`, `Lapis.Desugar`, `Lapis.TypeChecker`, `Lapis.Evaluator`, `Lapis.PartialEvaluator`
**Milestone:** M16 (com o plano 25)
**Decisão:** [Apêndice C — Q32](appendix-c-decisions.md), "Derrubar `goto`/`label`; controle estruturado"
**Substitui:** [plano 16](16-goto-and-labels.md) (`goto`/`label`, M6) — ver a nota no topo daquele plano
**Depende de:** 02, 04, 05, 06, 08 (M1–M4); nada do que este plano faz depende do
plano 16 ter existido — a substituição é direta

---

## Objetivo

Trocar `goto`/`label` — e o mecanismo de join point que os checava — por controle
de fluxo estruturado: `if`/`else` (já existente, ganhando corpo sem chaves),
`loop`, `break` (com valor) e `continue`, com rótulo opcional para laço aninhado.

A motivação é a Q32: o problema que travou a §25.4 do plano 25 (onde fica o
binding de `is` atravessando um `goto`) não tem resposta boa porque a pergunta
está errada. Com blocos léxicos comuns no lugar de join points, a pergunta não
chega a existir.

## Escopo

**Entra:** `loop`, `break`, `continue` na linguagem; rótulo opcional em `loop`
para `break`/`continue` de laço externo; `break` com valor; `if`/`else` aceitando
uma expressão qualquer no lugar do bloco (exceto outro `if` sem chaves — ver
§26.9); retirada de `goto`/`label`, `CoreGoto`/`CoreGotoIf`/`CoreLabeled`/
`CoreJoin`, e do particionamento de rótulos em grupos no desugar
(`Desugarer.CanSplitBefore`/`LastOfGroup`); reescrita de `@unless`/`@while` sobre
a base nova.

**Fica de fora:** `@until`/`@for` como macros novas — o autor já as marcou como
"ainda válidas", não como parte desta troca; ficam para uma milestone de macros de
controle futura, sobre a mesma base de `loop`/`break`. Join com parâmetros
(mencionado no plano 16 e na Q25 como evolução possível) sai de cogitação — não é
mais rota para nada, porque o problema que o motivava (escopo através de `goto`)
não existe mais.

---

## O que será construído

### 26.1 Surface AST

```csharp
// Lapis.Ast/Surface/SurfaceNodes.cs

// Then/Else deixam de ser sempre BlockExpression: um `if` sem chaves é uma
// expressão qualquer, exceto outro `if` sem chaves (§26.9). O bloco continua
// sendo o caso comum — e o único jeito de escrever um corpo com mais de uma
// linha.
public sealed record IfExpression(Expression Condition, Expression Then, Expression? Else) : Expression;

public sealed record LoopExpression(string? Label, BlockExpression Body) : Expression
{
    public SourceSpan? LabelSpan { get; init; }
}

public sealed record BreakExpression(string? Label, Expression? Value) : Expression
{
    public SourceSpan? LabelSpan { get; init; }
}

public sealed record ContinueExpression(string? Label) : Expression
{
    public SourceSpan? LabelSpan { get; init; }
}
```

`IfExpression.Then` muda de `BlockExpression` para `Expression` — é a única
mudança de forma num nó existente. `loop`/`break`/`continue` são `Expression`,
não `Statement`: com `break` carregando valor, `def x = loop { ... break 5; };`
precisa fazer sentido, do mesmo jeito que `def x = if c { 1 } else { 2 };` já faz.
É a diferença de fundo com o plano 16, onde `goto`/`label` eram `Statement`
justamente porque não produziam valor.

### 26.2 Core AST

```csharp
// Lapis.Ast/Core/CoreNodes.cs

sealed class CoreIf(int nodeId, SourceSpan span, CoreExpr condition, CoreExpr then, CoreExpr @else)
    : CoreExpr(nodeId, span)
{
    // Sem mudança de forma — Then já era CoreExpr aqui. A mudança é só na Surface;
    // a Core nunca soube se o `Then` veio com chaves.
}

sealed class CoreLoop(int nodeId, SourceSpan span, string? label, CoreExpr body)
    : CoreExpr(nodeId, span);

sealed class CoreBreak(int nodeId, SourceSpan span, string? label, CoreExpr? value)
    : CoreExpr(nodeId, span);

sealed class CoreContinue(int nodeId, SourceSpan span, string? label)
    : CoreExpr(nodeId, span);
```

**3 nós novos, 4 saem** (`CoreGoto`, `CoreGotoIf`, `CoreLabeled`, `CoreJoin`).
`CoreIf` não muda de forma — só o que a Surface aceita no lugar de `Then` muda.

### 26.3 Lexer e parser

Duas palavras reservadas novas: `loop`, `break`, `continue` — três, não duas.
`if`/`else` já existem.

```ebnf
if_expr       = "if" expression if_body ( "else" if_body )? ;
if_body       = block | non_if_expression ;

loop_expr     = "loop" ( ":" IDENT )? block ;
break_expr    = "break" ( ":" IDENT )? expression? ;
continue_expr = "continue" ( ":" IDENT )? ;
```

O rótulo de `loop`/`break`/`continue` usa o mesmo marcador `:` nos dois lados —
declaração e referência —, e por um motivo de gramática, não só de estilo: sem
ele, `break x;` seria ambíguo entre "rótulo `x`" e "valor `x`". Com `:`,
`break x;` é sempre valor; `break :x;` é sempre rótulo; `break :x, valor;` é os
dois. (A vírgula entre rótulo e valor evita `break :x valor;` parsear como
`break :x` seguido de uma expressão solta — ver §26.9 para a mesma preocupação em
`if`.)

Rótulos de `loop` vivem no mesmo espaço de nomes separado que os de `label`
viviam (plano 16 §16.3) — um `loop :x` e um `def x` convivem.

**`if_body` exclui `if_expr` de si mesmo quando não há chaves** — é a peça que
evita dangling-else, e está detalhada em §26.9.

### 26.4 Desugar

Trivial, comparado ao plano 16 §16.4: não há decomposição em blocos básicos, não
há partição de rótulos em grupos, não há `CanSplitBefore`/`LastOfGroup`. Cada nó
de Surface vira o nó de Core correspondente, estruturalmente:

```csharp
IfExpression n     => _factory.If(n.Span, Desugar(n.Condition), Desugar(n.Then), DesugarElse(n.Else)),
LoopExpression n   => _factory.Loop(n.Span, n.Label, Desugar(n.Body)),
BreakExpression n  => _factory.Break(n.Span, n.Label, n.Value is null ? null : Desugar(n.Value)),
ContinueExpression n => _factory.Continue(n.Span, n.Label),
```

`DesugarElse(null)` continua produzindo `Unit` — igual a hoje. Todo o código de
`Desugarer.cs` sobre decomposição (`DesugarWithLabels`, `DesugarLabelGroup`,
`LastOfGroup`, `CanSplitBefore`, `FallThrough`) sai inteiro.

### 26.5 Type checker

| Nó | Tipo | Verificações |
|---|---|---|
| `CoreIf` | `Join(Then, Else)` | condição `Bool` (`LAP0230`, sem mudança); ramos incompatíveis (`LAP0231`, sem mudança) |
| `CoreBreak` | `Never` | dentro de um `loop` (`LAP0523`); rótulo existe e está em escopo (`LAP0524`/`LAP0525`) |
| `CoreContinue` | `Never` | idem |
| `CoreLoop` | ver abaixo | — |

**`Never` continua a mesma mecânica de sempre (Q13).** `break`/`continue` são
`Never`, exatamente como `return`/`throw` já são — nenhuma regra de tipo nova, só
mais dois nós na lista. `def x = if c { break; } else { 1 };` tipa `Int` pela
mesma conta que já tipa `if c { return 1; } else { 2 };` hoje.

**O tipo de `CoreLoop` é a junção de todo `break` que o alcança.** Um percurso da
árvore coleta cada `CoreBreak` cujo rótulo é `null` ou é o rótulo deste `loop`,
sem descer para dentro de um `loop` aninhado **sem rótulo que aponte para fora**
— mesma forma de fronteira que `ReturnAnalysis` já usa para não atravessar função.

- **Nenhum `break` alcança:** o tipo é `Never`. O laço, se termina, só termina por
  `return`/`throw`/`break` de um laço **externo** — nunca por si.
- **Um ou mais alcançam:** o tipo é o `Join` de cada valor (`break;` sem valor
  conta como `Void`) — reaproveita `TypeRelations.Join`, o mesmo que já fecha
  `.[1, .[2]]` no span literal (plano 24) e a terceira regra de subtipagem
  (`T<A,B> <: T<?,?>`, plano 23). Incompatível é `LAP0526`.

`continue` não entra na junção — ele nunca produz o valor final do laço, só
reinicia a iteração.

**`ReturnAnalysis` ganha o caso `Loop`:** `DR(Loop) = true` quando o laço não tem
`break` que o alcance com completion normal — o mesmo raciocínio de
`DR(Goto L) = DR(L)` do plano 16, adaptado: se nada sai normalmente por aqui, o
código depois é inalcançável, e não precisa de `return` próprio. **A mesma
armadilha do plano 16 se aplica**: código depois de um `loop {}` sem `break`
autêntico é inalcançável, e `LAP0273` precisa saber disso sem acusar
erroneamente — não há salto implícito para marcar desta vez (não há mais salto
nenhum), mas vale testar explicitamente contra essa forma.

### 26.6 Evaluator

```csharp
enum CompletionKind { Normal, Return, Break, Continue, Abort }
```

`Break`/`Continue` carregam o rótulo (`string?`) e, no caso de `Break`, o valor.

`CoreLoop` avalia o corpo **em C# `while(true)`, não recursão** — é o mesmo
requisito do plano 16 §16.6 ("a pilha de C# não cresce"), e continua sendo o que
mantém `@while`/`loop` viáveis sem risco de stack overflow no interpretador,
Q8 intacta:

- completion `Normal` → mais uma volta (é o `continue` implícito de cair no fim
  do corpo);
- completion `Continue(null)` ou `Continue(esteRótulo)` → mais uma volta;
- completion `Continue(outroRótulo)` → sobe sem consumir — é de um laço externo;
- completion `Break(null, v)` ou `Break(esteRótulo, v)` → o `loop` completa
  `Normal` com valor `v` (ou `Unit`);
- completion `Break(outroRótulo, v)` → sobe sem consumir;
- `Return`/`Abort` → sobem sem consumir, como sempre.

O orçamento de iterações é a mesma ideia do `LAP0303` do plano 16, só que
contando voltas do `loop` em vez de saltos — **mesmo código, mesmo número**, só
o nome interno muda. Não é um novo diagnóstico: é o mesmo, gerado por um
mecanismo mais simples.

### 26.7 Partial evaluator

Trabalho de troca, não de invenção: `Effects.IsPure`, `FreeVariables` e
`PartialEvaluator.Specialize` tinham casos para `CoreGoto`/`CoreGotoIf`/
`CoreLabeled`/`CoreJoin` desde o M7 — trocam por casos para `CoreLoop`/
`CoreBreak`/`CoreContinue`.

O ganho não é só código a menos: um `CoreLoop` é um escopo léxico só, sem
ambiente de join a reconstruir por predecessor. Isso é o substrato padrão da
literatura de PE (Jones/Gomard/Sestoft) justamente porque especializar não
precisa provar nada sobre "quem mais chega aqui" — só há um caminho de entrada.
Os planos 13/14 (especialização, bounds-check elimination) herdam essa base sem
ter que redescobri-la.

### 26.8 Printers

`CoreSourcePrinter` imprime `CoreLoop`/`CoreBreak`/`CoreContinue` diretamente —
sem reconstrução nenhuma, porque não há decomposição a desfazer. É outra
simplificação em relação ao plano 16 §16.7, que precisava reconstituir a forma de
`label` a partir de `Labeled`.

### 26.9 `if`/`else` sem chaves, e o dangling-else

```c
if <condição> break;
if <condição> break; else continue;
```

`Then`/`Else` sem chaves aceitam **qualquer expressão**, com uma exclusão: não
pode ser outro `if` sem chaves.

```c
if a if b break;              // ✗ LAP0527 — se a intenção é aninhar, use chaves
if a { if b break; }          // ✓
if a { if b { break; } else { continue; } } else { }   // ✓
```

**Por que a exclusão, e não uma regra de precedência.** Sem ela, `if a if b
break; else other;` reabriria o dangling-else clássico: o `else` pertence ao `if
b` ou ao `if a`? A saída de C ("liga ao `if` mais próximo") é uma regra que o
leitor precisa simular de cabeça — o mesmo argumento que já decidiu não haver
regra de especificidade em `LAP0720` (plano 23) e não haver dominância na Q23.
Recusar aninhar sem chaves elimina a pergunta em vez de respondê-la: **antes**
desta mudança, `Then` era sempre bloco e o dangling-else não existia; a exclusão
devolve exatamente essa garantia.

Isto não é regra nova para o resto da linguagem — é a Q16 ("statements que
terminam em bloco dispensam `;`") valendo pela primeira vez para `if`, porque
antes `Then`/`Else` eram sempre bloco e a regra nunca era exercitada. `if c
break;` termina em expressão simples e pede `;`; `if c { break; }` termina em
bloco e dispensa.

A Core não sabe a diferença — `CoreIf.Then` é `CoreExpr` desde sempre, com ou
sem chaves na fonte.

---

## Decisões de design

### Por que blocos léxicos resolvem o que join points não resolviam

Um `label` é alcançável de vários lugares — é um join point por definição. Um
bloco de `if`/`loop` tem **uma** entrada. `if e is Some(v) { use(v); }` não tem
"outro caminho" até `use(v)` que não passe pela ligação — porque não há "até
ali" fora do bloco. O problema da §25.4 não fica mais fácil: some.

### Por que `break` carrega valor e `continue` não

Simetria com `if`/`match`, que já produzem valor — e o preço de não deixar seria
reintroduzir statement/expression como duas categorias onde uma bastava.
`continue` não tem "valor final" para carregar: ele reinicia, não sai — não há
o que devolver ao lugar que perguntou pelo tipo do `loop`.

### Por que rótulo com `:`, e não um sistema de nomes implícito

`break x;`/`break x, 1;` sem marcador é ambíguo entre rótulo e valor — os dois
são identificadores/expressões na mesma posição gramatical. `:` resolve sem
backtracking: o parser nunca precisa tentar as duas leituras. É o mesmo problema
que a Q5 resolveu para `<` com uma regra determinística, só que aqui a regra é
mais barata — um token, não uma lista de tokens que iniciam expressão.

### Por que este plano não herda a Q24/Q25 inteiras

A Q24 (`goto` para trás) e a subseção de escopo entre joins da Q25 ficam
históricas — foram revertidas, não estendidas. **A Q25 principal (mutação com
`var`) continua de pé e é usada por este plano sem alteração**: um `loop` com
progresso ainda precisa de `var`, do mesmo jeito que um `goto` para trás
precisava. Só o mecanismo de salto por baixo mudou.

---

## Migração do que já existia

O plano 16 foi implementado (M6) e a regrouping (tasks internas, sem plano
próprio) corrigiu o escopo depois. Este plano **substitui**, não estende:

- `src/Lapis.Ast/Surface/SurfaceNodes.cs` — `GotoStatement`/`LabelStatement` saem;
  `IfExpression.Then` muda de tipo; `LoopExpression`/`BreakExpression`/
  `ContinueExpression` entram.
- `src/Lapis.Ast/Core/CoreNodes.cs` — `CoreGoto`/`CoreGotoIf`/`CoreLabeled`/
  `CoreJoin` saem; `CoreLoop`/`CoreBreak`/`CoreContinue` entram.
- `src/Lapis.Desugar/Desugarer.cs` — `DesugarWithLabels`, `DesugarLabelGroup`,
  `LastOfGroup`, `CanSplitBefore`, `FallThrough` saem inteiros.
- `src/Lapis.TypeChecker/TypeChecker.cs` — o caso `Labeled`/`Goto`/`GotoIf` de
  `ReturnAnalysis` e de `CheckExpression` saem; entra `If`/`Loop`/`Break`/
  `Continue`.
- `src/Lapis.Evaluator/Evaluator.cs` — `CompletionKind.Goto` vira `Break`/
  `Continue`; o laço de consumo de completions em `CoreLabeled` vira o `while`
  de `CoreLoop`.
- `src/Lapis.PartialEvaluator/*` — casos de `CoreGoto`/`CoreLabeled` trocam por
  `CoreLoop`/`CoreBreak`/`CoreContinue` em `Effects`, `FreeVariables` e
  `PartialEvaluator.Specialize`.
- `src/Lapis.Runtime/Resources/prelude.ls` — `@unless`/`@while` reescrevem o
  corpo:

  ```c
  macro unless
      match Expression:condition Block:body
      expand { if !condition { body; } };

  macro while
      match Expression:condition Block:body
      expand { loop { if condition { body; } else { break; } } };
  ```

  A posição de `condition` no `while` novo não é acidente: é a posição 1 da
  regra de escopo do `is` (plano 25 §25.3, "condição de `if`, ramo verdadeiro").
  `@while e is Some(v) { usa(v); }` liga `v` dentro do corpo **de graça**, sem
  nada especial no `is` nem no `@while` — a macro expande para a forma que já
  tem a leitura certa.

- `tests/conformance/eval/goto/*`, `tests/conformance/eval/mutation/
  scope_across_joins*.ls`, `tests/conformance/types/lap052x_*.ls`,
  `tests/conformance/types/lap0230_goto_condition_must_be_bool.ls`,
  `examples/goto.ls` — reescritos ou removidos; o padrão que cada um demonstrava
  passa a ter a forma `if`/`loop`/`break`.
- `examples/control.ls`, `examples/loops.ls` — reescritos sobre `loop`/`break`/
  `continue`.
- `spec/lapislang-macros-0.1.md` §10 — reescrita: descreve `goto`/`label` hoje.

---

## Diagnósticos

```text
LAP0523  'break'/'continue' fora de um 'loop'
LAP0524  o rótulo '{0}' não existe
LAP0525  o rótulo '{0}' pertence a uma função externa
LAP0526  valores de 'break' no mesmo loop têm tipos incompatíveis: '{0}' e '{1}'
LAP0527  'if'/'else' sem chaves não pode ter 'if' como corpo direto; use chaves
```

Preenchem `LAP0523`–`LAP0527`, que estavam livres (nunca chegaram a ser
publicados — ver Apêndice B, "Controle de fluxo"). `LAP0520`–`LAP0522` (`goto`/
`label`) e `LAP0731` (`is` através de salto) ficam **retirados e não
reciclados** — código não volta ao pool depois de implementado, mesma regra de
`LAP0301`/`LAP0706`. `LAP0230`/`LAP0231` (condição/ramos de `if`) e `LAP0303`
(orçamento — "saltos" vira "iterações" na mensagem, mesmo número) continuam
vivos, só que agora servidos por um mecanismo mais simples.

Não há diagnóstico de "rótulo de `loop` duplicado": um `loop :x` aninhado dentro
de outro `loop :x` sombreia, como qualquer nome — a mesma permissividade que
`def x = 1; { def x = 2; }` já tem. `label` exigia unicidade porque o mecanismo
de grupo (plano 16 §16.5) precisava; `loop` não tem grupo.

---

## Testes necessários

### Parser

| Teste | Fonte | Esperado |
|---|---|---|
| `If_AcceptsBareExpression` | `if c break;` | `Then` não é `BlockExpression` |
| `If_BareThen_RequiresSemicolon` | `if c break` (sem `;`) | `LAP0102` |
| `If_Block_DispensesSemicolon` | `if c { break; }` | sem diagnóstico |
| `If_BareThenIsIf_ReportsLap0527` | `if a if b break;` | `LAP0527` |
| `If_BracedNestedIf_IsFine` | `if a { if b break; }` | compila |
| `Loop_Unlabeled` | `loop { break; }` | `LoopExpression(null, ...)` |
| `Loop_Labeled` | `loop :fora { break; }` | rótulo capturado |
| `Break_Bare` | `break;` | valor `null`, rótulo `null` |
| `Break_WithValue` | `break 5;` | valor capturado |
| `Break_WithLabel` | `break :fora;` | rótulo capturado, sem valor |
| `Break_WithLabelAndValue` | `break :fora, 5;` | os dois capturados |
| `Continue_Bare` | `continue;` | rótulo `null` |
| `Continue_WithLabel` | `continue :fora;` | rótulo capturado |
| `Goto_NoLongerParses` | `goto x;` | `LAP0112`/`LAP0110` — `goto` não é mais palavra-chave |

### Desugar

| Teste | Asserção |
|---|---|
| `If_BareThen_DesugarsLikeBlock` | `if c break;` e `if c { break; }` produzem a mesma `CoreIf` |
| `Loop_RoundTrips` | `CoreSourcePrinter` → reparse ⇒ mesma Core |
| `Break_WithValue_RoundTrips` | idem, com valor |

### Type checker

| Teste | Fonte | Esperado |
|---|---|---|
| `Loop_NoBreak_IsNever` | `def x: Int = { loop { print(1); }; 1 };` código após é inalcançável | tipa; `LAP0273` não dispara no `loop` em si |
| `Loop_WithBreak_IsJoinOfBreaks` | `loop { break 1; }` | `Int` |
| `Loop_MultipleBreaks_Join` | `break 1;` e `break;` no mesmo loop | `LAP0526` |
| `Loop_BreaksJoinAcrossIf` | `break` dentro de `if`/`else` do corpo | junta normalmente |
| `Break_OutsideLoop_ReportsLap0523` | `break;` fora de `loop` | `LAP0523` |
| `Continue_OutsideLoop_ReportsLap0523` | `continue;` fora de `loop` | `LAP0523` |
| `Break_UnknownLabel_ReportsLap0524` | `loop { break :outro; }` | `LAP0524` |
| `Break_LabelOfOuterFunction_ReportsLap0525` | rótulo de fora da função | `LAP0525` |
| `Break_TargetsCorrectLoop_WhenNested` | `loop :fora { loop { break :fora, 1; } }` | tipo do `loop :fora` é `Int` |
| `Continue_DoesNotJoinIntoLoopType` | `loop { continue; break 1; }` | tipo `Int`, sem contribuição do `continue` |
| `Loop_Shadowing_IsAllowed` | `loop :x { loop :x { break :x; } }` | sem diagnóstico; alcança o mais interno |
| `MissingReturn_SeesLoopWithoutBreak` | função `Int` cujo único caminho é um `loop {}` sem `break` | não dispara `LAP0272` |

### Evaluator

| Teste | Fonte | Saída |
|---|---|---|
| `Loop_BreaksImmediately` | `loop { break 1; }` | `1` |
| `Loop_ContinuesThenBreaks` | contador com `continue`/`break` | valor esperado |
| `Loop_Nested_BreakTargetsLabeled` | `break :fora` de dentro de laço interno | sai dos dois |
| `Loop_Nested_ContinueTargetsInner` | `continue` sem rótulo | reinicia só o interno |
| `Loop_DoesNotGrowStack` | 100.000 iterações | sem stack overflow |
| `Loop_InfiniteWithoutBreak_Aborts` | `loop { }` | `LAP0303` |
| `If_BareThen_Evaluates` | `if true break; else continue;` dentro de loop | mesmo resultado da forma com chaves |
| `Unless_ByHand` | `if !condition { body; }` | equivale ao `@unless` |
| `While_ByHand` | `loop { if condition { body; } else { break; } }` | itera o número certo de vezes |
| `While_WithIs_BindsInsideBody` | `@while e is Some(v) { usa(v); e = prox(); }` | `v` visível e correto a cada volta |

### Propriedade

| Teste | Asserção |
|---|---|
| `Loop_TerminatesOrAborts` | todo programa termina **ou** reporta `LAP0303` |
| `IfBareForm_EquivalentToBlockForm` | `if c e; else f;` ≡ `if c { e; } else { f; }`, mesma saída |

### Corpus

- Equivalência do PE inalterada sobre o corpus migrado.
- `examples/control.ls`, `examples/loops.ls` reescritos e rodando.

---

## Critérios de conclusão

- [ ] `goto`/`label` removidos: não lexam mais como palavra-chave.
- [ ] `if`/`else` aceitando corpo sem chaves, com `LAP0527` fechando o
      dangling-else.
- [ ] `loop`/`break`/`continue` parseando, desugarando, tipando e executando.
- [ ] `break` com valor; tipo do `loop` é a junção dos `break` que o alcançam.
- [ ] Rótulo de `loop`/`break`/`continue` com `:`, resolvendo por nome, com
      sombreamento permitido.
- [ ] `ReturnAnalysis` cobrindo `Loop` sem falso positivo em `LAP0272`/`LAP0273`.
- [ ] Iteração em C# `while`, não recursão — sem stack overflow em laço longo.
- [ ] `@unless`/`@while` reescritos sobre a base nova, incluindo
      `While_WithIs_BindsInsideBody`.
- [ ] PE (`Effects`, `FreeVariables`, `Specialize`) com casos para
      `CoreLoop`/`CoreBreak`/`CoreContinue`, sem caso vivo para os nós antigos.
- [ ] Corpus migrado; equivalência do PE inalterada.
- [ ] Zero regressão fora do que este plano explicitamente muda.

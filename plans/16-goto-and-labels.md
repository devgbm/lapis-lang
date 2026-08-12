# Plano 16 — `goto` e `label`

> **⚠️ SUPERADO pela Q32 e pelo [plano 26](26-structured-control-flow.md).**
> `goto`/`label` saem da linguagem; `if`/`loop`/`break`/`continue` entram no
> lugar. A razão está na Q32 (Apêndice C): o único uso real de `goto`/`label` no
> corpus era o padrão de laço e o padrão de desvio condicional, e o segundo já
> era redundante com o `if`/`else` que a linguagem tem desde o M3.
>
> Este plano fica como registro histórico — é a razão de join points terem
> existido, e o que a implementação aprendeu com eles (a seção final, "laço com
> progresso") é parte do argumento da Q32. Nada abaixo desta nota descreve a
> linguagem atual.

**Projeto:** `Lapis.Ast`, `Lapis.Parser`, `Lapis.Desugar`, `Lapis.TypeChecker`, `Lapis.Evaluator`
**Milestone:** M6 — retirado no plano 26
**Spec:** [`lapislang-macros-0.1.md` §10](../spec/lapislang-macros-0.1.md) *(seção substituída)*
**Depende de:** 02, 04, 05, 06, 08 (M1–M4 concluídos)

---

## Objetivo

Dar à linguagem a primitiva de controle de fluxo sobre a qual `@unless` e `@while`
serão construídos (plano 20), sem quebrar nada do que existe.

Este plano é **independente do sistema de macros**. `goto`/`label` são úteis
sozinhos: eles dão ao partial evaluator um grafo de fluxo explícito, que é o que
os planos 14 (bounds-check elimination) e 15 (tracing) querem analisar.

## Escopo

**Entra:**

- `goto L;`, `goto L if e;`, `label L;` na linguagem de superfície;
- `CoreGoto`, `CoreGotoIf`, `CoreLabeled` na Core AST;
- decomposição em blocos básicos no desugar;
- tipagem (`Never` / `Void` / junção) e a regra de escopo do salto;
- salto **para trás**, e com ele o limite de saltos do evaluator (`LAP0303`);
- `CompletionKind.Goto` no evaluator.

**Fica de fora:**

- salto entre funções — um `label` é local à função, como `return`;
- salto para dentro de um bloco ainda não aberto;
- remoção de `If`/`Match` da Core — plano 20, e só quando as macros funcionarem;
- construção de um CFG explícito — plano 14 já o quer, e passa a tê-lo de graça.

---

## O que será construído

### 16.1 Surface AST

```csharp
// Lapis.Ast/Surface/SurfaceNodes.cs
sealed record GotoStatement(string Label, Expression? Condition) : Statement
{
    public required SourceSpan LabelSpan { get; init; }
}

sealed record LabelStatement(string Label) : Statement
{
    public required SourceSpan LabelSpan { get; init; }
}
```

São `Statement`, não `Expression`: um salto não produz valor, e mantê-lo fora da
gramática de expressão evita `def x = goto L;`, que não quer dizer nada.

### 16.2 Core AST

```csharp
sealed class CoreGoto   : CoreExpr { string Label; }
sealed class CoreGotoIf : CoreExpr { string Label; CoreExpr Condition; }

sealed record CoreJoin(string Name, CoreExpr Body, SourceSpan Span);

sealed class CoreLabeled : CoreExpr
{
    CoreExpr Entry;
    ImmutableArray<CoreJoin> Joins;
}
```

**19 nós.** `CoreGotoIf` é primitivo em vez de desugarar para `If(cond, Goto, ())`
porque uma macro de controle construída sobre `goto` não pode depender do `if` da
linguagem — a construção seria circular.

### 16.3 Lexer e parser

Dois tokens novos: `GotoKeyword`, `LabelKeyword`. A palavra `if` em
`goto L if e;` reaproveita `IfKeyword` — não há ambiguidade, porque `goto` já
determinou a produção.

```ebnf
goto_stmt  = "goto" IDENT ( "if" expression )? ";" ;
label_stmt = "label" IDENT ";" ;
```

Rótulos são identificadores comuns. **A proposta original escrevia `$end`, que não
lexa**: identificadores são `[A-Za-z_][A-Za-z0-9_]*` (spec §7). Rótulos vivem num
espaço de nomes separado do de valores, então `label x` e `def x` convivem sem
colidir, e a higiene das macros (plano 17) resolve os choques gerados por expansão.

### 16.4 Desugar — decomposição em blocos básicos

O trabalho de verdade deste plano. Um bloco cujos statements contêm `label` é
partido em segmentos:

```c
def x = 1;
goto done if c;
def y = 2;
label done;
print(x);
```

```text
Let(x, 1,
    Labeled(
        entry: Let(_, GotoIf(done, c), Let(y, 2, Goto(done))),
        joins: [ done → Let(_, print(x), ()) ]))
```

Três regras:

1. **`label L;` encerra o segmento corrente** com um `Goto(L)` implícito e abre um
   novo. É o que torna a decomposição um sufixo, e não uma cópia.
2. **O `Labeled` é aberto no primeiro `goto` do bloco.** Tudo declarado antes dele
   continua envolvendo o `Labeled`, e portanto continua em escopo nos dois lados.
3. **Nomes declarados entre o `goto` e o `label` não estão em escopo no destino.**
   Não é limitação: o salto pode tê-los pulado. Usá-los ali é `LAP0201`, com a
   mensagem normal de variável inexistente.

Com salto para trás, os joins de um grupo podem se referenciar mutuamente. A
decomposição não muda — o que muda é que o grafo de joins deixa de ser um DAG.

### 16.5 Type checker

| Nó | Tipo | Verificações |
|---|---|---|
| `CoreGoto` | `Never` | rótulo existe (`LAP0520`) e está no escopo (`LAP0521`) |
| `CoreGotoIf` | `Void` | idem, mais condição `Bool` (`LAP0230`) |
| `CoreLabeled` | `Join(entrada, joins…)` | rótulos duplicados no mesmo grupo (`LAP0522`) |

`Never` já se propaga por `Let`, `If`, `Binary`, `Unary` e `Call` desde o M1, então
`goto` cabe em qualquer posição sem regra nova. É o mesmo mecanismo de `return`
(Q13).

**`ReturnAnalysis`** ganha o caso `Labeled`: `DR(Labeled) = DR(entry) && todos os
joins DR`. Um `Goto` isolado **não** conta como retorno — ele desvia, não sai da
função —, e é por isso que a análise precisa olhar os joins.

Com ciclos entre joins a análise precisa de um ponto fixo: assume-se `DR = true`
para os joins ainda não visitados e itera até estabilizar. É a leitura otimista
padrão, e é correta porque um join que só volta para o laço nunca "cai fora" da
função sem passar por um `return`.

### 16.6 Evaluator

`Completion` ganha um caso, ao lado de `Normal`, `Return` e `Abort`:

```csharp
enum CompletionKind { Normal, Return, Goto, Abort }
```

`CoreLabeled` avalia a entrada; se a completion for `Goto(L)` e `L` for um dos seus
joins, avalia o corpo daquele join — que pode devolver outro `Goto`, e o laço
continua. Qualquer outra completion sobe.

É exatamente o tratamento que `Return` já recebe na fronteira de chamada
(plano 08 §8.6): **nenhuma exceção C#, nenhum mecanismo novo**.

**Iteração, não recursão.** Um salto para trás é apenas mais uma volta do `while` em
C# que consome as completions: o join é reavaliado e **a pilha de C# não cresce**. É
o que torna `@while` viável sem recursão na linguagem e sem risco de stack overflow
no interpretador.

**Terminação deixa de ser garantida.** Até o M4 todo programa terminava por
construção — sem recursão (Q8) e sem laços. `goto` para trás acaba com isso. O
evaluator passa a contar saltos e aborta com `LAP0303` ao passar do limite
(1.000.000), exatamente como `LAP0302` já faz com profundidade de chamada.

### 16.7 Printers

`CoreSourcePrinter` precisa reconstituir a forma de superfície a partir de
`Labeled` para manter o round-trip (plano 02 §2.8): imprimir a entrada, depois
`label L;` seguido do corpo de cada join. É a operação inversa da decomposição, e
existe justamente porque `lapis pe` precisa emitir programas re-parseáveis.

---

## Decisões de design

### Por que join points, e não saltos de verdade

A Core não tem nó `Block` (Q10): um bloco é uma cadeia de `Let`. "Pular para a
instrução 7" não quer dizer nada nessa representação. Join points são a leitura que
a estrutura já suporta — e são a forma que compiladores funcionais usam há décadas
justamente para casar fluxo não estruturado com escopo léxico.

O ganho concreto: **substituição e inlining continuam textuais** no partial
evaluator, que era a razão de não haver `Block` para começo de conversa.

### Por que salto para trás, e o que ele custa

Decisão do autor, e é o que viabiliza `@while` (plano 20). O preço está pago com os
olhos abertos:

| Perde | Ganha |
|---|---|
| garantia de terminação por construção | `@while` no prelude, sem tocar no compilador |
| PE sobre laços é mais difícil que sobre `If` | uma forma só de controle para a análise entender |
| `LAP0303` a mais no evaluator | laços sem precisar de recursão (Q8 segue valendo) |

O item do meio é o mais caro e cai nos planos 13 e 14: especializar um laço exige
*widening* ou combustível. Está registrado lá.

### Por que o salto não sai do escopo

Um `label` é local à função, como `return`. Saltar para dentro de um bloco ainda não
aberto pularia as declarações que ele introduz, e os nomes lá dentro não teriam
sentido — é a mesma razão pela qual nomes declarados entre o `goto` e o `label` não
são visíveis no destino.

### Por que `Statement` e não `Expression`

`def x = goto L;` não quer dizer nada. Manter `goto`/`label` fora da gramática de
expressão elimina a pergunta sem precisar de regra.

---

## Testes necessários

### Parser

| Teste | Fonte | Esperado |
|---|---|---|
| `Goto_Unconditional` | `goto fim;` | `GotoStatement(fim, null)` |
| `Goto_Conditional` | `goto fim if x > 0;` | condição capturada |
| `Label_Declaration` | `label fim;` | `LabelStatement(fim)` |
| `Goto_RequiresSemicolon` | `goto fim` | `LAP0102` |
| `Goto_RequiresIdentifier` | `goto 1;` | `LAP0112` |
| `Label_IsNotAnExpression` | `def x = label a;` | `LAP0110` |

### Desugar

| Teste | Asserção |
|---|---|
| `Block_WithoutLabels_HasNoLabeled` | bloco comum não ganha `Labeled` |
| `Label_SplitsIntoSegments` | um `label` ⇒ um join |
| `Label_TerminatesSegmentWithImplicitGoto` | segmento anterior termina em `Goto` |
| `Labeled_OpensAtFirstGoto` | `def` anterior ao `goto` envolve o `Labeled` |
| `MultipleLabels_ProduceMultipleJoins` | três `label` ⇒ três joins |
| `Labeled_RoundTrips` | `CoreSourcePrinter` → reparse ⇒ mesma Core |

### Type checker

| Teste | Fonte | Esperado |
|---|---|---|
| `Goto_IsNever` | `def x: Int = { goto fim; label fim; 1 };` | tipa |
| `Goto_UnknownLabel` | `goto inexistente;` | `LAP0520` |
| `Goto_Backward_IsAllowed` | `label a; goto a if c;` | tipa |
| `Goto_OutOfScope_IsError` | rótulo de função diferente | `LAP0521` |
| `Label_Duplicate` | `label a; label a;` | `LAP0522` |
| `GotoIf_ConditionMustBeBool` | `goto fim if 1;` | `LAP0230` |
| `Goto_DoesNotCrossFunction` | `goto` para rótulo de fora da função | `LAP0520` |
| `Labeled_JoinsBranchTypes` | joins de tipos incompatíveis | `LAP0231` |
| `ScopeAfterLabel_ExcludesSkipped` | `def` entre `goto` e `label`, usado no destino | `LAP0201` |
| `ScopeAfterLabel_KeepsEarlier` | `def` antes do `goto`, usado no destino | tipa |
| `MissingReturn_SeesJoins` | função `Int` que só retorna na entrada | `LAP0272` |

### Evaluator

| Teste | Fonte | Saída |
|---|---|---|
| `Goto_SkipsStatements` | `goto fim; print(1); label fim; print(2);` | `2` |
| `GotoIf_False_FallsThrough` | condição falsa | executa tudo |
| `GotoIf_True_Jumps` | condição verdadeira | pula |
| `Goto_ChainedJumps` | salto que cai em join que salta de novo | último join |
| `Goto_InsideFunction_DoesNotEscape` | `goto` não vaza da closure | valor da função |
| `Unless_ByHand` | `@unless` escrito à mão com `goto` | equivale ao `if` |
| `While_ByHand` | laço escrito à mão | itera o número certo de vezes |
| `Goto_BackwardLoop_Terminates` | laço com condição de saída | termina |
| `Goto_InfiniteLoop_Aborts` | `label a; goto a;` | `LAP0303` |
| `Goto_Backward_DoesNotGrowStack` | 100.000 iterações | sem stack overflow |

### Propriedade

| Teste | Asserção |
|---|---|
| `Goto_TerminatesOrAborts` | property: todo programa termina **ou** reporta `LAP0303` |
| `GotoForm_EquivalentToIf` | property: `if c { a }` ≡ a forma com `goto`, mesma saída |

O último não é formalidade: ele é a evidência de que `goto`/`label` expressam o
mesmo que `If` expressa. Sem ela, nenhuma macro de controle construída sobre `goto`
merece confiança.

---

## Critérios de conclusão

- [x] `goto`/`label` parseiam, desugaram, tipam e executam.
- [x] Round-trip do `CoreSourcePrinter` verde para programas com rótulos.
- [x] `GotoForm_EquivalentToIf` verde — evidência de que `goto` expressa o mesmo que `If`.
- [x] Salto fora de escopo e rótulo desconhecido rejeitados com código e span.
- [x] Laço infinito abortando com `LAP0303`, sem stack overflow.
- [x] `ReturnAnalysis` cobrindo `Labeled`.
- [x] Zero regressão: a suíte inteira continua verde sem alteração de expectativa.

---

## O que a implementação corrigiu no plano

Quatro pontos deste plano não sobreviveram ao contato com o código. Ficam
registrados porque a diferença é a informação, não o plano original.

### `label` é palavra-chave contextual, não reservada

§16.3 previa dois tokens novos. `label` não pode ser reservada: o exemplo da spec
§13 usa `label` como **nome de campo** (`type<Label: Str, ...> { label: Str; }`), e
reservá-la quebraria um programa documentado. Ela é reconhecida só quando inicia um
statement e vem seguida de um identificador — dois identificadores seguidos nunca
formam expressão, então não há ambiguidade. `goto` é reservada de verdade.

### `DR(Labeled)` não é "entrada e todos os joins"

§16.5 propunha `DR(Labeled) = DR(entry) && todos os joins DR`. Está errado: com essa
regra a função abaixo, que sempre retorna, seria rejeitada com `LAP0272` —
`DR(entry)` é falso porque a entrada termina em salto, não em `return`.

```c
def f = fn() Int { goto fim; label fim; return 1; };
```

A regra correta é **`DR(Goto L) = DR(L)`**: o salto não retorna, quem retorna é o
destino. Daí `DR(Labeled) = DR(entry)` sozinho — todo join é alcançado por salto, e
o salto já consulta o join. O ponto fixo continua necessário, pelo mesmo motivo
(ciclos entre joins), e é o maior ponto fixo: começa otimista e desce.

E um salto **condicional** é um desvio de duas saídas, com a continuação sendo a
segunda: `DR(Let(_, GotoIf(L), resto)) = DR(L) && DR(resto)`, exatamente como um
`If`. Tratado no caso do `Let`, porque o nó sozinho não enxerga a sua continuação.

### O salto implícito precisa ser marcado

O salto que fecha um segmento é gerado pelo desugar, não escrito por ninguém. Sem
uma marca (`CoreGoto.IsImplicit`) ele causa dois defeitos: `LAP0273` acusa "código
inalcançável após return" apontando para um `label` perfeitamente alcançável, e o
printer não tem como distinguir o salto que deve omitir daquele que deve imprimir.

### `LAP0231` no `Labeled` é inalcançável hoje

Todo caminho menos o último termina em salto, e salto é `Never` — a junção nunca
falha. O código fica como está (é a operação correta, e passa a importar no dia em
que um join tiver parâmetros), mas o diagnóstico não tem caso de conformidade
porque não tem como ser produzido.

---

## O que ficou faltando, e não é bug: laço com progresso

**Um laço construído com salto para trás não avança.** Bindings são imutáveis e o
corpo de um join roda no ambiente do grupo, o mesmo em toda volta: nada muda entre
iterações, então a condição de saída também não muda.

```c
def i = 0;
label repete;
def j = i + 1;
print(j);
goto repete if j < 3;   // imprime 1 para sempre, até LAP0303
```

Isso **não** invalida o M6: saída antecipada sem aninhamento, `@unless` e o grafo de
fluxo explícito para os planos 14 e 15 já são o que este plano prometeu, e estão
verdes. O que fica bloqueado é `@while` (plano 20), até que se escolha entre join
com parâmetros, mutação, ou laço por recursão. A spec de macros §10.6 registra a
comparação.

# Plano 24 — Span: sequência com o tamanho no tipo

**Projeto:** `Lapis.Lexer`, `Lapis.Parser`, `Lapis.Ast`, `Lapis.TypeChecker`, `Lapis.Runtime`, `prelude.ls`
**Milestone:** M12
**Spec:** [`lapislang-0.2.md` §18, §21](../spec/lapislang-0.2.md) — revisão
**Depende de:** 02 (modelo de tipos), 04 (parser), 06 (checker), 09 (prelude)

---

## Objetivo

A sequência primitiva passa a se chamar **span** e a carregar o tamanho no tipo:
`[Int;3]`. Com tamanho e índice conhecidos a indexação é total; nos demais casos
devolve `Option<T>`.

## Escopo

**Entra:** o tipo `[T;N]`/`[T;?]`, a construção `.[...]`, indexação checada,
`length`, a troca de `Result<T, IndexError>` por `Option<T>`, e a migração do
prelude e do corpus.

**Fica de fora:** `Array` e `List` como biblioteca (span é a base delas);
aritmética de tamanho (Q30); o sistema de constraints que tornaria `[T;?]`
indexável sem `Option`.

---

## O que será construído

### 24.1 Por que "span" e não "array"

**Decisão do autor.** `[T;N]` é a primitiva: uma sequência contígua de tamanho
conhecido pelo tipo. `Array` e `List` — crescimento, realocação, capacidade
separada de comprimento — virão como **biblioteca**, construídas sobre span.

Nomear a primitiva de "array" agora tomaria o nome que a estrutura de biblioteca
vai querer, e obrigaria a inventar um pior para ela depois. O custo é uma renomeação
mecânica hoje (`ArrayType` → `SpanType`, `ArrayValue` → `SpanValue`,
`ArrayExpression` → `SpanExpression`, e as mensagens de `LAP024x`).

### 24.2 O que **não** é adiável

A decisão inicial foi "`[T;N]` agora, `[T;?]` depois". Não se sustenta: o
`prelude.ls` tem quatro campos cujo tamanho ninguém sabe —
`TypeInfo.typeParameterNames`, `.fields`, `.variants` e
`VariantInfo.payloadTypeNames`. `reflect(T).fields` tem o tamanho que `T` tiver, e
não há `N` para escrever.

**Confirmado pelo autor: `[T;?]` entra imediatamente.** O que separa as duas
metades não é o tipo, é a indexação — e `[T;?]` indexando para `Option` é o que já
acontece hoje, com outro nome no envelope.

### 24.3 `[T;N]` é atribuível a `[T;?]` — **Q29 decidida**

```c
def imprime = fn(s: [Int;?]) Void { ... };
imprime(.[1, 2, 3]);        // `[Int;3]` num parâmetro `[Int;?]`
```

`?` **não** é um tamanho diferente: é a ausência da informação. Um `[Int;3]` é um
span de tamanho desconhecido do qual por acaso se sabe o tamanho, então a conversão
é esquecer o que se sabia — sempre segura, e numa direção só.

É a **segunda** regra de subtipagem da linguagem (hoje só `Never <: T`, Q13), e ela
não abre variância: o elemento continua invariante, `[Int;3]` não é `[Any;?]`.

### 24.4 Representação em runtime

Um span carrega **ponteiro, tamanho do elemento e quantidade** — é o que permite a
`[T;?]` responder `length` sem o tipo dizer.

No evaluator atual isso já é verdade sem trabalho nenhum: `ArrayValue` guarda
`Elements` e `ElementType`, e `Elements.Length` é a quantidade. A observação vale
para um backend futuro, e é a razão de span ser uma primitiva e não um `type` do
prelude — o layout é do compilador.

### 24.5 Indexação devolve `Option<T>`

**Muda em relação à 0.2.** Hoje `s[i]` é `Result<T, IndexError>` (spec §21); passa
a ser `Option<T>`.

```c
var s = .[1, 2, 3];
var e = s[1];              // Option<Int> — Some(2) ou None
```

`IndexError.OutOfBounds` nunca carregou informação: era um enum de uma variante
cujo significado é "falhou". `Result` existe para o erro que **diz alguma coisa**;
onde não há o que dizer, `Option` é o tipo honesto. E `Option` estava no prelude
desde o M2 **sem um único consumidor** — passa a ter o seu.

A tabela completa:

| Alvo | Índice | Tipo do resultado |
|---|---|---|
| `[T;N]` | constante, `0 <= i < N` | `T` — total, sem envelope |
| `[T;N]` | constante fora dos limites | `LAP0244`, erro de compilação |
| `[T;N]` | dinâmico | `Option<T>` |
| `[T;?]` | qualquer | `Option<T>` |

O tipo de `s[i]` depende de `i` ser constante. É deliberado, e o risco precisa de
diagnóstico: trocar `def i = 1;` por `var i = 1;` muda `s[i]` de `T` para
`Option<T>` e quebra o que vem depois. `LAP0210` diz isso quando o esperado é `T` e
o encontrado é `Option<T>`.

### 24.6 `length`

```c
def ds = .[1, 2, 3];
def n = ds.length;         // 3 — constante, o tipo é [Int;3]

var s = .[1, 2, 3];
var m = s.length;          // Int em runtime, lido da memória
```

Sobre `[T;N]`, `length` é a constante `N`, dobrada pelo **checker** — e utilizável
como argumento const genérico (Q18). Sobre `[T;?]`, é um `Int` comum.

Isso fecha um buraco de três milestones: `array_length` está previsto no plano 09
desde o M2 e nunca foi implementado; a falta dele forçou o caso de conformidade de
reflection (M10) a escrever `match info.variants[0] { ... }` só para perguntar "tem
alguma?", e impediu `@foreach` no M11.

### 24.7 `var` alarga o tamanho

```c
def ds = .[1, 2, 3];       // [Int;3]
var s  = .[1, 2, 3];       // [Int;?] — pode ser reatribuído a outro tamanho
var f: [Int;3] = .[1,2,3]; // [Int;3] — anotado, e `f = .[4,5]` passa a ser LAP0210
```

Um `var` sem anotação **alarga** para `?`, porque `s = .[1,2,3,4,5]` tem de
continuar válido (decisão do autor). Com anotação explícita o tamanho é mantido, e
a reatribuição passa a ser checada — é o que dá acesso a indexação total num
binding mutável.

### 24.8 Reflection

O tamanho é parte do tipo, então `reflect` o expõe: `typeName` de um campo
`[Int;3]` é `"[Int;3]"`. Sai de graça de `ToDisplayString`.

### 24.9 Migração

É a maior parte do plano.

| Onde | O quê |
|---|---|
| `prelude.ls` | quatro campos `T[]` → `[T;?]`; `IndexError` perde o consumidor |
| `PreludeScope` | `MakeOk`/`MakeIndexError`/`IndexResultArguments` → `MakeSome`/`MakeNone` |
| `Lapis.Ast` | `ArrayType`/`ArrayExpression` → `Span*`, com `ArraySize` |
| `Lapis.Runtime` | `ArrayValue` → `SpanValue` |
| `examples/` | `arrays.ls` (→ `spans.ls`), `generics.ls`, `reflection.ls`, `result.ls` |
| conformidade | 10 arquivos citam `IndexError` |
| `Residualizer` | `.[]` volta a ser residualizável — o tipo diz elemento e tamanho |

A última linha é ganho inesperado: hoje um array vazio não é residualizável porque
`[]` sozinho não diz o que carrega (M7).

**`IndexError` sai do prelude?** Ele fica sem nenhum consumidor. Manter um nome
exportado que nada usa é dívida; removê-lo é quebra de um nome publicado. A
recomendação é **remover** — a linguagem está sendo fechada agora, e é o momento em
que essa quebra custa menos.

### 24.10 Diagnósticos

```text
LAP0244  índice {0} fora dos limites de [{1};{2}]
LAP0245  o tamanho de um span deve ser um Int não negativo
```

`LAP0241` (span vazio requer anotação) continua: `.[]` diz o tamanho, não o
elemento.

---

## Decisões de design

### Por que `.[` e não outra desambiguação

`.User { }` já resolveu o mesmo problema do mesmo jeito (Q2). O ponto passa a
significar, uniformemente, "isto é um valor sendo construído" — e `[` fica livre
para ser sempre tipo.

### Por que o tamanho no tipo, e não análise no checker

A alternativa era provar `0 <= i < length` por análise de valores. Isso é
*bounds-check elimination*, resultado de partial evaluation (plano 14), e colocá-la
no checker o faria raciocinar sobre valores — contra a spec §47.

Com o tamanho no tipo, `s[3]` sobre `[Int;3]` é erro **de tipo**, com a mesma
maquinaria que rejeita `def a: Str = 1;`. E os dois não competem: o tipo cobre
índice literal, o PE continua provando `i < n` para um `i` derivado de laço. O
plano 14 fica com o caso interessante e perde o trivial.

---

## Q30 — aritmética de tamanho: **adiada, confirmado**

`s.concat(.[4,5])` sobre `[Int;3]` daria `[Int;5]`, exigindo somar tamanhos no
tipo — primeiro degrau de tipos dependentes.

Adiada com razão própria: **span é a base de `Array` e `List`**, e é nelas que
concatenação faz sentido com capacidade e crescimento. Resolver aritmética de
tamanho para a primitiva seria pagar por um caso que a biblioteca vai reformular.
Por enquanto `concat` devolve `[T;?]`.

---

## Testes necessários

### Tipo e construção

| Teste | Fonte | Esperado |
|---|---|---|
| `Span_FromLiteral` | `def a = .[1,2,3];` | `[Int;3]` |
| `Span_VarWidens` | `var a = .[1,2,3];` | `[Int;?]` |
| `Span_VarAnnotated_KeepsSize` | `var a: [Int;3] = ...` | `[Int;3]` |
| `Span_VarAnnotated_WrongSizeOnAssign` | `a = .[4,5]` | `LAP0210` |
| `Span_Nested` | `.[.[1],.[2]]` | `[[Int;1];2]` |
| `Span_Empty_NeedsAnnotation` | `def a = .[];` | `LAP0241` |
| `Type_WithoutDot_IsAType` | `def a: [Int;3]` | `[` é tipo |
| `Construction_NeedsDot` | `def a = [1,2,3];` | erro de sintaxe |

### Subtipagem (Q29)

| Teste | Fonte | Esperado |
|---|---|---|
| `Sized_IsAssignableToUnknown` | `[Int;3]` em `[Int;?]` | compila |
| `Unknown_IsNotAssignableToSized` | `[Int;?]` em `[Int;3]` | `LAP0210` |
| `Element_IsInvariant` | `[Int;3]` em `[Any;?]` | `LAP0210` |

### Indexação

| Teste | Fonte | Esperado |
|---|---|---|
| `Index_Known_IsTheElement` | `.[1,2,3][1]` | `Int`, valor 2 |
| `Index_OutOfBounds_IsCompileError` | `.[1,2,3][3]` | `LAP0244` |
| `Index_Dynamic_IsOption` | índice `var` | `Option<Int>` |
| `Index_OnUnknownSize_IsOption` | `[Int;?]` | `Option<Int>` |
| `Index_None_OutOfRange` | índice dinâmico fora | `Option.None` |
| `Index_NoLongerReturnsResult` | corpus | nenhum `IndexError` sobrevive |

### `length`

| Teste | Asserção |
|---|---|
| `Length_OnSized_IsAConstant` | `.[1,2,3].length` ⇒ 3, dobrado no checker |
| `Length_IsUsableAsConstGeneric` | `Somefn<a.length>()` compila |
| `Length_OnUnknown_IsRuntimeInt` | sobre `[T;?]` |

### Não-regressão

| Teste | Asserção |
|---|---|
| `Prelude_Compiles` | `TypeInfo` com `[T;?]` |
| `Reflection_ShowsTheSize` | `typeName` de um campo `[Int;3]` |
| `EmptySpan_IsResidualizable` | `.[]` sobrevive ao PE |
| corpus inteiro | migrado, **mesmas saídas** |

---

## Critérios de conclusão

- [ ] `[T;N]` e `[T;?]` no modelo de tipos, com `.[...]` construindo.
- [ ] `Int[]` removido — uma sintaxe só para tipo de span.
- [ ] `[T;N] <: [T;?]`, numa direção só, com elemento invariante.
- [ ] Índice literal fora dos limites virando `LAP0244` em compilação.
- [ ] Indexação devolvendo `Option<T>`; `IndexError` aposentado.
- [ ] `length` constante sobre `[T;N]`, runtime sobre `[T;?]`.
- [ ] Prelude, exemplos e corpus migrados, **com as mesmas saídas**.

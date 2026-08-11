# Plano 24 — Arrays com tamanho no tipo

**Projeto:** `Lapis.Lexer`, `Lapis.Parser`, `Lapis.Ast`, `Lapis.TypeChecker`, `prelude.ls`
**Milestone:** M12
**Spec:** [`lapislang-0.2.md` §18, §21](../spec/lapislang-0.2.md) — revisão
**Depende de:** 02 (modelo de tipos), 04 (parser), 06 (checker), 09 (prelude)

---

## Objetivo

O tamanho do array passa a viver **no tipo**: `[Int;3]`. Com tamanho e índice
conhecidos, a indexação é total e devolve o elemento; fora dos limites é erro de
compilação.

## Escopo

**Entra:** a sintaxe `[T;N]` e `[T;?]`, a construção `.[...]`, indexação checada
sobre `[T;N]`, `length` como constante, e a migração do prelude e do corpus.

**Fica de fora:** o sistema de constraints que tornaria `[T;?]` indexável sem
`Result` (`if i < arr.length`) — indexar `[T;?]` continua devolvendo `Result`,
que é o comportamento de hoje. Também fica de fora aritmética de tipos (`concat`
produzindo `[T;N+M]`) — ver Q30.

---

## O que será construído

### 24.1 O que **não** é adiável

A decisão foi "implementar `[Type;N]` agora e `[Type;?]` depois". Isso não se
sustenta como está, e é melhor descobrir aqui: o `prelude.ls` tem **quatro** campos
de array cujo tamanho ninguém sabe.

```c
def VariantInfo = type { ...; payloadTypeNames: Str[]; };
def TypeInfo = type { ...; typeParameterNames: Str[]; fields: FieldInfo[]; variants: VariantInfo[]; };
```

`reflect(T).fields` tem o tamanho que o tipo tiver — não há `N` para escrever. Sem
`[T;?]` o prelude não compila, e sem prelude não há linguagem.

O que separa as duas metades não é o tipo, é a **indexação**:

| | Tipo | Indexar com índice literal | Indexar com índice dinâmico |
|---|---|---|---|
| `[T;N]` | agora | elemento, fora dos limites é erro de compilação | `Result` |
| `[T;?]` | **agora** | `Result` | `Result` |

Indexar `[T;?]` devolvendo `Result` é exatamente o comportamento atual — nada
regride. O que fica para depois é o sistema de constraints que provaria
`i < arr.length` e tornaria o `Result` desnecessário também ali.

### 24.2 Sintaxe

```text
tipo         [Int;3]      [Int;?]      [[Int;3];2]
construção   .[1, 2, 3]   .[]
```

**`[` é tipo, `.[` é valor** — a mesma regra que `.User { }` já estabeleceu (Q2):
construção leva ponto inicial, e o parser distingue com um token só.

`Int[]` **sai**. Não há como manter as duas formas sem escolher qual delas
carrega o tamanho, e uma linguagem com dois jeitos de escrever o mesmo tipo é
exatamente o que este projeto evita.

### 24.3 Modelo de tipos

```csharp
public sealed record ArrayType(LapisType Element, ArraySize Size) : LapisType;

public abstract record ArraySize;
public sealed record FixedSize(int Value) : ArraySize;      // [Int;3]
public sealed record UnknownSize : ArraySize;               // [Int;?]
public sealed record ConstSize(string Parameter) : ArraySize;   // [Int;N] em fn<N: Int>
```

`ConstSize` é o encaixe com Q18: `fn<N: Int>(a: [Int;N])` é o que const generics
sempre quiseram ser, e o `FixedArray<Int, N>` dos exemplos vira a forma nativa.

### 24.4 Indexação

Em `CheckIndex`, com `[T;N]` e índice **constante conhecida** (o checker já tem
`ConstantOf`):

- `0 <= i < N` → o tipo é `T`. Sem `Result`, sem `match`.
- fora → `LAP0251`, erro de compilação com o span do índice.

Em qualquer outro caso — `[T;?]`, ou índice dinâmico — o tipo é
`Result<T, IndexError>`, como hoje.

Isso muda o tipo de uma expressão conforme o índice seja constante ou não. É
deliberado, e o risco vale registrar: trocar `def i = 1;` por `var i = 1;` muda o
tipo de `arr[i]` de `T` para `Result<T, IndexError>` e quebra tudo que vem depois.
O diagnóstico de `LAP0210` precisa dizer isso em voz alta quando o tipo esperado é
`T` e o encontrado é `Result<T, _>`.

### 24.5 `length`

Com o tamanho no tipo, `arr.length` sobre `[T;N]` é a **constante** `N` — dobrada
pelo checker, não pelo PE.

Isso fecha um buraco que apareceu três vezes: `array_length` está previsto no plano
09 desde o M2 e nunca foi implementado, e a falta dele forçou o caso de conformidade
de reflection (M10) a escrever `match info.variants[0] { Ok => ..., Err => ... }`
só para perguntar "tem alguma?", e impediu `@foreach` no M11.

Sobre `[T;?]`, `length` é `Int` comum.

### 24.6 Migração

Não é um detalhe do plano — é a maior parte dele.

| Onde | O quê |
|---|---|
| `prelude.ls` | quatro campos `T[]` → `[T;?]` |
| `PreludeScope.MakeTypeInfo` | os `ArrayValue` construídos passam a carregar tamanho |
| `examples/` | `arrays.ls`, `generics.ls`, `reflection.ls`, `result.ls` |
| conformidade | 9 casos usam `[]` |
| `Residualizer.CanResidualize` | array vazio deixa de ser problema: `.[]` tem tipo `[T;0]` |

A última linha é um ganho inesperado. Hoje um array vazio não é residualizável
porque `[]` sozinho não diz o que carrega (M7); com o tamanho e o elemento no tipo,
`.[]` volta a ser emitível.

### 24.7 Diagnósticos

```text
LAP0244  índice {0} fora dos limites de [{1};{2}]
LAP0245  o tamanho de um array deve ser um Int não negativo
LAP0104  esperado ';' no tipo de array          (parser)
```

Os três primeiros números livres da seção de arrays. `LAP0251` e `LAP0252`, que
seriam os óbvios, já são variante e construção.

`LAP0241` (array vazio requer anotação) **continua**: `.[]` não diz o tipo do
elemento, só o tamanho.

---

## Decisões de design

### Por que `.[` e não outra desambiguação

Porque `.User { }` já resolveu o mesmo problema do mesmo jeito, e uma segunda regra
para o mesmo caso seria uma regra a mais para lembrar. O ponto passa a significar,
uniformemente, "isto é um valor sendo construído".

### Por que o tamanho no tipo, e não análise no checker

A alternativa era o checker provar `0 <= i < length` por análise de valores. Isso é
*bounds-check elimination*, que é resultado de **partial evaluation** (plano 14), e
colocá-la no checker significaria fazer o checker raciocinar sobre valores — contra
a spec §47 ("sem unificação global, simples e previsível").

Com o tamanho no tipo, `arr[3]` sobre `[Int;3]` é erro **de tipo**, com a mesma
maquinaria que já rejeita `def a: Str = 1;`.

E os dois não competem: o tipo cobre índice literal; o PE continua sendo quem prova
`i < n` para um `i` derivado de laço. O plano 14 fica com o caso interessante e
perde o trivial.

---

## Questões que este plano abre

### Q29 — `[T;N]` é atribuível a `[T;?]`?

```c
def imprime = fn(a: [Int;?]) Void { ... };

imprime(.[1, 2, 3]);        // `[Int;3]` num parâmetro `[Int;?]`
```

Sem isso, nenhuma função aceita arrays de tamanhos diferentes e a feature vira uma
camisa de força. Com isso, a linguagem ganha a **segunda** regra de subtipagem —
hoje só existe `Never <: T` (Q13).

**Recomendação:** aceitar, numa direção só (`[T;N] <: [T;?]`, nunca o contrário).
É esquecer informação, que é sempre seguro.

### Q30 — aritmética de tamanho

`arr.concat(.[4,5])` sobre `[Int;3]` daria `[Int;5]` — o que exige somar tamanhos
**no tipo**. A linguagem não tem aritmética de tipos, e introduzi-la é um degrau
grande (é o começo de tipos dependentes).

**Recomendação:** `concat` devolve `[T;?]` por enquanto. Quem precisar do tamanho
exato reconstrói com `.[...]`.

---

## Testes necessários

### Tipo e construção

| Teste | Fonte | Esperado |
|---|---|---|
| `Sized_FromLiteral` | `def a = .[1,2,3];` | tipo `[Int;3]` |
| `Sized_Annotation` | `def a: [Int;3] = .[1,2,3];` | compila |
| `Sized_WrongSize` | `def a: [Int;2] = .[1,2,3];` | `LAP0210` |
| `Sized_Nested` | `.[.[1],.[2]]` | `[[Int;1];2]` |
| `Empty_NeedsAnnotation` | `def a = .[];` | `LAP0241` |
| `Type_WithoutDot_IsAType` | `def a: [Int;3] = ...` | `[` sem ponto é tipo |
| `Construction_NeedsDot` | `def a = [1,2,3];` | erro de sintaxe |

### Indexação

| Teste | Fonte | Esperado |
|---|---|---|
| `Index_Known_IsTheElement` | `.[1,2,3][1]` | `Int`, valor 2 |
| `Index_OutOfBounds_IsCompileError` | `.[1,2,3][3]` | `LAP0244` |
| `Index_Negative_IsCompileError` | `.[1,2,3][0-1]` | `LAP0244` |
| `Index_Dynamic_IsResult` | índice `var` | `Result<Int, IndexError>` |
| `Index_OnUnknownSize_IsResult` | `[Int;?]` | `Result`, como hoje |
| `Index_VarChangesTheType` | `def i` → `var i` | o diagnóstico explica |

### `length`

| Teste | Asserção |
|---|---|
| `Length_OnSized_IsAConstant` | `.[1,2,3].length` ⇒ 3, dobrado no checker |
| `Length_IsUsableAsConstGeneric` | `Somefn<a.length>()` compila |
| `Length_OnUnknown_IsInt` | sobre `[T;?]` |

### Não-regressão

| Teste | Asserção |
|---|---|
| `Prelude_Compiles` | `TypeInfo` com `[T;?]` |
| `Reflection_Unchanged` | `reflect(User).fields` continua indexável |
| `EmptyArray_IsResidualizable` | `.[]` agora sobrevive ao PE |
| corpus inteiro | migrado, mesmas saídas |

---

## Critérios de conclusão

- [ ] `[T;N]` e `[T;?]` no modelo de tipos, com `.[...]` construindo.
- [ ] `Int[]` removido — uma sintaxe só para tipo de array.
- [ ] Índice literal fora dos limites virando `LAP0244` em tempo de compilação.
- [ ] `length` constante sobre `[T;N]`.
- [ ] Prelude, exemplos e corpus migrados, **com as mesmas saídas**.
- [ ] **Q29 decidida** — sem ela nenhuma função aceita array.

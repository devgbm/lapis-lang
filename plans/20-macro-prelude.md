# Plano 20 — Macros de controle e a retirada de `If`/`Match`

**Projeto:** `Lapis.Runtime` (`prelude.ls`), `Lapis.Ast`, `Lapis.TypeChecker`
**Milestone:** M11
**Spec:** [`lapislang-macros-0.1.md` §12](../spec/lapislang-macros-0.1.md)
**Depende de:** 16 (goto/label), 17 (macro engine), 18 (constraint), 19 (reflection)

---

## Objetivo

Escrever `@if`, `@unless` e `@match` como macros do prelude, e **só então**, com
critério objetivo cumprido, retirar `If` e `Match` da Core.

## Escopo

**Entra:** `@if`, `@unless`, `@while` e `@match` no `prelude.ls`; a comparação por
variante; a exaustividade por `constraint`; a retirada condicional dos nós.

**Fica de fora:** `@foreach` — precisa de um protocolo de iteração sobre coleções
que a 0.2 não define.

---

## O que será construído

### 20.1 `@if` e `@unless` — nada de novo é preciso

```c
def unless = macro
    match Expression:condition Block:body
    expand {
        goto done if condition;
        body;
        label done;
    };
```

```c
def ifm = macro
    match Expression:condition Block:then else Block:otherwise
    expand {
        goto alt if !condition;
        then;
        goto done;
        label alt;
        otherwise;
        label done;
    }

    match Expression:condition Block:then
    expand {
        goto done if !condition;
        then;
        label done;
    };
```

`goto`, `label` e `!` (Q4) bastam. O teste `GotoForm_EquivalentToIf` do plano 16 é
o que autoriza esta fase a existir: se as duas formas não são observacionalmente
iguais, não há substituição a fazer.

### 20.2 `@while` — o que o salto para trás comprou

```c
macro while
    match Expression:condition Block:body
    expand {
        label top;
        goto done if !condition;
        body;
        goto top;
        label done;
    };
```

`@while` é a razão de o `goto` poder voltar (plano 16 §10.2), e é o primeiro
programa LapisLang capaz de não terminar. O evaluator aborta com `LAP0303`.

### 20.3 `@match` compara variantes; a carga é campo — Q20 + Q23

**Decisão do autor:** um braço nomeia só a variante, e a carga é lida como campo do
escrutinado.

```c
@match result {
    Result.Ok  { return result.value },
    Result.Err { return result.error }
}
```

Isso elimina `enumTag` e `enumPayload` — exatamente as primitivas que o M3 rejeitou
por ferirem o princípio §58.2 — e ainda **reaproveita a máquina de campos** que o M3
construiu para `type`: ler `result.value` é a mesma operação que ler `user.name`.

#### Carga nomeada

```csharp
// VariantInfo ganha nomes; hoje é ImmutableArray<LapisType> Payload
sealed record PayloadInfo(string? Name, LapisType Type, SourceSpan Span);
sealed record VariantInfo(string Name, ImmutableArray<PayloadInfo> Payload, SourceSpan Span);
```

```c
def Result = enum<T, E> { Ok(value: T), Err(error: E) };
```

O nome é **opcional** — `Ok(T)` continua válido e apenas não é legível por campo —,
então nenhum programa 0.2 quebra. O prelude passa a nomear as cargas de `Result` e
`Option`, porque é o que torna `@match` utilizável.

**Nomes de carga são únicos dentro do enum** (`LAP0523`). É o que faz o campo
determinar a variante sozinho, e com isso o **tipo** do acesso sai sem análise de
fluxo nenhuma — só a **segurança** precisa dela.

#### A comparação

Para variantes nulárias o `==` já funciona. Para variantes com carga, `Result.Ok` é
um construtor, e funções não são comparáveis (`LAP0281`). Regra nova, estreita:

```csharp
// TypeChecker.CheckEquality — caso novo
// `r == Result.Ok` onde Result.Ok : fn(T) Result<T, E>
if (right is FunctionType && IsVariantConstructor(node.Right))
{
    return PrimitiveType.Bool;   // compara só a variante
}
```

A conta final:

| Sai | Entra |
|---|---|
| `CoreMatch`, `CorePattern` e família | nomes de carga em `VariantInfo` |
| `TryMatch` do evaluator | uma regra de tipo para `valor == Enum.Variante` |
| `MatchCoverage` e a exaustividade do checker | a análise de dominância de §20.3b |
| `CoreIf` | |

### 20.3b Ler a carga com segurança

`result.value` tem tipo conhecido. Falta garantir que a variante seja mesmo `Ok`
ali — fora de um braço o acesso é indefensável:

```c
def r: Result<Int, IndexError> = xs[0];
print(r.value);          // e se for Err?  ⇒ LAP0524
```

**A regra:** o acesso a campo de carga só é aceito quando **dominado** por uma
comparação que fixa a variante. É uma consulta de dominância sobre o grafo que
`Labeled` já expõe: um join alcançado **apenas** por arestas guardadas por
`x == E.V` pode assumir a variante.

```text
goto arm0 if subject == Result.Ok;   ← aresta guardada
...
label arm0;                           ← join dominado pelo guarda
    return subject.value;             ← aceito
```

> **A análise não é custo extra deste plano.** É a mesma que o plano 14 constrói
> para *bounds-check elimination* — a pesquisa que motiva o projeto. Ela chega aqui
> um milestone antes e se paga duas vezes: elimina a checagem de limites **e** torna
> a leitura de carga segura sem `Result` aninhado.

**É o portão que decide a retirada de `Match`.** Enquanto a análise não existir,
`Match` fica na Core para os casos com carga.

### 20.4 Exaustividade por `constraint` e reflection

Q6 exige `match` exaustivo. Com `@match` sendo macro, quem impõe é a `constraint`:

```c
constraint {
    def enumName = enumNameOf(arms);

    if enumName == "" {
        if !hasWildcard(arms) {
            throw "match sobre valor não-enum exige um braço '_'";
        }
    } else {
        def faltando = missingVariants(reflect(enumName).variants, arms);

        if arrayLength(faltando) > 0 {
            throw "match não é exaustivo; faltam: " + join(faltando, ", ");
        }
    }
}
```

**O que faz isso funcionar é uma decisão já tomada.** Q3 exige variantes
qualificadas — `Color.Red`, nunca `Red`. Então os próprios braços nomeiam o enum, e
a `constraint` descobre qual é **sem precisar do tipo do escrutinado**, que em tempo
de expansão ainda não existe.

**Limite conhecido:** braços só de literais ou só `_` não nomeiam enum nenhum. Aí a
`constraint` exige `_` — a mesma regra que o checker aplica hoje a `Int`, `Float` e
`Str`.

### 20.5 A retirada, com critério de saída

`If` e `Match` **só saem** quando todos estes estiverem verdes:

- [ ] `GotoForm_EquivalentToIf` — equivalência observacional (plano 16);
- [ ] `@if` e `@unless` passando toda a suíte de `if` existente, sem alteração de expectativa;
- [ ] `@match` passando toda a suíte de `match` existente, idem;
- [ ] nenhum programa hoje aceito passa a exigir anotação;
- [ ] acesso a campo de carga tipando e a análise de dominância rejeitando o não guardado;
- [ ] exaustividade por `constraint` produzindo os mesmos `LAP0262` que o checker produz hoje, com os mesmos spans;
- [ ] a suíte de conformidade (plano 11) inteira verde com `if`/`match` reescritos como macros.

Enquanto **um** deles estiver vermelho, os nós ficam. Não é cautela: é o princípio
§58.1 — "se o PE e o evaluator discordam, o PE está errado" — aplicado à
substituição de uma primitiva por uma macro.

### 20.6 O que a linguagem ganha com a retirada

| Antes | Depois |
|---|---|
| `if`/`match` são palavras reservadas | são macros do prelude, sombreáveis |
| a análise de fluxo entende `If` e `Match` | entende `goto`/`label` e só |
| adicionar `@while` mexe no compilador | mexe no `prelude.ls` |
| a Core tem 19 nós | tem 16 |

O ganho real não é o tamanho: é que a **análise de fluxo do plano 14** passa a ter
uma forma só de controle para entender. Bounds-check elimination sobre um CFG de
`goto` é mais simples do que sobre `If` aninhado com `Match` dentro.

---

## Decisões de design

### Por que `@if` e não `if`

Se `if` continuasse sem `@`, seria uma palavra reservada com tratamento especial no
parser — e o objetivo é o contrário. Com `@if`, `if` deixa de ser keyword e vira um
nome do prelude, sombreável como `Result`.

O custo é sintático: todo programa passa a escrever `@if`. É um custo real e vale
dizê-lo em voz alta — **esta é a parte da proposta com maior impacto na ergonomia**,
e a que mais merece a confirmação do autor antes do M14 começar (Q22).

### Por que `@foreach` não entra

`@while` entra porque `goto` para trás basta. `@foreach` precisa de mais: um
protocolo de iteração sobre coleções (`length` + índice, ou um iterador), que a 0.2
não define. É trabalho de biblioteca, não de macro, e vem depois.

---

## Testes necessários

### Equivalência

| Teste | Asserção |
|---|---|
| `MacroIf_MatchesCoreIf` | property: `@if` e `if` produzem a mesma saída |
| `MacroMatch_MatchesCoreMatch` | property: idem para `match` |
| `MacroIf_PassesLegacyIfSuite` | a suíte de `if` inteira, com `@if` |
| `MacroMatch_PassesLegacyMatchSuite` | idem |

Os quatro são **portões**, não testes comuns: a retirada de §20.5 depende deles.

### Exaustividade por constraint

| Teste | Fonte | Esperado |
|---|---|---|
| `MacroMatch_Exhaustive` | todas as variantes | expande |
| `MacroMatch_MissingVariant` | falta uma | erro, listando quais |
| `MacroMatch_Wildcard` | com `_` | expande |
| `MacroMatch_NonEnum_NeedsWildcard` | `Int` sem `_` | erro |
| `MacroMatch_Bool_TwoLiterals` | `true`/`false` | expande, como hoje |
| `MacroMatch_SameSpansAsChecker` | span idêntico ao `LAP0262` atual | igual |

### Carga como campo

| Teste | Fonte | Esperado |
|---|---|---|
| `Payload_NamedInDeclaration` | `enum { Ok(value: T) }` | `VariantInfo` com nome |
| `Payload_UnnamedStillParses` | `enum { Ok(T) }` | válido, sem acesso por campo |
| `Payload_DuplicateName` | `enum { Ok(v: T), Err(v: E) }` | `LAP0523` |
| `Payload_FieldType` | `result.value` num braço `Result.Ok` | tipo da carga, não `Any` |
| `Payload_UnguardedAccess` | `r.value` fora de braço | `LAP0524` |
| `Payload_GuardedByGoto` | acesso após `goto L if r == Result.Ok` | aceito |
| `Payload_GuardedByWrongVariant` | guarda `Err`, acessa `.value` | `LAP0524` |
| `Payload_PartiallyGuarded` | join com uma aresta não guardada | `LAP0524` |

Os três últimos são a análise de dominância, e são o portão de §20.5.

### Comparação por variante

| Teste | Fonte | Esperado |
|---|---|---|
| `VariantEquality_Nullary` | `c == Color.Red` | `Bool` — já funciona hoje |
| `VariantEquality_WithPayload` | `r == Result.Ok` | `Bool`, compara só a variante |
| `VariantEquality_IgnoresPayload` | `Result.Ok(1) == Result.Ok` | `true` |
| `VariantEquality_DifferentVariant` | `Result.Err(e) == Result.Ok` | `false` |
| `VariantEquality_WrongEnum` | `c == Result.Ok` | `LAP0280` |
| `FunctionEquality_StillRejected` | `f == g` com funções comuns | `LAP0281` |

O último garante que a regra nova é **estreita**: só construtor de variante, não
função qualquer.

### `@while`

| Teste | Asserção |
|---|---|
| `MacroWhile_Iterates` | contador chega ao valor esperado |
| `MacroWhile_ZeroIterations` | condição falsa de saída ⇒ corpo não executa |
| `MacroWhile_Infinite_Aborts` | `LAP0303` |

### Regressão

| Teste | Asserção |
|---|---|
| `MacroMatch_NoAnnotationRegression` | nenhum programa aceito hoje passa a exigir anotação |
| `ResultExample_StillWorks` | `examples/result.ls` reescrito com `@match` produz a mesma saída |

Os dois são portões de §20.5: se falharem, a retirada não acontece.

---

## Critérios de conclusão

- [ ] `@if`, `@unless` e `@match` no `prelude.ls`, escritos em LapisLang.
- [ ] Os quatro testes-portão de equivalência verdes.
- [ ] `enumPayload` tipando por variante, sem regressão de anotação.
- [ ] Exaustividade por `constraint` com os mesmos códigos e spans de hoje.
- [ ] Só então: `CoreIf`, `CoreMatch` e a família `CorePattern` removidos, com
      `if`/`match` deixando de ser palavras reservadas.

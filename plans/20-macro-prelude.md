# Plano 20 — Macros de controle e a retirada de `If`/`Match`

**Projeto:** `Lapis.Runtime` (`prelude.ls`), `Lapis.Ast`, `Lapis.TypeChecker`
**Milestone:** M14
**Spec:** [`lapislang-macros-0.1.md` §12](../spec/lapislang-macros-0.1.md)
**Depende de:** 16 (goto/label), 17 (macro engine), 18 (constraint), 19 (reflection)

---

## Objetivo

Escrever `@if`, `@unless` e `@match` como macros do prelude, e **só então**, com
critério objetivo cumprido, retirar `If` e `Match` da Core.

## Escopo

**Entra:** as macros de controle no `prelude.ls`; as primitivas que `@match` exige;
a checagem de exaustividade por `constraint`; a retirada condicional dos nós.

**Fica de fora:** `@foreach` e `@while` — precisam de salto para trás, que precisa
de recursão (Q8, 0.3).

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

### 20.2 `@match` — a conta que precisa ser feita

`@match` precisa de duas coisas que `goto`/`label` não dão: **saber qual variante um
valor é** e **extrair a carga**. Duas primitivas novas:

```text
enumTag(valor) Int
enumPayload(valor, índice) <tipo da carga>
```

O comentário que hoje está em `CoreNodes.cs` explica por que `Match` continuou
primitivo:

> desugará-lo exigiria primitivas `enum_tag` e `enum_payload`, aumentando o
> runtime — contra a spec §58 ("runtime mínimo")

A conta, agora que temos os dois lados:

| Sai | Entra |
|---|---|
| `CoreMatch`, `CorePattern` e família | `enumTag`, `enumPayload` |
| a máquina de padrões do evaluator (`TryMatch`) | |
| `MatchCoverage` e a exaustividade do checker | uma `constraint` em `.ls` |
| `CoreIf` | |

Cinco estruturas de C# saem, duas nativas entram. A conta favorece as macros.

### 20.3 O obstáculo que decide o cronograma

> **O tipo de `enumPayload` depende da variante.**

Em `Result.Ok(v) => ...`, `v` é `T`. Em `Result.Err(e) => ...`, `e` é `E`. Uma
assinatura `fn(Any, Int) Any` perderia isso, e um `@match` expandido seria **menos
tipado** que o `Match` de hoje — o oposto do objetivo.

Saídas possíveis, em ordem de preferência:

1. **`enumPayload` como intrínseco do checker**, com o tipo derivado da variante
   testada no caminho de controle que chega até ele. Requer que o checker saiba, no
   ponto do `enumPayload`, que `enumTag(x) == k` já foi verificado — ou seja, uma
   análise de fluxo com refinamento por condição. É a solução certa e é trabalho de
   verdade: o checker de hoje é uma travessia única dirigida por sintaxe (§47),
   sem fluxo.

2. **Uma primitiva por variante**, gerada pelo `expand` a partir de reflection:
   `payloadOf_Result_Ok(x) T`. Preserva o tipo sem análise de fluxo, ao custo de
   uma nativa sintética por variante — o que contraria "runtime mínimo" de um jeito
   diferente e pior.

3. **Aceitar a perda de tipo** dentro do `expand` de `@match`, com uma asserção de
   tipo interna. Rejeitada: é exatamente o que a spec §41 proíbe — macro não
   substitui type checker.

**A saída 1 é a que este plano persegue**, e é por isso que o M14 é o último da
sequência: ele depende de o checker ganhar noção de fluxo, o que o CFG do plano 16
torna possível mas não automático.

### 20.4 Exaustividade por `constraint` e reflection

Q6 exige `match` exaustivo. Com `@match` sendo macro, quem impõe é a `constraint`:

```c
def matchm = macro
    match Expression:scrutinee { MatchArm:arms* separado por , }

    constraint {
        def enumName = enumNameOf(arms);

        if enumName == "" {
            if !hasWildcard(arms) {
                throw "match sobre valor não-enum exige um braço '_'";
            }
        } else {
            def info = reflect(enumName);
            def faltando = missingVariants(info.variants, arms);

            if arrayLength(faltando) > 0 {
                throw "match não é exaustivo; faltam: " + join(faltando, ", ");
            }
        }
    }

    expand { ... };
```

**O que faz isso funcionar é uma decisão já tomada.** Q3 exige variantes
qualificadas — `Color.Red`, nunca `Red`. Então os próprios braços nomeiam o enum, e
a `constraint` descobre qual é **sem precisar do tipo do escrutinado** — que, em
tempo de expansão, ainda não existe.

Sem Q3 isto seria impossível: a exaustividade dependeria do type checker, que roda
depois da expansão. É um caso em que uma decisão tomada por ergonomia acabou
pagando uma dívida arquitetural que ninguém tinha visto.

**Limite conhecido:** braços só de literais ou só `_` não nomeiam enum nenhum. Aí a
`constraint` exige `_`, que é a mesma regra que o checker aplica hoje a `Int`,
`Float` e `Str`.

### 20.5 A retirada, com critério de saída

`If` e `Match` **só saem** quando todos estes estiverem verdes:

- [ ] `GotoForm_EquivalentToIf` — equivalência observacional (plano 16);
- [ ] `@if` e `@unless` passando toda a suíte de `if` existente, sem alteração de expectativa;
- [ ] `@match` passando toda a suíte de `match` existente, idem;
- [ ] `enumPayload` tipando **tão bem quanto** `Match` — nenhum programa hoje aceito passa a exigir anotação;
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

Precisa de salto para trás, que precisa de recursão, que a 0.2 não tem (Q8). Entra
junto com laços, na 0.3. Colocá-lo aqui exigiria relaxar a regra de terminação do
plano 16 sem ter o que a substitua.

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

### Tipagem da carga

| Teste | Asserção |
|---|---|
| `EnumPayload_HasVariantType` | `Result.Ok(v)` ⇒ `v: T`, não `Any` |
| `EnumPayload_WrongVariant_IsError` | extrair carga da variante errada é erro |
| `MacroMatch_NoAnnotationRegression` | nenhum programa aceito hoje passa a exigir anotação |

O último é o portão de §20.5 que mais importa: se ele falhar, a retirada não
acontece.

---

## Critérios de conclusão

- [ ] `@if`, `@unless` e `@match` no `prelude.ls`, escritos em LapisLang.
- [ ] Os quatro testes-portão de equivalência verdes.
- [ ] `enumPayload` tipando por variante, sem regressão de anotação.
- [ ] Exaustividade por `constraint` com os mesmos códigos e spans de hoje.
- [ ] Só então: `CoreIf`, `CoreMatch` e a família `CorePattern` removidos, com
      `if`/`match` deixando de ser palavras reservadas.

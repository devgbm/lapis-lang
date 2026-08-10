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

**Fica de fora:** `@foreach` — precisa de iteração sobre coleção, que precisa de um
protocolo de iteração que a 0.2 não tem; e a ligação de carga em `@match`, que
aguarda Q23.

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

### 20.3 `@match` compara variantes — Q20

**Decisão do autor:** `@match` **não desestrutura carga**. Ele compara a variante, e
só. Isso elimina inteiramente `enumTag` e `enumPayload` — exatamente as primitivas
que o M3 havia rejeitado por ferirem o princípio §58.2:

> desugará-lo exigiria primitivas `enum_tag` e `enum_payload`, aumentando o
> runtime — contra a spec §58 ("runtime mínimo")

O comentário em `CoreNodes.cs` continua correto. A saída não foi pagar o preço: foi
não precisar dele. Basta que enums sejam **comparáveis**, e eles já são desde o M3.

```c
macro match
    match Expression:scrutinee { MatchArm:arms* separado por , }
    constraint { /* exaustividade — §20.4 */ }
    expand {
        def subject = scrutinee;

        goto arm0 if subject == Color.Red;
        goto arm1 if subject == Color.Green;
        goto done;

        label arm0;  /* corpo 0 */  goto done;
        label arm1;  /* corpo 1 */  goto done;
        label done;
    };
```

A conta final, agora sem contrapartida:

| Sai | Entra |
|---|---|
| `CoreMatch`, `CorePattern` e família | uma regra de tipo para `valor == Enum.Variante` |
| `TryMatch` do evaluator | |
| `MatchCoverage` e a exaustividade do checker | |
| `CoreIf` | |

**A única adição:** comparar um valor de enum com um **construtor de variante** não
aplicado compara apenas a variante, ignorando a carga.

```csharp
// TypeChecker.CheckEquality — caso novo
// `r == Result.Ok` onde Result.Ok : fn(T) Result<T, E>
if (right is FunctionType && IsVariantConstructor(node.Right))
{
    return PrimitiveType.Bool;   // compara tag
}
```

Para variantes nulárias (`Color.Red`) nada muda: `==` já funciona hoje. Para
variantes com carga, `Result.Ok` é um construtor, e funções não são comparáveis
(`LAP0281`) — daí a regra. **Uma regra de tipo e uma linha no evaluator**, contra
duas nativas.

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

Sem Q3 isto seria impossível: a exaustividade dependeria do type checker, que roda
depois da expansão.

**Limite conhecido:** braços só de literais ou só `_` não nomeiam enum nenhum. Aí a
`constraint` exige `_` — a mesma regra que o checker aplica hoje a `Int`, `Float` e
`Str`.

### 20.4b ⚠️ Ligação de carga — Q23, em aberto

Q20 tem uma consequência que precisa estar escrita, e não subentendida.

O `match` de hoje **liga a carga**:

```c
match result {
    Result.Ok(value) => return value,        // `value` é o conteúdo
    Result.Err(error) => return fallback
}
```

Comparando só a variante, `value` não tem de onde sair. **`examples/result.ls` — o
exemplo canônico de tratamento de erro da linguagem — não é expressável** com o
`@match` de §20.3.

Isso não invalida Q20: não ter `enumTag`/`enumPayload` continua valendo. O que falta
é decidir **como** a carga é lida:

| Caminho | Custo | Observação |
|---|---|---|
| Acesso a campo na variante (`r.value`) | uma regra no checker | carga vira campo nomeado; combina com `type` |
| Acessor gerado por macro, por enum | zero primitivas | verboso, mas só usa o que já existe |
| Manter `Match` da Core só para desestruturação | zero trabalho | contradiz Q22 pela metade |

**Enquanto Q23 não for decidida, `Match` permanece na Core para os casos com carga.**
É o critério de saída que o autor já estabeleceu — os nós só saem quando o
substituto estiver funcional — aplicado a uma parte específica.

### 20.5 A retirada, com critério de saída

`If` e `Match` **só saem** quando todos estes estiverem verdes:

- [ ] `GotoForm_EquivalentToIf` — equivalência observacional (plano 16);
- [ ] `@if` e `@unless` passando toda a suíte de `if` existente, sem alteração de expectativa;
- [ ] `@match` passando toda a suíte de `match` existente, idem;
- [ ] nenhum programa hoje aceito passa a exigir anotação ou reescrita;
- [ ] Q23 decidida e a ligação de carga funcionando;
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
| `PayloadBinding_StillWorks` | `examples/result.ls` continua rodando (via Q23) |

Os dois são portões de §20.5: se falharem, a retirada não acontece.

---

## Critérios de conclusão

- [ ] `@if`, `@unless` e `@match` no `prelude.ls`, escritos em LapisLang.
- [ ] Os quatro testes-portão de equivalência verdes.
- [ ] `enumPayload` tipando por variante, sem regressão de anotação.
- [ ] Exaustividade por `constraint` com os mesmos códigos e spans de hoje.
- [ ] Só então: `CoreIf`, `CoreMatch` e a família `CorePattern` removidos, com
      `if`/`match` deixando de ser palavras reservadas.

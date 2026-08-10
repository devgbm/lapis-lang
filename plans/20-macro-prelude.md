# Plano 20 — Macros de controle no prelude

**Projeto:** `Lapis.Runtime` (`prelude.ls`)
**Milestone:** M11
**Spec:** [`lapislang-macros-0.1.md` §12](../spec/lapislang-macros-0.1.md)
**Depende de:** 16 (goto/label), 17 (macro engine), 18 (constraint), 19 (reflection)

---

## Objetivo

Escrever no `prelude.ls`, na própria linguagem, as construções de controle que a
LapisLang **não tem** — e demonstrar com isso que a tese do sistema de macros se
sustenta: um laço deixa de ser trabalho de compilador e vira seis linhas de
biblioteca.

## Escopo

**Entra:** `@unless` e `@while` no `prelude.ls`.

**Fica de fora:**

- **`@if` e `@match`** — `if` e `match` continuam no compilador. §20.3 explica, e é
  a parte mais importante deste plano;
- retirada de `CoreIf`, `CoreMatch` ou da família `CorePattern`: nada sai;
- `@foreach` — precisa de um protocolo de iteração sobre coleções que a 0.2 não
  define.

> **Este plano não retira nada da Core.** A versão anterior previa substituir `If` e
> `Match` por macros; a análise de §20.3 mostrou que `@match` não se sustenta sem
> uma construção de linguagem que ainda não existe, e o escopo foi corrigido antes
> de virar código.

---

## O que será construído

### 20.1 `@unless`

```c
macro unless
    match Expression:condition Block:body
    expand {
        goto done if condition;
        body;
        label done;
    };
```

### 20.2 `@while`

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

`@while` é a razão de `goto` poder voltar (plano 16), e é o primeiro programa
LapisLang capaz de não terminar — o evaluator aborta com `LAP0303`.

É também a demonstração mais forte da tese: **um laço, que em qualquer outra
linguagem é trabalho de compilador, aqui é seis linhas de `prelude.ls`.**

> ⚠️ **Bloqueado desde o M6.** A expansão acima está sintaticamente correta e vai
> executar — mas o laço **não avança**. Bindings são imutáveis e o corpo de um join
> roda no ambiente do grupo, o mesmo em toda volta: `condition` vale o mesmo em
> todas as iterações, então `@while` ou não roda, ou roda até o orçamento de saltos
> acabar. Nunca itera "o número certo de vezes".
>
> Não é defeito de `goto`: é o resto da linguagem. Antes deste plano é preciso
> escolher um caminho — **join com parâmetros** (`label L(x: Int)` / `goto L(x+1)`,
> a forma da literatura, que mantém tudo imutável), **mutação** (`var`), ou **laço
> por recursão** (derruba a Q8). A comparação está na spec de macros §10.6 e no
> plano 16.
>
> `@unless` (§20.1) não é afetado: ele só salta para frente.

### 20.3 Por que `@if` e `@match` ficaram de fora

A parte deste plano que vale ser lida com atenção, porque é uma decisão revertida
depois de análise — e revertida no lugar certo, antes da implementação.

`@if` sozinho é trivial: `goto`, `label` e `!` bastam. O problema é `@match`, e é um
só: **ler a carga de uma variante com segurança.** Três saídas foram examinadas, e
as três caíram:

| Saída | Por que caiu |
|---|---|
| `enumTag`/`enumPayload` como nativas | o tipo da carga depende da variante; `fn(Any, Int) Any` tornaria `@match` **menos** tipado que o `Match` de hoje — o oposto do objetivo |
| Carga como campo (`result.value`) | o tipo sai fácil (o nome do campo determina a variante), mas nada garante que a variante seja a certa: `r.value` sobre um `Err` é indefensável, e a spec §30 proíbe exceção de runtime |
| Campo + análise de dominância | funciona, mas faz a segurança de uma construção básica depender de análise de fluxo. Muito peso para o que se ganha |

O padrão comum é o mesmo nas três: **extrair carga com segurança é problema de
linguagem, não de macro.** Uma macro transforma sintaxe; ela não tem como
estabelecer que um valor é da variante `Ok` no ponto do acesso.

**E `@if` sem `@match` seria pior que nenhum dos dois:** metade do controle de fluxo
viraria prelude e metade continuaria no compilador, e a análise de fluxo do plano 14
teria **duas** formas a entender em vez de uma. O ganho que a versão anterior deste
plano declarava — "uma forma só de controle" — some se a substituição for parcial.

### 20.4 O caminho de volta

A retirada volta à mesa quando a linguagem tiver uma **construção própria para ler a
carga de uma variante com segurança** — uma palavra reservada, não uma macro e não
um acesso a campo.

O requisito, sem projetar a solução:

- entregar o **tipo** da carga, derivado da variante nomeada no ponto do acesso;
- entregar a **garantia** de que a variante é aquela, sem depender de análise de
  fluxo;
- ter caminho definido quando não é — sem exceção de runtime (§30, Q9).

Com ela no lugar, a conta de §58.2 fecha de verdade: saem `CoreMatch`,
`CorePattern` e família, o `TryMatch` do evaluator e a máquina de exaustividade;
entra uma construção só.

**Registrado como Q23**, sem forma definida — especificá-la antes de precisar dela
seria projetar no escuro.

---

## Decisões de design

### Por que `@while` entra e `@foreach` não

`@while` precisa só de `goto` para trás, que o M6 entrega — mas veja o aviso em
§20.2: o M6 entregou o salto, e não o progresso. `@foreach` precisa de um
protocolo de iteração sobre coleções — `length` mais índice, ou um iterador — que a
0.2 não define. É trabalho de biblioteca, não de macro.

### Por que macros que só acrescentam

Toda macro deste plano **adiciona** uma construção que a linguagem não tinha.
Nenhuma substitui algo que o compilador já faz. É a divisão que sobrou depois de
§20.3, e ela tem uma virtude que a original não tinha: **não há como regredir**. Um
`@while` quebrado não afeta nenhum programa existente.

---

## Testes necessários

### `@unless`

| Teste | Fonte | Esperado |
|---|---|---|
| `Unless_False_RunsBody` | condição falsa | corpo executa |
| `Unless_True_SkipsBody` | condição verdadeira | corpo não executa |
| `Unless_EquivalentToNegatedIf` | property: `@unless c { b }` ≡ `if !c { b }` | mesma saída |

### `@while`

| Teste | Fonte | Esperado |
|---|---|---|
| `While_Iterates` | contador até 10 | chega a 10 |
| `While_ZeroIterations` | condição falsa de saída | corpo não executa |
| `While_Infinite_Aborts` | `@while true { }` | `LAP0303` |
| `While_DoesNotGrowStack` | 100.000 iterações | sem stack overflow |
| `While_BodySeesOuterBindings` | `def` antes do laço | visível no corpo |

### Não-regressão

| Teste | Asserção |
|---|---|
| `CoreIf_StillExists` | `if` continua palavra reservada e nó da Core |
| `CoreMatch_StillExists` | `match` idem, com desestruturação de carga |
| `ResultExample_Unchanged` | `examples/result.ls` roda sem alteração |
| `PreludeMacros_DoNotShadowKeywords` | `macro while` não colide com nada existente |

A suíte inteira anterior é o teste que mais importa aqui: **este plano não pode
mudar o comportamento de nenhum programa que já funcionava.**

---

## Critérios de conclusão

- [ ] `@unless` e `@while` no `prelude.ls`, escritos em LapisLang.
- [ ] `@while` iterando sem crescer a pilha de C#, e abortando com `LAP0303` quando
      não termina.
- [ ] `if`, `match` e a desestruturação de carga **intactos**.
- [ ] Zero alteração de expectativa em qualquer teste anterior.

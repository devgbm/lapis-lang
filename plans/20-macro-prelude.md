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

**Desbloqueado pela Q25.** Até o `var` existir, esta expansão rodava mas nunca
iterava: nada mudava entre as voltas, então `condition` valia o mesmo sempre. Com
mutação, o corpo do laço pode avançar o estado que a condição lê.

Uma restrição de escrita fica: um `var` declarado **depois** de um `label` vive
dentro daquele join e não é visível no seguinte, então a macro precisa expandir
para uma forma em que as declarações do usuário fiquem **antes** do primeiro
rótulo gerado. É a mesma regra de escopo do plano 16 §16.4, e é o que os testes
de `@while` precisam cobrir.

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

`@while` precisa de `goto` para trás (M6) e de mutação (Q25) — os dois existem. `@foreach` precisa de um
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
| `While_CountsUp` | `var i = 0; @while i < 3 { i = i + 1; }` | itera 3 vezes |
| `While_DeclarationsBeforeLabel` | `var` do usuário fica em escopo em todas as voltas | |
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

- [x] `@unless` e `@while` no `prelude.ls`, escritos em LapisLang.
- [x] `@while` iterando sem crescer a pilha de C#, e abortando com `LAP0303` quando
      não termina.
- [x] `if`, `match` e a desestruturação de carga **intactos**.
- [x] Zero alteração de expectativa em qualquer teste anterior.

---

## O que a implementação mudou no plano

### 1. Macros do prelude precisaram de uma camada no registro

O plano trata o `prelude.ls` como se macros já viajassem com ele. Não viajavam: o
`MacroRegistry` era por arquivo, e o `PreludeLoader` nem roda expansão — uma
`macro` no prelude chegaria ao desugar.

Duas peças pequenas resolveram, e as duas espelham o que já existia para valores:

- `PreludeLoader` separa as `MacroDeclaration` antes do desugar, e `PreludeScope`
  as guarda ao lado dos bindings. Não são `PreludeBinding` porque uma macro não
  tem tipo nem valor (Q19).
- `MacroRegistry` ganhou uma camada de base. Uma macro do arquivo com o mesmo
  nome **sombreia** a do prelude, sem diagnóstico — a mesma regra de
  `def Result = ...`; duas do próprio arquivo continuam sendo `LAP0511`.

### 2. A expansão funcionou sem nenhuma mudança no engine

`@unless` e `@while` são exatamente o que o plano escreveu, e passaram de
primeira: higiene renomeia `top` e `done`, a captura de bloco é *spliced* em vez
de virar escopo aninhado, e a invocação gulosa consome `i < 3 { ... }` inteiro.
O M8 e o M6 já tinham resolvido o que era preciso.

### 3. A restrição de escrita é maior do que o §20.2 previu

O plano diz que "a macro precisa expandir para uma forma em que as declarações do
usuário fiquem antes do primeiro rótulo gerado", e trata disso como um cuidado ao
escrever a macro. O que a implementação mostrou é uma regra **do usuário**, e mais
larga:

> **Todo `var` que um laço usa se declara antes do primeiro `@while` do bloco.**

```c
var i = 0;
@while i < 2 { i = i + 1; }
var k = 0;                      // preso no join que `@while` deixou aberto
@while k < 3 { k = k + 1; }     // LAP0201: 'k' não existe
```

A causa é do M6 e está correta: joins são **irmãos**, não aninhados, e um não
enxerga os bindings do outro porque um salto pode ter pulado a declaração. O que o
M11 muda é que os rótulos passaram a ser **invisíveis** — quem escreve `@while` não
tem por que saber que um `label` foi introduzido.

Não é bug e não tem correção barata: a saída é *join com parâmetros*
(`label L(x: Int);` / `goto L(x + 1);`), registrada como evolução possível desde o
M6. Até lá a regra está em letras grandes no `examples/control.ls` e travada por
teste, para que o dia em que deixar de valer seja visível.

Um `@unless` **aninhado no corpo** de um `@while` não sofre disso: o corpo é um
join só, e a macro interna não abre um irmão dele.

---

## O que ficou de fora

**`@if` e `@match`** — pelo motivo do §20.3, que a implementação não teve razão
nenhuma para revisitar. `match` continua no compilador **com** desestruturação de
carga, que é justamente o que nenhuma macro conseguiria fazer com segurança.

**`@foreach`** — continua precisando de um protocolo de iteração sobre coleções.
E de algo mais básico que o plano não menciona: **não há como perguntar o tamanho
de um array**. `array_length` está previsto no plano 09 mas nunca foi
implementado, e sem ele nem a forma `índice < tamanho` se escreve.

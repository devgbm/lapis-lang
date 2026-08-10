# Apêndice C — Decisões de Design e Questões Abertas

Registro das escolhas que a spec 0.2 não determina, ou determina de forma
ambígua. Cada entrada tem: o problema, a decisão provisória, a justificativa e
o que precisa de confirmação.

**As entradas marcadas 🔴 mudam a linguagem e devem ser confirmadas pelo autor da
spec.** As marcadas 🟡 são internas à implementação.

---

## Q1 🔴 — Declaração vs. uso de generics

**Problema.** A spec §13 mostra:

```c
def SomeType = type<"value", 1, true, Int, fn() Int { return 1; }> {
    label: Str;
};
```

Isso é a forma de **uso** aparecendo na posição de **declaração**. Uma declaração
precisa de nomes para os parâmetros — não há como o corpo de `SomeType` se
referir a `"value"`. §14 tem o mesmo problema com `type<T, 3>`.

**Decisão.** Separar as duas formas:

```c
def FixedArray = type<T, N: Int> { values: T[]; };   // declaração: nomes + tipos
def a: FixedArray<Int, 3> = ...;                     // uso: valores
```

Parâmetro genérico é ou `IDENT` (parâmetro de tipo) ou `IDENT ":" type`
(parâmetro const). Argumento genérico é um tipo ou um valor constante.

**Justificativa.** É a única leitura que torna const generics utilizáveis — o
corpo precisa de um nome para o valor. Preserva integralmente a forma de *uso*
que a spec exemplifica, que é o que §13 realmente quer demonstrar.

**Precisa de confirmação:** sim.

---

## Q2 🔴 — Sintaxe de construção de `type`

**Problema.** A spec §14 define `type { id: Int; name: Str; }` e §24 lista
`Construct` na Core AST, mas nenhuma seção mostra como criar uma instância nem
como ler um campo.

**Decisão.**

```c
def u = User { id: 1, name: "Gabriel" };
def n = u.name;
```

Com a restrição de "sem literal de struct" na condição de `if` e no escrutinado
de `match` (Apêndice A §A.8).

**Alternativa considerada:** construção em forma de chamada, `User(id: 1)`.
Rejeitada porque exigiria argumentos nomeados, que não existem na linguagem, e
porque a forma de chaves espelha a declaração.

**Precisa de confirmação:** sim — inclusive se `type` deve mesmo ser construível
na 0.2, ou se fica só declarável até a 0.3.

---

## Q3 🔴 — Variantes de enum: nuas ou qualificadas

**Problema.** A spec usa as duas formas: `Ok(10)` e `Err(error)` (§15, §16) nuas,
e `IndexError.OutOfBounds` (§21) qualificada.

**Decisão.** Ambas são válidas:

- `Enum.Variante` é sempre válido (acesso a membro sobre um `MetaType`);
- `def X = enum { ... };` **também** injeta os nomes das variantes no escopo em
  que o `def` aparece;
- colisão com um nome existente ⇒ `LAP0203`.

**Justificativa.** Faz os exemplos da spec funcionarem literalmente. O custo é
poluição de escopo, mitigada pelo diagnóstico de colisão.

**Risco conhecido.** Dois enums com variantes homônimas no mesmo escopo tornam a
forma nua inutilizável para ambos. Aceitável em v0.2; se incomodar, a injeção
pode virar opt-in.

**Precisa de confirmação:** sim.

---

## Q4 🔴 — Operadores `!`, `&&`, `||`

**Problema.** A lista de tokens da spec §44 não inclui negação lógica nem
conjunção/disjunção. Sem elas, `Bool` só serve como condição de `if` e não há
como escrever `if !encontrado` ou `if 0 <= i && i < n` — este último é
justamente o padrão que a spec §41/§42 quer analisar.

**Decisão.** Adicionar `!`, `&&`, `||` com curto-circuito, desugarados para `If`
(plano 05 §5.2).

**Precisa de confirmação:** sim (é adição de sintaxe à spec).

---

## Q5 🟡 — Ambiguidade de `<` em chamadas genéricas

**Problema.** `identity<Int>(10)` e `a < b > (c)` têm a mesma forma de tokens.

**Decisão.** Backtracking limitado: só é chamada genérica se o `>` de fechamento
for imediatamente seguido de `(`.

**Limitação aceita.** `a < b > (c)` parseia como chamada genérica. Contorno:
`(a < b) > (c)`. Documentado e testado explicitamente para que não seja
descoberto como "bug".

**Alternativa considerada:** turbofish (`identity::<Int>(10)`). Rejeitada por
divergir da sintaxe que a spec exemplifica.

---

## Q6 🔴 — Exaustividade de `match`

**Problema.** A spec §22 não diz se `match` precisa cobrir todas as variantes.

**Decisão.** Sim, obrigatória (`LAP0262`), com `_` disponível.

**Justificativa.** `match` é uma expressão que precisa produzir um valor; um
`match` não exaustivo teria que ter um comportamento definido para "nenhum braço
casou", e as opções (abortar, retornar `Void`) são ambas piores que exigir
cobertura. Além disso, exaustividade é o que permite ao PE eliminar braços
impossíveis com segurança (plano 14).

**Precisa de confirmação:** sim.

---

## Q7 🔴 — Inferência de argumentos genéricos

**Problema.** A spec §34 chama `print(result)` sem argumentos genéricos, mas §13
sempre mostra generics explícitos (`identity<Int>(10)`). E `print` precisa ser
genérico para aceitar qualquer valor.

**Decisão.** Inferência de 1ª ordem para **parâmetros de tipo** quando os
argumentos genéricos são omitidos numa chamada: casamento posicional entre os
tipos dos parâmetros formais e os dos argumentos reais. Parâmetros **const**
nunca são inferidos (`LAP0296`).

**Justificativa.** É o mínimo para os exemplos da spec funcionarem, e é
previsível (spec §47) — nada de unificação global.

**Precisa de confirmação:** sim.

---

## Q8 🔴 — Recursão

**Problema.** A spec não menciona recursão. `Let(x, v, body)` com `x` invisível
em `v` (a leitura natural de "bindings são imutáveis", §8) a torna impossível.

**Decisão.** **Sem recursão na v0.2.** `def f = fn() { return f(); };` é
`LAP0201`.

**Justificativa.** (a) É a leitura literal de §8 e §48; (b) sem recursão, a
terminação do partial evaluator é trivial, o que deixa M6–M8 focados no que a
spec quer estudar em vez de em estratégias de generalização; (c) é reversível —
`def` recursivo pode ser adicionado depois sem quebrar nada.

**Impacto.** A linguagem não é Turing-completa na 0.2. Isso é uma limitação séria
para "pesquisa de avaliação" a médio prazo, e **deve ser revisitada na 0.3**
junto com a estratégia de terminação do PE (memoização de especializações +
generalização de argumentos, ao estilo dos supercompiladores).

**Precisa de confirmação:** sim — é a decisão de maior impacto deste apêndice.

---

## Q9 🔴 — Divisão inteira por zero

**Problema.** A spec não define. As opções: retornar `Result` (muda o tipo de
`/`), produzir um valor indefinido, ou abortar.

**Decisão.** Abortar a execução com `LAP0301` e exit code 1. `Float` segue IEEE
754 (`inf`/`nan`), sem abortar.

**Justificativa.** Retornar `Result` de `/` contradiria §25/§26, que tratam
aritmética como operação comum, e contaminaria toda expressão aritmética com
`Result`. Abortar mantém o tipo de `/` simples e é observável (portanto testável
na equivalência PE/evaluator).

**Precisa de confirmação:** sim.

---

## Q10 🟡 — Core AST sem `Block`

**Problema.** A spec §24 lista `Block` na Core AST.

**Decisão.** Não implementar. `Let(name, value, body)` sequencia; um statement
vira `Let` com nome fresco sintético. O printer re-achata cadeias de `Let` para
exibição.

**Justificativa.** §48 já sugere a forma `Let(x, v, ...)` com continuação; e §24
diz que "a estrutura exata poderá mudar". Um único nó de escopo simplifica todo
`switch`, e torna substituição/inlining no PE textuais.

---

## Q11 🟡 — `Field` na Core AST

**Problema.** §24 não lista um nó de acesso a membro, mas `user.id` e
`IndexError.OutOfBounds` precisam de um.

**Decisão.** Adicionar `CoreField(target, name)`, que serve para acesso a campo
de struct **e** para acesso a variante de enum (o checker distingue pela
`Resolution`).

---

## Q12 🟡 — Evaluator sobre a Core, não sobre a Surface

**Problema.** A spec §43–§48 sugere construir o evaluator (Etapa 4) antes do
desugar (Etapa 6).

**Decisão.** Desugar entra em M1; o evaluator nunca vê a Surface AST.

**Justificativa.** §36 define o evaluator como `Typed Core AST → Value`. Seguir a
ordem literal produziria um evaluator descartável e, pior, duas semânticas
concorrentes durante várias semanas.

---

## Q13 🟡 — `return` como expressão de tipo `Never`

**Problema.** `match r { Ok(v) => return v, Err(e) => return f }` exige `return`
em posição de expressão.

**Decisão.** `return` é uma expressão de tipo `Never` (bottom), subtipo de todo
tipo. Único caso de subtipagem da linguagem.

**Justificativa.** Um conceito resolve `return` em posição de statement, em braço
de `match`, em ramo de `if` e como operando — sem regras ad hoc.

---

## Q14 🟡 — `Result` do prelude vs. `Result` do usuário

**Problema.** O usuário pode escrever `def Result = enum { A };`. O que a
indexação passa a produzir?

**Decisão.** Sempre o `Result` do prelude, resolvido por identidade de
`TypeDefinition` na carga do prelude, não por busca de nome no escopo corrente.

**Justificativa.** Sombrear um nome não deve mudar a semântica de um operador.

---

## Q15 🟡 — `print` não é executado em tempo de partial evaluation

**Problema.** `print(30)` tem argumento conhecido. Um PE ingênuo o executaria.

**Decisão.** Efeitos nunca são executados em tempo de PE; `print` é sempre
residualizado.

**Justificativa.** Executá-lo moveria a saída do programa para o tempo de
compilação, violando `evaluate(P) ≡ evaluate(PE(P))` (spec §40) na dimensão que
mais importa observar.

---

## Questões deixadas em aberto para a 0.3

Sem decisão; listadas para não serem esquecidas.

| Tema | Pergunta |
|---|---|
| Recursão | `def` recursivo? mutuamente recursivo? qual estratégia de terminação no PE? |
| Módulos | a spec §3 diz "não existe conceito de módulo"; quando isso muda? |
| Mutabilidade | §8 exclui atribuição; análise de ranges fica bem mais interessante com laços |
| Laços | `for`/`while` estão em §23 como candidatos a desugar — desugar para quê, sem recursão? |
| Operador `?` | §23 cita "Result propagation" |
| Métodos | §23 cita "method syntax" |
| Conversões numéricas | sem promoção implícita, é preciso `intToFloat` no prelude |
| Dictionaries | §19 os remove explicitamente; reintroduzir quando? |
| `Never` visível | vale expor o tipo bottom ao usuário? |
| Overflow de `Int` | hoje é wrap (como C# `unchecked`); deveria ser `Result`? |

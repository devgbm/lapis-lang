# Apêndice C — Decisões de Design e Questões Abertas

Registro das escolhas que a spec não determina, ou determina de forma ambígua.
Este arquivo é **normativo para o projeto**: quando uma decisão nova contradiz uma
entrada daqui, ou a entrada antiga é revogada explicitamente (com o número de quem
a revogou), ou a decisão nova está errada. Não há terceira saída, e nenhuma
entrada some — código e decisão publicados não são reciclados.

## Como ler uma entrada

Cada entrada tem **problema, decisão, justificativa, estado** e, quando houver,
**relações** com outras entradas.

Duas dimensões independentes, que este apêndice já misturou no passado e agora
separa:

**Estado** — em que ponto a questão está:

| | Estado | Significado |
|---|---|---|
| ✅ | vigente | decidida e em vigor |
| 🔄 | encaminhada | decidida em parte, com saída definida para o resto |
| ⏳ | aberta | sem decisão; anotada para não ser esquecida |
| 🅿️ | estacionada | sem decisão **de propósito**, presa a outra questão |
| ⛔ | revogada | substituída por outra entrada, que é nomeada |

**Alcance** — quem enxerga a decisão:

| | Alcance | Significado |
|---|---|---|
| 🔴 | linguagem | muda o que um programa pode escrever |
| 🟡 | implementação | interna ao compilador; nenhum programa muda |

## Registro

| # | Tema | Estado | Alcance | Onde | Relações |
|---|---|---|---|---|---|
| Q1 | declaração de generics com parâmetros nomeados | ✅ | 🔴 | M4 | |
| Q2 | construção de `type` com `.Nome { campo: valor }` | ✅ | 🔴 | M3 | |
| Q3 | variantes de enum sempre qualificadas | ✅ | 🔴 | M2 | relaxada por Q23 dentro de `is` |
| Q4 | operadores `!`, `&&`, `\|\|` | ✅ | 🔴 | M1 | |
| Q5 | desambiguação de `<` por backtracking | ✅ | 🟡 | M4 | |
| Q6 | `match` exaustivo | ✅ | 🔴 | M3 | é o que segura Q22 |
| Q7 | argumentos genéricos sempre explícitos | ✅ | 🔴 | M4 | |
| Q8 | sem recursão | ⛔ | 🔴 | M1 | revogada por **Q34** |
| Q9 | `x / 0` produz o maior `Int` | ✅ | 🔴 | M1 | |
| Q10 | Core AST sem `Block` | ✅ | 🟡 | M1 | |
| Q11 | `Field` na Core AST | ✅ | 🟡 | M2 | |
| Q12 | evaluator sobre a Core, não sobre a Surface | ✅ | 🟡 | M1 | |
| Q13 | `return` como expressão de tipo `Never` | ✅ | 🟡 | M1 | |
| Q14 | `Result` do prelude vs. `Result` do usuário | ✅ | 🟡 | M2 | |
| Q15 | `print` não roda em partial evaluation | ✅ | 🟡 | M7 | |
| Q16 | statements que terminam em bloco dispensam `;` | ✅ | 🔴 | M1 | estendida por Q32 |
| Q17 | função literal como argumento genérico só em posição de expressão | ✅ | 🔴 | M4 | |
| Q18 | argumento const resolvível em tempo de compilação | ✅ | 🔴 | M4 | |
| Q19 | macro é declaração nomeada, não valor ligado por `def` | ✅ | 🔴 | M8 | |
| Q20 | `@match` compara variantes; sem `enumTag`/`enumPayload` | 🅿️ | 🔴 | — | presa a Q22 |
| Q21 | `constraint` roda na própria LapisLang | ✅ | 🔴 | M9 | |
| Q22 | `if` e `match` no compilador; `match` sai quando macro provar exaustividade | 🔄 | 🔴 | M16 | destravada em parte por Q23; segurada por Q6 |
| Q23 | `is` como primitiva da Core para ler carga de variante | ✅ | 🔴 | M16 · plano 25 | encaminha Q22; relaxa Q3 |
| Q24 | `goto` pode saltar para trás | ⛔ | 🔴 | M6 | revogada por **Q32** |
| Q25 | mutação com `var`; closure não captura `var` | ✅ | 🔴 | M6 | premissa de Q36 |
| Q26 | resolução de membro é type checking, sem nó novo na Core | ✅ | 🟡 | M13 | |
| Q27 | extension genérica casa receptor contra padrão, com `?` curinga | ✅ | 🔴 | M15 | |
| Q28 | `arr[i] = v` | ⛔ | 🔴 | — | revogada por **Q36** |
| Q29 | `[T;N]` é atribuível a `[T;?]` | ✅ | 🔴 | M12 | |
| Q30 | aritmética de tamanho no tipo | ⏳ | 🔴 | — | |
| Q31 | indexação devolve `Option`, não `Result` | ✅ | 🔴 | M12 | simetria questionada em Q36 |
| Q32 | derrubar `goto`/`label`; controle estruturado | ✅ | 🔴 | M16 · plano 26 | revoga Q24; destrava Q23 |
| Q33 | o norte do projeto passa a ser metaprogramação; PE adiado | ✅ | 🔴 | plano 27 | derruba a justificativa de Q8 |
| Q34 | recursão entra | ✅ | 🔴 | plano 27 §A1 | revoga Q8; encarece o PE |
| Q35 | `Str`: `length`, indexação e `Char` (A2a) | 🔄 | 🔴 | plano 27 §A2 | A2b em aberto |
| Q36 | escrita em elemento de span (`s[i] = v`), com warning | 🔄 | 🔴 | plano 27 §A3 | revoga Q28; tensiona Q31 |
| Q37 | módulos sem visibilidade por ora — tudo público | ✅ | 🔴 | plano 27 §B1 | |
| Q38 | evolução de macros (pseudo-keywords, splice, aninhamento, controle) | 🅿️ | 🔴 | — | pré-requisito de Q22 |
| Q39 | valor padrão de `T`; `.[T; n]` sem semente | ⏳ | 🔴 | — | recomendação: `def T.default` |

**A spec precisa ser atualizada** por causa destas decisões: §15/§16/§22
(variantes qualificadas), §44 (tokens `!`, `&&`, `\|\|`), §14/§24 (sintaxe de
construção), §25/§26 (divisão por zero), §10 da spec de macros
(`goto`/`label` saem) e — pela Q33 — **§61, o objetivo de pesquisa**. Detalhes em
cada entrada.

---

## Q1 ✅🔴 — Declaração vs. uso de generics

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

**✅ Confirmado pelo autor da spec.** Parâmetros genéricos são sempre nomeados na
declaração.

---

## Q2 ✅🔴 — Sintaxe de construção de `type`

**Problema.** A spec §14 define `type { id: Int; name: Str; }` e §24 lista
`Construct` na Core AST, mas nenhuma seção mostra como criar uma instância nem
como ler um campo.

**✅ Decidido pelo autor da spec:** a construção leva um **ponto inicial**.

```c
def u = .User { id: 1, name: "Gabriel" };
def n = u.name;
```

**Consequência boa:** o `.` inicial resolve a ambiguidade que motivou a pergunta.
Como nenhuma expressão comum começa com `.`, o parser sabe imediatamente que
`{` abre uma construção e não um bloco — então a restrição de "sem literal de
struct na condição de `if`/`match`" **deixa de ser necessária** e sai da
gramática. Isto passa a ser válido sem parênteses:

```c
if .Point { x: 1, y: 2 }.valid {
    ...
}
```

O custo é um caractere a mais por construção, pago uma vez, contra uma regra
contextual que o programador teria que carregar na cabeça.

---

## Q3 ✅🔴 — Variantes de enum: nuas ou qualificadas

**Problema.** A spec usa as duas formas: `Ok(10)` e `Err(error)` (§15, §16) nuas,
e `IndexError.OutOfBounds` (§21) qualificada.

**✅ Decidido pelo autor da spec:** **qualificação completa obrigatória**.

```c
Result.Ok(1)
Result.Err(error)
IndexError.OutOfBounds
```

Não há injeção de variantes no escopo. `Ok(10)` sozinho é `LAP0201` (variável
inexistente).

**Ganhos:** nenhuma poluição de escopo, nenhuma colisão possível entre enums com
variantes homônimas, e o diagnóstico `LAP0203` deixa de existir. Padrões de
`match` seguem a mesma regra: `Result.Ok(v)`, não `Ok(v)`.

**⚠️ A spec precisa ser atualizada.** Os exemplos das seções §15, §16 e §22 usam a
forma nua e passam a ser inválidos:

| Seção | Como está | Como fica |
|---|---|---|
| §15 | `Ok(10)`, `Err(error)` | `Result.Ok(10)`, `Result.Err(error)` |
| §21 | `Err(IndexError.OutOfBounds)` | já correto |
| §22 | `Ok(value) => value` | `Result.Ok(value) => value` |

---

## Q4 ✅🔴 — Operadores `!`, `&&`, `||`

**Problema.** A lista de tokens da spec §44 não inclui negação lógica nem
conjunção/disjunção. Sem elas, `Bool` só serve como condição de `if` e não há
como escrever `if !encontrado` ou `if 0 <= i && i < n` — este último é
justamente o padrão que a spec §41/§42 quer analisar.

**✅ Confirmado pelo autor da spec.** `!`, `&&` e `||` com curto-circuito,
desugarados para `If` (plano 05 §5.2). Implementados no M1.

**⚠️ A spec §44 precisa listar os três tokens.**

---

## Q5 ✅🟡 — Ambiguidade de `<` em chamadas genéricas

**Problema.** `identity<Int>(10)` e `a < b > (c)` têm a mesma forma de tokens.

**Decisão.** Backtracking limitado: só é leitura genérica se o `>` de fechamento
for imediatamente seguido de `(`.

**Limitação aceita.** `a < b > (c)` parseia como chamada genérica. Contorno:
`(a < b) > (c)`. Documentado e testado explicitamente para que não seja
descoberto como "bug".

**Alternativa considerada:** turbofish (`identity::<Int>(10)`). Rejeitada por
divergir da sintaxe que a spec exemplifica.

**✅ Confirmado pelo autor da spec**, com a limitação aceita.

### Ampliação no M4

A regra "seguido de `(`" não cobria dois usos legítimos, e ambos apareceram assim
que os generics saíram do papel:

```c
Result<Int, IndexError>.Ok(1);                          // seguido de '.'
def t = SomeType<"value", 1, true, Int, fn() Int { return 1; }>;   // seguido de ';'
```

O segundo é o **exemplo de const generics da própria spec §13**.

A regra passou a ser: aceita-se a leitura genérica quando o token após o `>` é
`(`, é `.`, **ou não pode iniciar uma expressão**. O terceiro caso não é uma
heurística nova — é a observação de que ali a leitura relacional não existe: `>`
é binário e ficaria sem operando à direita. Onde as duas leituras são possíveis
(`a < b > c`, `a < b > -c`) a relacional continua vencendo, e onde as duas
existem mas Q5 já havia decidido (`a < b > (c)`) nada muda.

---

## Q6 ✅🔴 — Exaustividade de `match`

**Problema.** A spec §22 não diz se `match` precisa cobrir todas as variantes.

**Decisão.** Sim, obrigatória (`LAP0262`), com `_` disponível.

**Justificativa.** `match` é uma expressão que precisa produzir um valor; um
`match` não exaustivo teria que ter um comportamento definido para "nenhum braço
casou", e as opções (abortar, retornar `Void`) são ambas piores que exigir
cobertura. Além disso, exaustividade é o que permite ao PE eliminar braços
impossíveis com segurança (plano 14).

**✅ Confirmado pelo autor da spec.**

---

## Q7 ✅🔴 — Inferência de argumentos genéricos

**Problema.** A spec §34 chama `print(result)` sem argumentos genéricos, mas §13
sempre mostra generics explícitos (`identity<Int>(10)`). E `print` precisa ser
genérico para aceitar qualquer valor.

**✅ Decidido pelo autor da spec:** **sem inferência por ora**. Argumentos
genéricos são sempre explícitos: `identity<Int>(10)`. Uma chamada a função
genérica sem argumentos é `LAP0290`.

**Consequência sobre `print`.** Aplicar a regra a uma assinatura
`print: fn<T>(T) Void` obrigaria a escrever `print<Int>(x)` em todo programa —
inclusive nos exemplos da spec, cujo §34 mostra `print(result)`. A saída adotada:

> `print` deixa de ser genérico. Sua assinatura passa a ser `fn(Any) Void`, onde
> `Any` é um tipo top **interno**: nenhuma sintaxe o produz, nenhum valor o tem,
> ele só aparece na assinatura de primitivas que aceitam qualquer valor.

Isso mantém `print(30)` exatamente como a spec escreve, e mantém a regra de
generics limpa — porque `print` simplesmente não é uma função genérica.

**Consequência sobre enums genéricos** (apurada no M4). A regra alcança também a
construção de variantes: `Result.Ok(1)` não diz qual é o tipo do erro. Os
argumentos vêm antes do ponto — `Result<Int, IndexError>.Ok(1)` — e a forma sem
eles é `LAP0298`, com nota apontando a forma correta.

Em **padrões** de `match` nada disso é necessário: ali o tipo do escrutinado já
fixa os argumentos, e `Result.Ok(value)` continua sendo a forma correta.

**Revisitar quando:** se `Any` começar a aparecer em mais de uma ou duas
primitivas, é sinal de que a inferência deveria voltar. O outro gatilho é o
incômodo de escrever `Option<Int>.None` — inferência a partir da anotação
resolveria justamente esse caso.

---

## Q8 ⛔🔴 — Recursão

> **⛔ Revogada pela Q34 (0.3).** A recursão entra. O que sustentava esta
> proibição era a justificativa (b) abaixo — "sem recursão, a terminação do
> partial evaluator é trivial" —, e ela servia ao PE, que a Q33 adiou. A própria
> entrada já previa: "deve ser revisitada na 0.3" e "é reversível". O texto
> original fica por inteiro, porque é ele que explica por que a linguagem passou
> quinze milestones sem recursão.

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

**✅ Confirmado pelo autor da spec.** Implementado no M1: `Let(x, v, body)` não
expõe `x` em `v`, e o teste `SelfReference_InOwnInitializer_ReportsLap0201`
trava o comportamento.

---

## Q9 ✅🔴 — Divisão inteira por zero

**Problema.** A spec não define. As opções: retornar `Result` (muda o tipo de
`/`), produzir um valor indefinido, ou abortar.

**✅ Decidido pelo autor da spec:** `x / 0` produz o **maior `Int`**
(`9223372036854775807`), qualquer que seja o sinal do dividendo. `Float` segue
IEEE 754 (`inf`/`nan`).

**Ganhos.** A divisão vira uma operação **total**: o tipo de `/` continua sendo
`Int` (sem contaminar toda expressão aritmética com `Result`, o que contradiria
§25/§26), e não há caminho de aborto em nenhuma operação binária. Três efeitos
concretos:

1. o evaluator perdeu um caso de `Completion.Abort` inteiro;
2. `LAP0301` foi **aposentado** (o código não é reciclado);
3. **o partial evaluator fica mais simples**: a regra de "divisão é impura porque
   pode abortar" (plano 12 §12.4, regra 3) some, e toda aritmética passa a ser
   dobrável sem análise de efeito.

**Caso de borda descoberto ao implementar.** `long.MinValue / -1` é a única
divisão inteira que estoura em Int64 — o quociente não cabe e o hardware trapeia,
mesmo em `unchecked`. Envolve para `long.MinValue`, coerente com o resto da
aritmética (`-MinValue == MinValue` em complemento de dois). Coberto por
`Divide_IsTotal_ForEveryIntPair`.

**⚠️ A spec deveria registrar isto**, já que §25/§26 não falam de divisão por zero.

---

## Q10 ✅🟡 — Core AST sem `Block`

**Problema.** A spec §24 lista `Block` na Core AST.

**Decisão.** Não implementar. `Let(name, value, body)` sequencia; um statement
vira `Let` com nome fresco sintético. O printer re-achata cadeias de `Let` para
exibição.

**Justificativa.** §48 já sugere a forma `Let(x, v, ...)` com continuação; e §24
diz que "a estrutura exata poderá mudar". Um único nó de escopo simplifica todo
`switch`, e torna substituição/inlining no PE textuais.

**Consequência descoberta ao implementar o M1.** Sem `Block`, o type checker não
distingue "redefinir no mesmo bloco" de "sombrear num bloco interno" — na cadeia
de `Let` as duas coisas têm exatamente a mesma forma. `LAP0202` passou portanto a
ser detectado no **desugar**, que ainda enxerga os blocos da Surface AST. É uma
verificação puramente sintática (dois `DefStatement` com o mesmo nome na mesma
lista de statements), então cabe naquela fase sem violar "o desugar não resolve
nomes".

---

## Q11 ✅🟡 — `Field` na Core AST

**Problema.** §24 não lista um nó de acesso a membro, mas `user.id` e
`IndexError.OutOfBounds` precisam de um.

**Decisão.** Adicionar `CoreField(target, name)`, que serve para acesso a campo
de struct **e** para acesso a variante de enum (o checker distingue pela
`Resolution`).

---

## Q12 ✅🟡 — Evaluator sobre a Core, não sobre a Surface

**Problema.** A spec §43–§48 sugere construir o evaluator (Etapa 4) antes do
desugar (Etapa 6).

**Decisão.** Desugar entra em M1; o evaluator nunca vê a Surface AST.

**Justificativa.** §36 define o evaluator como `Typed Core AST → Value`. Seguir a
ordem literal produziria um evaluator descartável e, pior, duas semânticas
concorrentes durante várias semanas.

---

## Q13 ✅🟡 — `return` como expressão de tipo `Never`

**Problema.** `match r { Ok(v) => return v, Err(e) => return f }` exige `return`
em posição de expressão.

**Decisão.** `return` é uma expressão de tipo `Never` (bottom), subtipo de todo
tipo. Único caso de subtipagem da linguagem.

**Justificativa.** Um conceito resolve `return` em posição de statement, em braço
de `match`, em ramo de `if` e como operando — sem regras ad hoc.

**Refinamento descoberto ao implementar o M1.** `Never` precisa **propagar**, não
só existir. `{ return 0; }` desugara para `Let($t, Return(0), ())`, cujo tipo
ingênuo seria `Void` — e aí `if c { return 0; } else { 2 }` não tiparia. Regras
adicionadas ao checker: um `Let` cujo valor sempre retorna tem tipo `Never` (o
corpo é inalcançável), e `Binary`, `Unary`, `Call` e a condição de `If` produzem
`Never` quando um subcomponente avaliado antes deles já diverge.

---

## Q14 ✅🟡 — `Result` do prelude vs. `Result` do usuário

**Problema.** O usuário pode escrever `def Result = enum { A };`. O que a
indexação passa a produzir?

**Decisão.** Sempre o `Result` do prelude, resolvido por identidade de
`TypeDefinition` na carga do prelude, não por busca de nome no escopo corrente.

**Justificativa.** Sombrear um nome não deve mudar a semântica de um operador.

---

## Q15 ✅🟡 — `print` não é executado em tempo de partial evaluation

**Problema.** `print(30)` tem argumento conhecido. Um PE ingênuo o executaria.

**Decisão.** Efeitos nunca são executados em tempo de PE; `print` é sempre
residualizado.

**Justificativa.** Executá-lo moveria a saída do programa para o tempo de
compilação, violando `evaluate(P) ≡ evaluate(PE(P))` (spec §40) na dimensão que
mais importa observar.

---

## Q16 ✅🔴 — Statements que terminam em bloco dispensam o `;`

**Problema.** O exemplo `abs` da própria spec §12 escreve

```c
if x < 0 {
    return -x;
}

return x;
```

sem `;` depois do `}` do `if`. Com a gramática original do Apêndice A isso não
parseava.

**Decisão.** `block`, `if_expr` e `match_expr` usados como statement não exigem
`;` — a mesma regra do Rust. Dentro de um bloco, a escolha entre "cauda" e
"statement" continua sendo feita pelo token seguinte (`}` ⇒ cauda), então não há
ambiguidade nova.

**✅ Confirmado pelo autor da spec.** É o que a spec já pressupunha nos próprios
exemplos — o `abs` de §12 não parsearia sem isso.

---

## Q17 ✅🔴 — Função literal como argumento genérico só em posição de expressão

**Problema.** A spec §13 admite uma função literal como argumento genérico
(`Make: fn() Int` recebendo `fn() Int { return 1; }`). Mas `fn(Int) Int` dentro
de uma anotação de tipo é um *tipo* de função, e as duas leituras colidem
exatamente onde o parser não tem contexto para escolher.

**Decisão.** Em posição de **tipo**, `fn` é sempre um tipo de função; a função
literal só é escrevível em posição de **expressão**. Na prática:

```c
def Wrapper = type<Make: fn() Int> { tag: Int; };

def w = .Wrapper<fn() Int { return 1; }> { tag: 0 };   // ok
def w: Wrapper<fn() Int { return 1; }> = ...;          // não escrevível
```

**Consequência.** Um `type` com parâmetro const de função é construível mas não
anotável. Como não há inferência a contrariar (Q7), a anotação nunca é
obrigatória — nenhum programa fica sem saída.

**Justificativa.** A alternativa seria fazer o backtracking `fn`-tipo/`fn`-valor
também dentro da gramática de tipos, o que exige um printer de código-fonte para
a Surface AST (hoje só a Core tem um) só para manter o round-trip do
`CoreSourcePrinter`. Custo alto para um caso que a spec cita uma vez.

**✅ Confirmado pelo autor da spec.**

**Revisitar quando:** se const generics de função virarem uso corrente.

---

## Q18 ✅🔴 — Argumento const tem de ser resolvível em tempo de compilação

**Problema.** Que expressões podem aparecer em posição de argumento const?
Literais, claro. E um `def`? E o `N` de um `fn<N: Int>` envolvente?

**✅ Decidido pelo autor da spec.** O critério é **um só**: o argumento tem de ser
resolvível em tempo de compilação. Nunca um valor de execução.

```c
def somefn = fn<N: Int>(x: Int) Int { return x * N; };

somefn<3>(x);          // literal            — ok
somefn<tres>(x);       // def ligado a 3     — ok
somefn<N>(x);          // parâmetro const    — ok, dentro de um fn<N: Int>
somefn<somevar>(x);    // parâmetro comum    — LAP0294
```

**Parâmetro const é constante.** Esta é a parte que não é óbvia: `N` dentro de
`fn<N: Int>` *é* uma constante, porque a própria regra acima garante que ele só
pode ter recebido um valor conhecido em compilação. Não conhecer o valor **ainda**
não o torna variável — só o torna **simbólico**.

Daí `ConstParameterArgument` no modelo de tipos: uma constante de valor pendente,
identificada pelo nome. É o análogo de `TypeParameterType` no mundo dos valores, e
`FixedArray<Int, N>` é um tipo tão legítimo quanto `Box<T>` — distinto de
`FixedArray<Int, 3>` até que a instanciação de fora feche o `N`:

```c
def Boxed = type<T, N: Int> { values: T[]; };

def make = fn<N: Int>(v: Int) Boxed<Int, N> {
    return .Boxed<Int, N> { values: [v] };
};

def b: Boxed<Int, 4> = make<4>(9);   // o retorno fecha em Boxed<Int, 4>
```

**O que conta como "resolvível" hoje.** Um literal, uma função literal, ou um
`def` ligado a um dos dois — direta ou indiretamente por uma cadeia de `def`s.
Isto é **propagação**, não *folding*: `def n = 1 + 2;` ainda não é constante,
porque dobrar a expressão é trabalho do partial evaluator (spec §58) e replicá-lo
no checker significaria manter duas aritméticas em sincronia. É a única lacuna
conhecida da regra, e o PE do M6 a fecha sem mudar nada aqui.

**Consequência sobre a execução.** Como o checker não monomorfiza, um valor
construído dentro de um corpo genérico carrega o argumento simbólico
(`Boxed<Int, N>`) nos seus `TypeArguments`. Isso não afeta igualdade nem
formatação, que olham definição e campos; quem fecha esses tipos é o PE.

---

## Q19 ✅🔴 — Como se declara uma macro

**Problema.** A proposta de macros escrevia `macro unless match ... expand { };` —
uma declaração nomeada. A 0.2 §2 é categórica: *"não existem declarações nomeadas
específicas para funções, tipos ou enums"*, e *"a forma única de introduzir um nome
é `def name = expression;`"*.

**Proposta.** `def unless = macro match ... expand { ... };`, como `fn`, `type` e
`enum`. Uma regra só para o idioma inteiro, e escopo, sombreamento e diagnósticos
de `def` valem de graça.

**A ressalva honesta.** Uma macro **não é um valor de runtime**: `print(m)` sobre
uma macro não faz sentido. O tipo dela seria `MacroType`, interno de compile time, e
usá-la em posição de valor seria `LAP0510`. Não é conceito novo — é o mesmo
tratamento que `MetaType` já dá a `type`/`enum` — mas é uma assimetria real entre
"o que `def` liga" e "o que é valor".

**Alternativa.** Manter `macro nome ...` como declaração à parte, aceitando a exceção
ao §2. Mais honesto quanto à natureza não-valor da macro, menos uniforme.

**✅ Decidido pelo autor: `macro <nome> match ... expand { ... };`** — a forma da
proposta original, mantida.

**Justificativa dele, que fecha a questão:** uma macro **não é first-class
citizen**. `def` liga valores; macro não é valor. Não existe em runtime, não é
argumento, não é retorno. Forçá-la a passar por `def` exigiria um `MacroType` que
só existe para proibir tudo o que `def` normalmente permite — a uniformidade seria
aparente e a assimetria, real.

**Consequência:** é a única exceção ao §2, e ela é justificada em vez de tolerada.
Macros ocupam um espaço de nomes próprio: `macro log` e `def log = fn ...` convivem,
porque `@log` e `log` nunca se confundem. Usar uma macro em posição de valor é
`LAP0510`.

---

## Q20 🅿️🔴 — `@match` compara variantes (estacionada junto com `@match`)

**Problema.** Para `@match` ser macro, ele precisa de duas primitivas que
`goto`/`label` não dão: `enumTag(valor)` e `enumPayload(valor, índice)`. O
comentário em `CoreNodes.cs` rejeitou exatamente isso no M3:

> desugará-lo exigiria primitivas `enum_tag` e `enum_payload`, aumentando o
> runtime — contra a spec §58 ("runtime mínimo")

**A conta, agora com os dois lados:**

| Sai | Entra |
|---|---|
| `CoreMatch`, `CorePattern` e família | `enumTag` |
| `TryMatch` do evaluator | `enumPayload` |
| `MatchCoverage` e a exaustividade do checker | |
| `CoreIf` | |

Cinco estruturas de C# contra duas nativas. **A conta favorece as macros.**

**O obstáculo que decide o cronograma.** O tipo de `enumPayload` depende da
variante: em `Result.Ok(v)`, `v` é `T`; em `Result.Err(e)`, `e` é `E`. Uma
assinatura `fn(Any, Int) Any` perderia isso, e um `@match` expandido seria **menos**
tipado que o `Match` de hoje — o oposto do objetivo.

A saída é `enumPayload` ser intrínseco do checker, com tipo derivado da variante
testada no caminho de controle. Isso exige que o checker ganhe **noção de fluxo**, e
o checker de hoje é uma travessia única dirigida por sintaxe (§47).

**✅ Decidido pelo autor, e a decisão dissolve o problema em vez de pagá-lo:**
`@match` **não desestrutura carga**. Ele compara a variante, e só. Basta que enums
sejam comparáveis — e são, desde o M3.

Com isso `enumTag` e `enumPayload` **não são necessários**, o comentário do
`CoreNodes.cs` continua correto, e o obstáculo do tipo dependente de variante
desaparece: não há carga a tipar.

A única adição é uma regra de tipo estreita: comparar um valor de enum com um
**construtor de variante** não aplicado compara apenas a variante.
`r == Result.Ok` tem tipo `Bool`. Uma regra e uma linha no evaluator, contra duas
nativas e uma análise de fluxo.

**O que a decisão custa** foi o que a estacionou. A ligação de carga
(`Result.Ok(value) => ...`) deixa de existir, e nenhuma das saídas examinadas em Q23
se sustentou. `@match` saiu do escopo, e esta decisão fica guardada: **quando ele
voltar, a comparação por variante continua sendo a forma certa de despachar.** O que
falta não é o despacho — é ler a carga.

---

## Q21 ✅🔴 — `constraint` roda na própria LapisLang

**Problema.** Que linguagem roda dentro de `constraint`? Uma linguagem de macro
separada (como `macro_rules!` do Rust) ou a própria linguagem?

**✅ Decidido pelo autor.** A própria, no **mesmo evaluator** do plano 08. O que
muda entre as fases é só o ambiente: nativas e escopo.

**Justificativa.** O princípio §58.3 diz que o evaluator é a implementação de
referência da semântica. Uma segunda linguagem precisaria de segundo lexer, parser,
checker e evaluator, todos com o mesmo dever de correção, e a divergência entre as
duas seria fonte permanente de bugs.

**Consequência boa:** o partial evaluator do M6–M9 roda em `constraint` também,
porque é o mesmo Core. Uma constraint cara pode ser especializada pela mesma máquina
que especializa o programa.

**Consequência a administrar:** `Lapis.Macros` passaria a depender de `TypeChecker` e
`Evaluator`, fechando um ciclo. A saída é a mesma do plano 09 (`PreludeScope` dado no
Runtime, `PreludeLoader` carga no Cli): `Lapis.Macros` declara uma interface
`IConstraintRunner` e o `Lapis.Cli` a implementa.

---

## Q22 🔄🔴 — `if` e `match` continuam no compilador (`match`, por ora)

**Problema.** Se `@if` e `@match` funcionarem, `if`/`match` viram macros do prelude
e deixam de ser keywords. Todo programa passa a escrever `@if`.

**Primeira decisão do autor:** sim, viram prelude.

**Decisão final, depois da análise de Q23:** **não.** `if` e `match` permanecem
construções do compilador; entram no prelude apenas construções que a linguagem
**não** tem — `@unless` e `@while`.

**O que mudou entre as duas.** `@if` sozinho é trivial. `@match` precisa ler a carga
de uma variante, e as três saídas examinadas caíram (Q23). O padrão comum:
**extrair carga com segurança é problema de linguagem, não de macro** — uma macro
transforma sintaxe, e não tem como estabelecer que um valor é da variante `Ok` no
ponto do acesso.

**E `@if` sem `@match` é pior que nenhum dos dois:** metade do controle vira prelude
e metade fica no compilador, e a análise de fluxo do plano 14 passa a ter **duas**
formas a entender em vez de uma. O ganho declarado — "uma forma só de controle" —
some se a substituição for parcial.

**Consequências:**

- `if` e `match` seguem palavras reservadas; `CoreIf` e `CoreMatch` seguem na Core;
- a desestruturação por padrão (`Result.Ok(value) => ...`) segue sendo como se lê
  uma carga;
- o plano 20 deixa de retirar nada, e nenhum programa existente muda de
  comportamento.

**Voltou à mesa com a Q23 (M16), e a decisão foi partida ao meio.**

A Q23 respondeu a metade que era dela: `is` é primitiva da Core (plano 25 §25.2),
então uma macro construída sobre ele **não** depende mais do `match`. O
argumento "nenhuma macro lê carga de variante" caiu.

O que sobrou, e que é o único motivo de `match` continuar aqui, é a
**exaustividade** (Q6/`LAP0262`). `match` é expressão e precisa produzir valor em
toda execução; provar que os braços cobrem o enum exige saber o tipo do
escrutinado, e uma macro roda **antes** do checker, sobre a Surface. Um `@match`
expandindo para `if e is A(x) { … } else if e is B(y) { … } else { ??? }` não tem
o que pôr naquele `else`: `throw` é compile-time (`LAP0507`), abortar
reintroduziria o caminho de falha em runtime que a §30 proíbe, e deixar o checker
reconhecer a forma expandida seria `match` no compilador com outro nome.

**Estado atual:** as duas construções coexistem, com papéis distintos —
`is` testa uma variante e **não** é exaustivo; `match` cobre todas e é.

**Volta à mesa** quando o sistema de macros souber provar exaustividade. Aí
`match` vira `@match` no prelude e `CoreMatch` sai da Core; `CoreIs` fica.

> O argumento de que "o plano 14 passaria a ter duas formas de controle a
> entender" não vale mais nessa direção: uma cadeia de `if`/`is` usa `CoreIf`,
> que o PE já trata, e faz `CoreMatch` desaparecer. É uma forma a **menos**.

---

## Q23 ✅🔴 — Construção para ler carga de variante com segurança

**Problema.** Para `@match` ser macro, é preciso ler a carga de uma variante fora do
`match` da Core. Três saídas foram examinadas, e **as três caíram**:

| Saída | Por que caiu |
|---|---|
| `enumTag`/`enumPayload` como nativas | o tipo da carga depende da variante; `fn(Any, Int) Any` tornaria `@match` **menos** tipado que o `Match` de hoje |
| Carga como campo (`result.value`), com nome na declaração | o **tipo** sai fácil — o nome do campo determina a variante —, mas nada garante que a variante seja a certa: `r.value` sobre um `Err` é indefensável, e §30 proíbe exceção de runtime |
| Campo + análise de dominância sobre o grafo de `Labeled` | funciona, e a análise até se paga (é a mesma do plano 14, para bounds-check elimination) — mas fazer a segurança de uma construção **básica** depender de análise de fluxo é peso demais para o que se ganha |

O padrão comum às três é o mesmo, e é o achado que fechou a questão:

> **Extrair carga com segurança é problema de linguagem, não de macro.**

Uma macro transforma sintaxe. Ela não tem como estabelecer que um valor é da
variante `Ok` no ponto do acesso — isso é papel de uma construção da linguagem, com
regra de tipo própria.

**✅ Decidido pelo autor: a palavra reservada é `is`** (plano 25).

```c
e is Some                     // Bool
e is Some(value)              // Bool, e liga `value` onde o teste é verdadeiro

if e is Some(value) { var a = value + 1; }
```

Os três requisitos abaixo são satisfeitos porque **a ligação e a prova nascem
juntas**: não existe posição em que `value` esteja em escopo e a variante seja
outra. Não há análise de fluxo, e não há caminho de runtime para o caso falso — há
ausência de escopo.

`is` é **primitiva da Core** (`CoreIs`, plano 25 §25.2), e não açúcar sobre
`match`. A primeira implementação (M16) o fez como açúcar e foi substituída:
daquele jeito a Q22 ficava circular — `match` seria insubstituível porque seu
substituto estava definido em termos dele. Como primitiva, `is` é justamente o
que dá à Q22 uma saída; o que ainda falta lá é a exaustividade.

O nó carrega os dois ramos (`Is(e, V, x?, então, senão)`) e não só um `Bool`:
com a ligação num nó irmão do teste, dar-lhe escopo exigiria análise de
dominância — a saída recusada acima. Dentro do nó, a ligação e a prova continuam
nascendo juntas.

Uma restrição vem junto: a ligação só vale onde o desugar consegue lhe dar escopo —
condição de `if` e operando esquerdo de `&&` (`LAP0730`).

> **Atualização com a Q32.** A formulação original tinha uma segunda restrição —
> "a ligação não atravessa um salto" (`LAP0731`) — pela mesma razão do `var`
> declarado entre um `goto` e o seu rótulo. Com `goto`/`label` derrubados, a
> restrição não tem mais o que proteger: `is` só aparece dentro de blocos léxicos
> comuns (`if`, `loop`), e um bloco léxico não tem o problema de múltiplos
> predecessores que motivava a regra. `LAP0731` fica reservado, não emitido —
> ver Q32 e o plano 25 revisado.

**O requisito, sem projetar a solução:**

- entregar o **tipo** da carga, derivado da variante nomeada no ponto do acesso;
- entregar a **garantia** de que a variante é aquela, sem depender de análise de
  fluxo;
- ter caminho definido quando não é — sem exceção de runtime (§30, Q9).

Especificar a forma antes de precisar dela seria projetar no escuro. O que está
registrado é o requisito e o que **não** funciona — que é a parte cara de descobrir.

**Quando houver resposta**, a conta de §58.2 fecha: saem `CoreMatch`, `CorePattern`
e família, o `TryMatch` do evaluator e a máquina de exaustividade; entra uma
construção só. E Q20 sai do estacionamento.

---

## Q24 ⛔🔴 — `goto` pode saltar para trás

> **⛔ Revogada pela Q32.** `goto`/`label` saíram da linguagem no M16, e com eles
> a pergunta. O que esta entrada decidiu e que **sobreviveu**: saltar para trás
> acabou com a terminação por construção, e a resposta foi orçamento com
> diagnóstico em vez de travamento. Isso continua valendo sobre `loop`
> (`LAP0303`, hoje "limite de iterações"). O resto era semântica de uma
> construção que não existe mais e foi removido daqui — está no histórico do
> plano 16.

## Q25 ✅🔴 — Mutação com `var`

**Problema.** `goto` para trás funciona (M6), mas nenhum laço escrito com ele
avançava: a linguagem não tinha como um valor mudar entre iterações.

```c
def i = 0;
label repete;
def j = i + 1;
print(j);
goto repete if j < 3;   // imprimia 1 para sempre, até LAP0303
```

**✅ Decidido pelo autor: mutação, com a palavra reservada `var`.**

```c
var i = 0;
label repete;
i = i + 1;
print(i);
goto repete if i < 3;   // 1, 2, 3
```

`def` continua definitivo; `var` pode ser reatribuído com `x = e;`. A atribuição é
**statement**, não expressão — o que elimina de uma vez `if (x = 1)` e a confusão
entre `=` e `==`.

### A restrição que faz isso caber: um `var` não atravessa fronteira de função

**✅ Decidido pelo autor: closure não captura `var`.** Usar um `var` de fora dentro
de uma função — lendo ou escrevendo — é `LAP0207`.

É o que dispensa a pergunta que uma linguagem com mutação normalmente precisa
responder: captura por valor ou por referência? Nenhuma das duas, porque não há
captura. E o preço que **não** se paga é o que importa aqui: sem aliasing, uma
closure continua sendo (código, ambiente imutável) para o partial evaluator, que é
a premissa dos planos 12 a 14.

O `Environment` continua persistente na **estrutura** — nenhum ambiente ganha ou
perde nomes. O que muda é o conteúdo de um slot marcado como mutável, e nenhuma
closure enxerga um desses.

### O que mais decorre

- **`var` nunca é constante de compilação:** `Somefn<umVar>()` é `LAP0294`, como a
  Q18 exige. O valor de hoje não é o de amanhã.
- **Atribuir a um `def` ou a um parâmetro é `LAP0206`.**
- **O tipo do `var` é o da declaração e não muda:** uma atribuição que não cabe é
  `LAP0210`.

### A restrição de escopo entre joins, e como ela encolheu *(histórico — ver Q32)*

> Esta subseção descreve o mecanismo de escopo de `goto`/`label`, retirado pela
> **Q32**. `loop`/`break`/`continue` não têm join point nenhum — o corpo de um
> `loop` é um bloco léxico comum, e a pergunta que esta subseção resolve não
> chega a existir. Fica como registro do raciocínio que levou até lá.

A formulação original desta seção era: uma declaração feita **depois** de um
`label` vive dentro daquele join, um join não enxerga os bindings de outro, e
portanto todo `var` que um laço usa precisa ser declarado **antes do primeiro
rótulo** do bloco.

A primeira metade continua verdadeira; a conclusão, não. Ela vinha de o desugar
achatar **todos** os rótulos de um bloco num grupo só — e a irmandade só é
necessária entre rótulos que se referenciam. Rótulos sem salto entre si formam
grupos **aninhados**, e aí uma declaração escrita entre dois laços os atravessa
como um `Let` comum.

O que resta da regra é exatamente o que ela sempre quis proteger, sem o excesso:

> O que um `goto` **explícito** pode ter pulado não está em escopo no destino.

Ver `Desugarer.CanSplitBefore` e os dois casos irmãos em
`tests/conformance/eval/mutation/`.

**Join com parâmetros** (`label L(x: Int);` / `goto L(x + 1);`) segue valendo como
possível evolução, agora por outro motivo: não é mais para contornar escopo, e sim
porque daria ao partial evaluator um grafo de fluxo em forma canônica.

---

## Q26 ✅🟡 — Onde a resolução de membro acontece

**Decidida ao avaliar a proposta.** A [spec de type
members](../spec/lapislang-type-members-0.1.md) §24 da versão original pedia uma
fase própria:

```text
... → Name Resolution → Type Resolution → Member Resolution → Member Desugaring → ...
```

Isso exigiria uma **segunda** análise de tipos antes do checker, e a 0.2 é, por
escolha explícita, uma travessia única dirigida por sintaxe (spec §47, plano 06).

**Decisão: member resolution é type checking.** `user.hello` já é
`CoreField(user, "hello")` e `user.hello()` já é `CoreCall(CoreField(...), [])`.
O checker resolve o membro no mesmo `CheckField` onde já resolve campo de struct e
variante de enum, e registra uma `Resolution` — como `VariantResolution` e
`ReflectResolution` já fazem.

Consequência que decide o custo da feature: **a Core não ganha nó nenhum e não há
fase de lowering.**

---

## Q27 ✅🔴 — Extensions genéricas casam receptor contra padrão?

**Decidida pelo autor — e a decisão dissolve a pergunta.**

`def<T> Result<T>.isOk` aplicado a um `Result<Int, IndexError>` exige casar o tipo
do receptor contra o padrão do dono e ligar `T`. Isso **é** unificação, e Q7
estabelece que argumento genérico nunca é inferido.

| Saída | Custo |
|---|---|
| A. só extensions especializadas | some a metade útil da feature |
| **B. unificação restrita ao dono** *(recomendada)* | é inferência, mas fechada: sem bounds, sem recursão, sem falha parcial |
| C. exigir o argumento na invocação (`result.isOk<Int>()`) | coerente ao pé da letra, e ninguém escreve |

O argumento a favor de **B**: o que Q7 recusa é deduzir o argumento de uma chamada
a partir dos valores passados. Aqui ele já está **escrito no tipo do receptor** —
`Result<Int, IndexError>` é o que o checker já sabe —, e o casamento só o
transporta para o corpo. Não há busca nem escolha.

Junto vem a sobreposição: se `Result<T>.descrever` e `Result<Int>.descrever`
coexistem, `Result<Int>` tem dois. A recomendação é **erro** (`LAP0720`) em vez de
uma regra de especificidade — falhar ruidosamente, como a 0.2 já faz com
`a < b < c`.

> **✅ Decidido pelo autor: nenhuma das três.** O alcance passa a ser **escrito**,
> com `?` como curinga:
>
> ```c
> def Result<?, ?>.isOk       = fn(self) Bool { ... };          // qualquer Result
> def Result<Int, ?>.maiorQue = fn(self, v: Int) Bool { ... };  // só Result<Int, ...>
> def Result.ok = fn<T>(value: T) Result<T, Error> { ... };     // membro genérico
> ```
>
> A regra em uma linha: **`<>` à esquerda do `=` fala do dono; `<>` à direita fala
> do membro.**
>
> `?` **não** é parâmetro, é curinga: não liga nome nenhum. Então não há
> unificação, não há o que transportar para o corpo, e a tensão com a Q7
> desaparece — não foi resolvida, deixou de existir.
>
> É também a **terceira** regra de subtipagem, com a mesma forma das duas
> anteriores: `Never <: T` (Q13), `[T;N] <: [T;?]` (Q29), `T<A,B> <: T<?,?>`. Nas
> três, esquecer o que se sabia é seguro e afirmar o que não se sabe não é. O `?`
> de `Result<?, ?>` é literalmente o mesmo `?` de `[Int;?]`.
>
> O preço é declarado: sem nome para o argumento do dono, o corpo não consegue
> escrevê-lo — `def Result<?, ?>.unwrapOr` não tem como anotar o `fallback`. O que
> precisa do argumento se escreve como membro genérico, que já funciona. Ver plano
> 23 §23.6.
>
> A sobreposição segue sendo `LAP0720`, e a decisão a melhora: com `?` escrito, os
> dois padrões que se cruzam estão **na fonte**.

---

## Q28 ⛔🔴 — `arr[i] = v`

> **⛔ Revogada pela Q36.** A escrita em elemento entra. Esta entrada a recusou
> apostando que a mutação iria **por API** (`push`, `setElement`) em vez de
> sintaxe — e a aposta não se sustentou: o spike do M17 mostrou que essa API não
> é escrevível em LapisLang, porque sem escrita em elemento não existe expressão
> que produza um span de elementos computados. A saída que esta entrada apontava
> não existia. A objeção que **sobreviveu** — "ignorar em silêncio perde escrita
> sem avisar" — foi respondida na Q36 com um warning.

## Q29 ✅🔴 — `[T;N]` é atribuível a `[T;?]`?

**Decidida pelo autor: sim, numa direção só.**

```c
def imprime = fn(s: [Int;?]) Void { ... };
imprime(.[1, 2, 3]);        // `[Int;3]` num parâmetro `[Int;?]`
```

`?` **não** é um tamanho diferente — é a ausência da informação. Um `[Int;3]` já é
um span cujo tamanho por acaso se conhece, então a conversão é esquecer o que se
sabia, e esquecer é sempre seguro. O contrário (`[T;?]` para `[T;N]`) não vale: o
tamanho poderia ser qualquer um em runtime.

É a **segunda** regra de subtipagem da linguagem (a primeira é `Never <: T`, Q13),
e ela não abre variância: o elemento continua invariante, `[Int;3]` não é
`[Any;?]`.

Consequência de runtime, também decidida: um span carrega **tamanho do elemento e
quantidade** junto do dado, porque é o que permite a `[T;?]` responder `length` sem
o tipo dizer. No evaluator atual isso já é verdade de graça — `SpanValue` guarda
`Elements` e `ElementType` —, e a exigência vale para um backend futuro.

> **Implementada no M12.** A relação vive em `TypeRelations.IsAssignableTo`, e
> **toda** posição que aceita um valor passa por ela — incluindo a atribuição a
> `var`, que até então comparava com `!=` e por isso rejeitava
> `var a = .[1]; a = .[1, 2, 3];`. A junção de ramos ganhou o par: dois spans do
> mesmo elemento e tamanhos diferentes juntam-se em `[T;?]`, o que dá tipo a
> `if c { .[1] } else { .[1, 2] }` e a `.[.[1], .[2, 3]]`.

---

## Q30 ⏳🔴 — aritmética de tamanho no tipo

`s.concat(.[4,5])` sobre `[Int;3]` daria `[Int;5]`, o que exige somar tamanhos **no
tipo** — primeiro degrau de tipos dependentes.

**Adiada pelo autor**, com uma razão melhor do que "é caro": span é a **base** de
`Array` e `List`, que virão como biblioteca, e é nelas que concatenação faz sentido
— com capacidade separada de comprimento e política de crescimento. Resolver
aritmética de tamanho na primitiva seria pagar por um caso que a biblioteca vai
reformular.

Por enquanto `concat` devolve `[T;?]`.

---

## Q31 ✅🔴 — indexação devolve `Option`, não `Result`

**Decidida pelo autor.** `s[i]` era `Result<T, IndexError>` (spec §21) e passa a
ser `Option<T>`.

`IndexError.OutOfBounds` nunca carregou informação: um enum de uma variante só,
cujo significado é "falhou". `Result` existe para o erro que **diz alguma coisa**;
onde não há o que dizer, `Option` é o tipo honesto.

E fecha uma assimetria que estava no repositório: `Option` foi para o prelude no M2
porque a spec §29 o cita, e ficou **sem um único consumidor** desde então. Agora
tem o seu — e `IndexError` fica sem nenhum, o que abre a pergunta de aposentá-lo
(recomendação do plano 24: sim, agora, que é quando a quebra custa menos).

> **Implementada no M12**, com uma metade que a decisão não previa: onde o tamanho
> **está** no tipo e o índice é constante, não há envelope nenhum. `Option<T>` é o
> caso de `[T;?]` e de índice dinâmico; `[T;N]` com índice constante devolve `T`
> direto, e o índice fora dos limites vira `LAP0244` em compilação. `IndexError`
> saiu do prelude.

---

## Q32 ✅🔴 — Derrubar `goto`/`label`; controle estruturado

**Problema.** A §25.4 do plano 25 travou em `goto L if e is Some(value)`: o destino
de um salto pode ter outros predecessores que não passam pela ligação de `is`, e a
regra de escopo do M11 (nenhum binding atravessa um `goto` explícito) existe
precisamente para recusar esse caso. As saídas eram todas caras — parâmetro em
join point, ou dominância, e a Q23 já tinha recusado dominância pelo mesmo motivo
("peso demais para o que se ganha").

**A pergunta certa não era "como fazer o binding atravessar o salto".** Era: por
que existe um salto para o binding atravessar?

**Levantamento do que `goto`/`label` realmente serviam.** Todo caso do corpus é
uma de duas formas — o padrão de laço (`@while`) ou o padrão de desvio condicional
(`@unless`) — mais os testes que demonstram o mecanismo cru em si. E a segunda
forma já era redundante: `if`/`else` existe como expressão estruturada desde o M3
(`IfExpression(Condition, Then, Else?)`, `Else` opcional), então `@unless` nunca
precisou de `goto` — sempre foi `if !condition { body }` escrito por um caminho
mais longo.

**✅ Decidido pelo autor: `goto`/`label` saem da linguagem.** No lugar:

```c
if <condição> <expressão | bloco> (else <expressão | bloco>)?

loop (: rótulo)? <bloco>              // laço infinito
break (rótulo)? (valor)?              // sai do loop, opcionalmente com valor
continue (rótulo)?                    // volta ao topo do loop
```

Detalhes de superfície, Core, checker, evaluator e PE: **plano 26**.

**Por que isso resolve a §25.4, e não só contorna.** O problema era join com
predecessores heterogêneos. `if`/`loop` não têm join: cada um é um bloco léxico
comum, com uma única forma de entrar. `if e is Some(v) { use(v); break; }` amarra
`v` no mesmo escopo onde `use`/`break` rodam — não existe segundo caminho até ali
que não passe pela amarração, porque não existe "ali" fora do bloco. A pergunta da
§25.4 deixa de fazer sentido, do mesmo jeito que a Q27 não foi respondida — foi
dissolvida.

**Por que agora, e não depois do M16.** O M6 e a regrouping (M11) ainda não tinham
sido usados como base de nada além de si mesmos — nenhuma milestone entre M12 e
M15 tocou `goto`/`label`. É o ponto mais barato em que essa troca vai existir:
depois do M16 ela custaria reabrir também o `is`, e depois do M17-19 custaria
reabrir o partial evaluator.

**O que se ganha, além de resolver a §25.4:**

| Perde | Ganha |
|---|---|
| `goto`/`label`, `LAP0520`–`LAP0522`, `LAP0731`; `LAP0303` muda de "saltos" para "iterações" sem trocar de número | `if`/`loop`/`break`/`continue` — quatro formas em vez de duas mais um mecanismo de join |
| o trabalho já feito em M6/M11 (join points, regrouping) | controle de fluxo estruturado, que é o substrato padrão da literatura de PE (Jones/Gomard/Sestoft) — `CoreLoop`/`CoreBreak` especializam sem reconstruir ambiente de join |
| — | `break`/`continue` rotulados resolvem laço aninhado sem o problema que motivou join com parâmetros |

O item da esquerda no meio dói — M6 e a regrouping foram trabalho real —, mas o da
direita paga na M17-19: os planos 13/14 iam ter que ensinar o especializador a
lidar com o grafo de joins mais cedo ou mais tarde, e `loop`/`break` chegam nele
sem essa forma.

**O que não muda:** Q25 (mutação com `var`) continua inteira — um `loop` com
progresso ainda precisa de `var`, exatamente como um `goto` para trás precisava.
Só o mecanismo de salto por baixo é que sai.

---

## Q33 ✅🔴 — O norte do projeto passa a ser metaprogramação

**Problema.** A spec §61 define o objetivo de pesquisa como *"quanto de um
programa pode ser executado antecipadamente quando parte de seus valores é
conhecida?"*. Quinze milestones depois, o que ficou mais interessante — e mais
original — foi outra coisa: uma linguagem que se define em si mesma, com macros
higiênicas validadas em tempo de compilação, `constraint` rodando no próprio
evaluator, reflection como valor comum e um prelude escrito na própria
linguagem.

**Decisão do autor.** O norte passa a ser **desenvolver o protótipo de uma
linguagem com foco em metaprogramação**. O partial evaluator continua no
roteiro, mas **adiado** para depois da biblioteca padrão.

**Justificativa.**

1. **Uma stdlib valida a linguagem melhor que qualquer suíte.** Escrever
   `Str`, `Array<T>`, `File` e `Console` exercita a linguagem como um usuário a
   exercitaria, e encontra o que testes escritos pelo próprio compilador não
   encontram. O primeiro spike já provou o ponto: descobriu que **nenhuma
   dessas bibliotecas é escrevível hoje** (ver Q36).
2. **Escrever o PE contra uma superfície que ainda cresce significa reabri-lo a
   cada milestone.** O PE precisa de um caso por construção; fechar a linguagem
   primeiro é mais barato do que reabrir o especializador seis vezes.
3. A tese de metaprogramação **já está demonstrada** e sustenta um protótipo
   sério: `@while` é biblioteca, não compilador (plano 20/26).

**Consequências.**

- **A spec §61 muda**, e o README com ela. É a primeira decisão deste apêndice
  que altera o objetivo declarado do projeto, e não apenas a linguagem.
- M17–M19 (PE) vão para o fim do roteiro (plano 27, fase F).
- **A justificativa (b) da Q8 cai**, o que reabre a recursão — ver Q34.
- Macros e reflection deixam de ser feature e passam a ser o eixo: Q38 recolhe a
  evolução pretendida.

**O que *não* muda.** O evaluator continua sendo a referência semântica, a suíte
de conformidade continua sendo a especificação executável, e a propriedade
`evaluate(P) ≡ evaluate(PE(P))` continua valendo para o que o PE já faz. Adiar
não é abandonar: nenhuma garantia existente é relaxada.

---

## Q34 ✅🔴 — Recursão entra

**Problema.** A Q8 proibiu recursão. Sem ela, metade de uma biblioteca padrão
não existe — e a Q33 removeu a razão da proibição.

**Decisão do autor.** **Uma função pode referenciar a si mesma.**

```c
def foo = fn(value: Int) Void { ... foo(2) ... };
```

**Viabilidade arquitetural** — o levantamento completo está no plano 27 §A1; o
resumo é que a linguagem já pagou quase tudo:

| Peça | Estado |
|---|---|
| tipar `foo` antes de checar o corpo | **já dá**: parâmetros são anotados por obrigação (spec §26) e o retorno omitido é `Void`, então a assinatura é derivável da sintaxe, sem inferência (Q7 intacta) |
| orçamento de profundidade | **já existe**: `MaxCallDepth = 10_000` e `LAP0302`, hoje inalcançáveis e isentos na cobertura de diagnósticos — a isenção sai |
| ambiente cíclico da closure | **é o trabalho real**: `Let(x, v, body)` não expõe `x` em `v`, e a closure captura o ambiente por valor |

**Escopo desta decisão.** Só **auto**-recursão. Recursão **mútua** (`a` chama
`b` declarado depois) é outra questão — exige olhar declarações adiante, o que
a Q8 nunca precisou responder — e fica em aberto.

**Custo aceito.** A terminação do PE deixa de ser trivial: passa a exigir
memoização de especializações e generalização de argumentos, ao estilo dos
supercompiladores. É exatamente o que a Q8 antecipou. A troca é consciente: a
linguagem vira Turing-completa e ganha uma stdlib, e o PE fica mais caro quando
voltar.

---

## Q35 🔄🔴 — `Str`: `length`, indexação e `Char`

**Problema.** `Str` é opaca. Não tem `length`, não é indexável, não há tipo
`Char`. As únicas operações são `+` (concatenação) e comparação. Nenhuma função
de string é escrevível.

**Proposta do autor.** Introduzir `Char` e tornar `Str` um caso especial de
span — `[Char;N]` —, ganhando `length` e indexação **de graça**, pelas regras
que os spans já têm.

**A favor.** É elegante e coerente: `"abc".length` viraria constante de
compilação pelo mesmo mecanismo de `.[1,2,3].length` (Q29/Q31), literal de
string teria tamanho no tipo, e `var s` alargaria para `[Char;?]` como qualquer
span. Zero regra nova para length e indexação.

**Contra, e é o que mantém esta entrada aberta.**

1. **O que é um `Char`?** Ponto de código Unicode, unidade UTF-16 ou grafema?
   São três linguagens diferentes. `StrValue` guarda um `string` de C#, que é
   UTF-16; indexar por ponto de código sobre UTF-16 é O(n) ou exige outra
   representação.
2. **Representação em runtime.** `StrValue(string)` viraria
   `SpanValue(ImmutableArray<Value>, Char)` — um `Value` por caractere. Custo de
   memória e alocação alto, e toda concatenação passa a copiar arrays de
   ponteiros. Aceitável num protótipo, mas é decisão consciente.
3. **Alcance da mudança.** `Str` aparece no prelude (`TypeInfo.name`,
   `FieldInfo.typeName`, `payloadTypeNames: [Str;?]`), na captura de macro
   (`Str:path`), em `contextGet`, em reflection e em `print`. `[Str;?]` viraria
   `[[Char;?];?]`.
4. **Concatenação.** Se `Str` é span, `s1 + s2` exige que spans concatenem — o
   que hoje é `LAP0280`. Ou `+` vira geral para spans (e aí resolve junto uma
   peça da Q36), ou `Str` mantém um `+` especial e a unificação fica pela
   metade.
5. **FFI depois.** String de C é sequência de **bytes** terminada em NUL, não de
   pontos de código. A escolha aqui decide o custo do marshalling na fase C.

**Saída intermediária a considerar.** Manter `StrValue` como representação e
expor `length`/indexação como **intrínsecos** (do mesmo jeito que `.length` de
span já é `SpanLengthResolution`), sem prometer que `Str` *é* um span. Destrava
a stdlib de strings com uma fração do custo, e deixa a unificação para quando
`Char` estiver decidido.

**✅ Decidido pelo autor: A2a.** `length` e indexação de `Str` entram como
**intrínsecos**, mantendo `StrValue` como representação; `Char` entra como tipo
primitivo. `Str` **não** vira `[Char;N]` agora.

**O que A2a fixa, porque código precisa de resposta:** `Char` é um **ponto de
código** Unicode. `s.length` conta pontos de código e `s[i]` indexa por ponto de
código — não por unidade UTF-16.

A escolha é a que preserva portas. Começar em unidade UTF-16 (o que C# dá de
graça, e O(1)) e migrar para ponto de código depois mudaria **silenciosamente o
resultado** de programas já escritos, sobre texto fora do plano básico. O
caminho inverso — começar em ponto de código e otimizar a representação — não
muda resultado nenhum. O custo aceito é `length` e indexação em O(n) sobre a
`string` de C#.

`Str` continua **sem tamanho no tipo**, então `s[i]` devolve `Option<Char>`
sempre, como `[T;?]` (Q31). Literal de string com tamanho no tipo é A2b.

**🔄 Encaminhada:** A2b — unificar `Str` com `[Char;N]` — segue em aberto, e só
volta à mesa depois que a stdlib de strings existir e mostrar se a unificação
compra o suficiente para pagar (2)–(4) acima.

---

## Q36 🔄🔴 — Escrita em elemento de span

**Problema.** Não existe hoje nenhuma expressão que produza um span de elementos
**computados e distintos**: as duas construções são a lista literal (`.[1,2,3]`)
e a repetição (`.[T; inicial; n]`, todos iguais), `a[0] = v` não parseia e spans
não concatenam. Logo `map`, `filter`, `split`, `sort` e `Array.push` não são
escrevíveis — o que a Q28 supunha resolvido "por API" não tinha como existir.

**Decisão do autor, na parte que está fechada.**

```c
var s1 = .[0, 1, 2];   // [Int;?] — var alarga o tamanho
s1[0] = 1;             // muta: s1 vira .[1, 1, 2]
s1[99] = 2;            // fora dos limites: nada acontece, sem erro e sem aviso
var y = s1[0];         // leitura continua devolvendo Option<Int> (Q31)

def s2 = .[0, 1, 2];   // [Int;3]
s2[0] = 1;             // erro: s2 é def, imutável (Q25)
var x = s2[4];         // erro de compilação: tamanho conhecido, índice fora
```

**Por que isto não reincide no que a Q28 recusou.** A Q28 rejeitou "ignorar em
silêncio" como regra geral. Esta proposta é mais estreita: o silêncio vale
**apenas onde o tamanho é desconhecido** (`[T;?]`), que é exatamente a situação
em que a linguagem já admite não saber — e onde a *leitura* já devolve `Option`
em vez de garantir. Onde o tamanho é conhecido, continua erro de compilação
(`LAP0244`, já implementado). O silêncio não é a regra; é o resíduo.

**Por que não introduz aliasing.** Span é valor. `s1[0] = 1` é atualização
funcional do span inteiro religada ao `var` — o mesmo mecanismo de `u.a.b = 1`,
que já existe (`CoreAssign` com caminho). A premissa da Q25 ("nenhuma closure
captura `var`, logo não há aliasing") fica intacta, e com ela a premissa do PE.

**O que fica em aberto — a assimetria.** Ler fora dos limites devolve `Option`:
a falha aparece **no tipo**, como a spec §30 exige. Escrever fora dos limites
não devolve nada: a falha não aparece **em lugar nenhum**. As duas metades da
mesma operação tratam a mesma falha de formas opostas, e atribuição é
*statement* de propósito (Q25), então não há onde um `Bool` de retorno ir parar.

Saídas possíveis, nenhuma escolhida:

| Saída | Custo |
|---|---|
| silêncio puro | assimetria com Q31; escrita perdida sem sinal nenhum |
| **`warning` de compilação** | pega o engano detectável sem custar tipo novo nem mexer na gramática |
| forma-expressão paralela (`def novo = s.comIndice(i, v);`) | precisa de `Option`/`Result` no retorno; é biblioteca, mas exige a escrita primitiva mesmo assim |

**✅ Decidido pelo autor: warning de compilação.** A escrita fora dos limites
continua sem efeito em runtime — nada aborta —, mas onde o compilador **consegue
ver** que ela vai falhar, ele avisa. Mecanismos mais fortes para o usuário
(forma-expressão, `Result`) ficam para depois.

Isso responde a objeção que a Q28 levantou e que sobreviveu: "perde escrita sem
avisar". Passa a avisar onde dá para avisar. O silêncio fica só onde o
compilador genuinamente não sabe — que é a mesma fronteira em que a *leitura*
devolve `Option` em vez de garantir, e portanto deixa de ser assimetria
arbitrária: **as duas metades da operação são honestas sobre o mesmo limite de
conhecimento.**

---

## Q37 ✅🔴 — Módulos sem visibilidade, por ora

**Problema.** Um sistema de módulos precisa decidir o que uma unidade exporta.
Distinguir público de privado é trabalho no checker, no formato de pacote e na
sintaxe.

**Decisão do autor.** **Tudo é público** na primeira versão. Sem `pub`/`private`.

**Justificativa.** O que trava a stdlib é multiarquivo, não encapsulamento. Uma
palavra reservada a menos é uma decisão a menos para revisar depois.

**Custo real, e ele não é zero.**

1. **Porta de mão única no formato `.lp`.** Um pacote sem campo de visibilidade
   obriga, quando ela chegar, a uma quebra de formato ou a um bump de versão.
   **Mitigação adotada:** o `.lp` v1 **grava o campo**, sempre com `public`.
   Reservar é grátis; retrofitar não é.
2. **Toda função auxiliar da stdlib vira superfície de API.** Combinado com a
   regra da casa — código publicado não é reciclado —, um helper interno passa a
   ser difícil de remover. É o custo aceito conscientemente.
3. Não afeta o checker: um escopo por unidade é a mesma estrutura com ou sem
   filtro de visibilidade.

---

## Q38 🅿️🔴 — Evolução do sistema de macros

**Problema.** O foco em metaprogramação (Q33) só se sustenta se as macros
crescerem. Hoje elas casam sequências de tokens e substituem — não constroem
nomes, não se compõem, não têm controle de fluxo na expansão.

**Direção pretendida pelo autor**, registrada agora e **executada depois**:

```c
macro value-x
    match 'pseudo-keyword' Type:t Str:i     // 'aspas simples' para pseudo-keyword
    expand { type { value_#i: $t } };       // splice: #i concatena no nome, $t insere o tipo

macro macro-a match @macro-b:b ...          // capturar outra macro já declarada
```

Mais: **condicionais e laços dentro do `expand`**.

**Por que está estacionada.** É mudança grande — toca matcher, substituição e
higiene — e o roteiro 0.3 precisa antes do básico (fases A–D). Além disso,
`#i`/`$t` introduzem duas formas novas de interpolação cuja interação com a
higiene (o `temp@1` de hoje) não é óbvia: um nome **construído** por
concatenação não tem contexto léxico de origem, e a regra de higiene atual
supõe que todo nome tem um.

**Relação com a Q22.** Exportar macros em pacotes está adiado até aqui, por
decisão do autor: macro precisa amadurecer antes de virar interface pública. E
`@match` (Q20/Q22) depende de controle de fluxo no `expand` para ser sequer
escrevível — esta entrada é pré-requisito daquela.

---

---

## Q39 ⏳🔴 — Valor padrão de `T` e `.[T; n]` sem semente

**Problema.** `.[T; semente; n]` exige uma semente do tipo `T`. Uma coleção
genérica não tem uma: `Array.new<T>(n)` não sabe com o que preencher. Hoje a
saída é o chamador fornecer (`Array.new<T>(semente, n)`), o que vaza um detalhe
de implementação para toda a API.

**Pergunta do autor.** Quanto custa criar o conceito de valor **padrão** e
aceitar `.[T; n]`?

**Análise.** O custo não está na sintaxe — está em *quem responde* "qual é o
padrão de `T`".

| Tipo | Padrão óbvio? |
|---|---|
| `Int`, `Float`, `Bool`, `Str`, `Char`, `Void` | sim — `0`, `0.0`, `false`, `""`, … |
| `type { … }` | sim, se todos os campos tiverem — recursivamente |
| **`enum`** | **não.** `Option<T>` é `None`? `Result<T,E>` é `Ok` ou `Err`? Um `enum { Red, Green, Blue }` é `Red` **porque foi escrito primeiro**? |

O caso do enum é o que mata a saída "padrão embutido para tudo": escolher a
primeira variante é arbitrário e **silenciosamente significativo**.

**Três saídas, e uma delas quase não custa nada porque a linguagem já a tem:**

| Saída | Como | Custo |
|---|---|---|
| padrão só para primitivos | `.[Int; 3]` vale, `.[MeuEnum; 3]` não | baixo; resolve buffer numérico e nada mais |
| **`def T.default`** | reusa membros de tipo (M13–M15). `def Int.default = 0;` no prelude; cada tipo declara o seu | **médio, e sem conceito novo** |
| slots `Option<T>` | `Array<T>` guarda `[Option<T>;?]` | zero de linguagem; custa um desembrulho por leitura |

**Recomendação: `def T.default`.** Ela não inventa nada — é a mesma máquina de
`def Result<?, ?>.isOk` (Q27), que já existe e já resolve membro sobre tipo
genérico. E funciona apesar da Q7 (sem inferência) justamente **por causa**
dela: como todo argumento genérico é explícito, no ponto de uso o `T` de
`Array.new<Int>(3)` já é `Int`, e o checker resolve `Int.default` ali mesmo.

A propriedade que a torna a saída certa para enums: ela é **opt-in**. Um enum
sem padrão sensato simplesmente não declara `default`, e `.[MeuEnum; 3]` vira
erro de compilação com mensagem clara — em vez de escolher `Red` por acidente
de ordem.

**Custo concreto:** (a) `default` para os primitivos no `prelude.ls`;
(b) o checker resolvendo `T.default` com `T` vindo de argumento genérico;
(c) diagnóstico novo para "`T` não declara `default`"; (d) decidir se `.[T; n]`
é açúcar para `.[T; T.default; n]` no desugar — provavelmente sim, e aí não há
nó novo na Core.

**Sem decisão, e não bloqueia nada agora.** A stdlib arranca com semente
explícita ou com slots `Option<T>`; `.[T; n]` é conveniência que pode entrar na
fase D sem quebrar o que já estiver escrito.


## Questões deixadas em aberto

Sem decisão; listadas para não serem esquecidas. As que ganharam número saíram
desta lista.

| Tema | Pergunta |
|---|---|
| Recursão mútua | `a` chama `b` declarado depois? (Q34 cobre só auto-recursão) |
| Mutabilidade no PE | `var` sobrevive à especialização? |
| Operador `?` | §23 cita "Result propagation" |
| Conversões numéricas | sem promoção implícita, é preciso `intToFloat` no prelude |
| Dictionaries | §19 os remove explicitamente; reintroduzir quando? |
| `Never` visível | vale expor o tipo bottom ao usuário? |
| Overflow de `Int` | hoje é wrap (como C# `unchecked`); deveria ser `Result`? |

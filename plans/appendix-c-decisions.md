# Apêndice C — Decisões de Design e Questões Abertas

Registro das escolhas que a spec 0.2 não determina, ou determina de forma
ambígua. Cada entrada tem: o problema, a decisão, a justificativa e o estado.

**As entradas marcadas 🔴 mudam a linguagem.** As marcadas 🟡 são internas à
implementação.

## Estado

| # | Tema | Estado |
|---|---|---|
| Q1 | declaração de generics com parâmetros nomeados | ✅ decidido · implementado |
| Q2 | construção de `type` com `.Nome { campo: valor }` | ✅ decidido · implementado |
| Q3 | variantes de enum sempre qualificadas | ✅ decidido · implementado |
| Q4 | operadores `!`, `&&`, `\|\|` | ✅ decidido · implementado |
| Q5 | desambiguação de `<` por backtracking | ✅ decidido · implementado |
| Q6 | `match` exaustivo | ✅ decidido · implementado |
| Q7 | argumentos genéricos sempre explícitos | ✅ decidido · implementado |
| Q8 | sem recursão na v0.2 | ✅ decidido · implementado |
| Q9 | `x / 0` produz o maior `Int` | ✅ decidido · implementado |
| Q10–Q15 | decisões internas de implementação | 🟡 em vigor |
| Q16 | statements que terminam em bloco dispensam `;` | ✅ decidido · implementado |
| Q17 | função literal como argumento genérico só em posição de expressão | ✅ decidido · implementado |
| Q18 | argumento const tem de ser resolvível em tempo de compilação | ✅ decidido · implementado |

**A spec 0.2 precisa ser atualizada** em quatro pontos por causa destas decisões:
§15/§16/§22 (variantes qualificadas), §44 (tokens `!`, `&&`, `\|\|`), §14/§24
(sintaxe de construção) e §25/§26 (divisão por zero). Detalhes em cada entrada.

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

**✅ Confirmado pelo autor da spec.** Parâmetros genéricos são sempre nomeados na
declaração.

---

## Q2 🔴 — Sintaxe de construção de `type`

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

## Q3 🔴 — Variantes de enum: nuas ou qualificadas

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

## Q4 🔴 — Operadores `!`, `&&`, `||`

**Problema.** A lista de tokens da spec §44 não inclui negação lógica nem
conjunção/disjunção. Sem elas, `Bool` só serve como condição de `if` e não há
como escrever `if !encontrado` ou `if 0 <= i && i < n` — este último é
justamente o padrão que a spec §41/§42 quer analisar.

**✅ Confirmado pelo autor da spec.** `!`, `&&` e `||` com curto-circuito,
desugarados para `If` (plano 05 §5.2). Implementados no M1.

**⚠️ A spec §44 precisa listar os três tokens.**

---

## Q5 🟡 — Ambiguidade de `<` em chamadas genéricas

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

## Q6 🔴 — Exaustividade de `match`

**Problema.** A spec §22 não diz se `match` precisa cobrir todas as variantes.

**Decisão.** Sim, obrigatória (`LAP0262`), com `_` disponível.

**Justificativa.** `match` é uma expressão que precisa produzir um valor; um
`match` não exaustivo teria que ter um comportamento definido para "nenhum braço
casou", e as opções (abortar, retornar `Void`) são ambas piores que exigir
cobertura. Além disso, exaustividade é o que permite ao PE eliminar braços
impossíveis com segurança (plano 14).

**✅ Confirmado pelo autor da spec.**

---

## Q7 🔴 — Inferência de argumentos genéricos

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

**✅ Confirmado pelo autor da spec.** Implementado no M1: `Let(x, v, body)` não
expõe `x` em `v`, e o teste `SelfReference_InOwnInitializer_ReportsLap0201`
trava o comportamento.

---

## Q9 🔴 — Divisão inteira por zero

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

## Q10 🟡 — Core AST sem `Block`

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

**Refinamento descoberto ao implementar o M1.** `Never` precisa **propagar**, não
só existir. `{ return 0; }` desugara para `Let($t, Return(0), ())`, cujo tipo
ingênuo seria `Void` — e aí `if c { return 0; } else { 2 }` não tiparia. Regras
adicionadas ao checker: um `Let` cujo valor sempre retorna tem tipo `Never` (o
corpo é inalcançável), e `Binary`, `Unary`, `Call` e a condição de `If` produzem
`Never` quando um subcomponente avaliado antes deles já diverge.

---

## Q14 🟡 — `Result` do prelude vs. `Result` do usuário

**Problema.** O usuário pode escrever `def Result = enum { A };`. O que a
indexação passa a produzir?

**Decisão.** Sempre o `Result` do prelude, resolvido por identidade de
`TypeDefinition` na carga do prelude, não por busca de nome no escopo corrente.

**Justificativa.** Sombrear um nome não deve mudar a semântica de um operador.

---

## Q16 🔴 — Statements que terminam em bloco dispensam o `;`

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

## Q15 🟡 — `print` não é executado em tempo de partial evaluation

**Problema.** `print(30)` tem argumento conhecido. Um PE ingênuo o executaria.

**Decisão.** Efeitos nunca são executados em tempo de PE; `print` é sempre
residualizado.

**Justificativa.** Executá-lo moveria a saída do programa para o tempo de
compilação, violando `evaluate(P) ≡ evaluate(PE(P))` (spec §40) na dimensão que
mais importa observar.

---

## Q17 🔴 — Função literal como argumento genérico só em posição de expressão

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

## Q18 🔴 — Argumento const tem de ser resolvível em tempo de compilação

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

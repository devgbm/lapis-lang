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
| Q19 | macro é declaração nomeada, não valor ligado por `def` | ✅ decidido |
| Q20 | `@match` compara variantes; sem `enumTag`/`enumPayload` | 🅿️ estacionada com `@match` |
| Q21 | `constraint` roda na própria LapisLang, no mesmo evaluator | ✅ decidido |
| Q22 | `if` e `match` **continuam** no compilador; `@while` entra no prelude | ✅ decidido |
| Q23 | construção de linguagem para ler carga de variante com segurança | ⏳ adiada, com requisito escrito |
| Q24 | `goto` pode saltar para trás; fim da terminação por construção | ✅ decidido |
| Q25 | mutação com `var`; closure não captura `var` | ✅ decidido |

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

## Q19 ✅ — Como se declara uma macro

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

## Q20 🅿️ — `@match` compara variantes (estacionada junto com `@match`)

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

## Q21 ✅ — `constraint` roda na própria LapisLang

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

## Q22 ✅ — `if` e `match` continuam no compilador

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
- o saldo de palavras reservadas passa a ser **só de entrada**: `macro`,
  `constraint`, `expand`, `goto`, `label`, `throw`. Nada sai;
- o plano 20 deixa de retirar nada, e nenhum programa existente muda de
  comportamento.

**Volta à mesa** quando Q23 tiver resposta.

---

## Q23 ⏳ — Construção para ler carga de variante com segurança

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

**✅ Decidido pelo autor: adiar, e resolver com uma palavra reservada.** A
funcionalidade será atacada criando uma construção própria para acessar a carga com
segurança. `if` e `match` continuam no compilador até lá (Q22).

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

## Q24 ✅ — `goto` pode saltar para trás

**Problema.** Salto para trás permite laços — e permite programas que não terminam.
Até o M4 a linguagem não tinha recursão (Q8) nem laços, então **todo programa
terminava por construção**.

**✅ Decidido pelo autor:** `goto` pode saltar em qualquer direção, desde que o
rótulo esteja no mesmo escopo ou num que o contenha, **dentro da mesma função**.
Sair do escopo é `LAP0521`.

**O que se ganha:** `@while` no prelude, sem tocar no compilador e sem precisar de
recursão.

**O que se perde, com os olhos abertos:**

1. **Terminação por construção.** O evaluator passa a contar saltos e abortar com
   `LAP0303` — o mesmo tratamento que `LAP0302` já dá à profundidade de chamada.
2. **O PE passa a enfrentar laços.** Especializar um laço exige *widening* ou
   combustível, e é problema genuinamente mais difícil que especializar `If`.
   Registrado nos planos 13 e 14.
3. **`Never` muda de leitura.** Deixa de significar "não retorna" e passa a
   significar "não continua daqui" — que é o que sempre foi de fato.

**O que não se perde:** a pilha de C#. Um salto para trás é mais uma volta do laço
que consome completions no evaluator, não uma chamada recursiva.

**O que o M6 revelou, e a decisão não previa:** o salto para trás, sozinho, **não
produz laço com progresso** — nada mudava entre as voltas. Resolvido pela Q25, com
`var`.

---

## Q25 ✅ — Mutação com `var`

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

### A restrição de escopo entre joins, e como ela encolheu

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

## Questões deixadas em aberto para a 0.3

Sem decisão; listadas para não serem esquecidas.

| Tema | Pergunta |
|---|---|
| Recursão | `def` recursivo? mutuamente recursivo? qual estratégia de terminação no PE? |
| Módulos | a spec §3 diz "não existe conceito de módulo"; quando isso muda? |
| Mutabilidade | resolvido pela Q25 (`var`); falta decidir se `var` sobrevive à especialização do PE |
| Laços | `for`/`while` continuam candidatos a macro (plano 20), agora que `var` os torna possíveis |
| Operador `?` | §23 cita "Result propagation" |
| Métodos | §23 cita "method syntax" |
| Conversões numéricas | sem promoção implícita, é preciso `intToFloat` no prelude |
| Dictionaries | §19 os remove explicitamente; reintroduzir quando? |
| `Never` visível | vale expor o tipo bottom ao usuário? |
| Overflow de `Int` | hoje é wrap (como C# `unchecked`); deveria ser `Result`? |

# Plano 25 — `is`: testar variante e desembrulhar carga

**Projeto:** `Lapis.Lexer`, `Lapis.Parser`, `Lapis.Desugar`, `Lapis.TypeChecker`
**Milestone:** M16 (com o plano 26)
**Spec:** revisão da spec §22 — fecha **Q22** e **Q23**
**Depende de:** 03 (lexer), 04 (parser), 05 (desugar), 06 (checker), 23 (curinga)

> **Revisado pela Q32.** A versão original deste plano tinha uma restrição a
> mais — a ligação de `is` não atravessa um `goto` (§25.4 antiga, `LAP0731`) —
> porque `goto`/`label` ainda existiam. A Q32 os derrubou (plano 26), e a
> restrição não tem mais o que proteger: `is` só aparece dentro de blocos
> léxicos comuns, e um bloco léxico não tem o problema de múltiplos
> predecessores que motivava a regra. O texto abaixo já reflete isso — a nota
> serve para quem procura a versão antiga.

> **Nota de implementação (M16).** Duas decisões tomadas ao codificar este
> plano, que o texto original não previa:
>
> 1. **`LAP0730`–`LAP0734` saem do desugar, não do checker.** O plano supunha
>    que resolver dono/aridade de uma variante exigisse tipo — e por isso
>    "pertenceriam" ao checker. Na prática, o desugar já faz uma varredura
>    puramente sintática dos `def X = enum { ... }` visíveis (o mesmo espírito
>    de `ReportDuplicateDefinitions`) mais as variantes do prelúdio (passadas
>    de fora, já resolvidas — `Pipeline.KnownVariantsOf`), e resolve dono e
>    aridade **antes** de montar o `CoreMatch`. Duas variantes de nomes iguais
>    em enums diferentes sem dono escrito é `LAP0732` (ambíguo), pelo mesmo
>    motivo que "não existe" é `LAP0732`. O ganho: o checker não precisa saber
>    que um `CoreMatch` veio de `is` — ele vê o `match` de sempre, com
>    `EnumName` já preenchido, e usa exatamente `CheckVariantPattern` sem
>    nenhum caso novo.
> 2. **Os argumentos genéricos do dono não são validados contra o
>    escrutinado.** `Result<Int, ?>.Ok(v)` é aceito pela gramática e não
>    reporta erro — mas, como em `match`, os tipos da carga vêm da instância
>    real do escrutinado, não do que o padrão escreveu. `Is_WithWrongGenericOwner`
>    (a lista de testes abaixo) não tem caso: validar essa concordância exigiria
>    uma checagem que `match` nunca teve, e ficou fora desta rodada. Registrado
>    aqui, não como pendência silenciosa.

---

## Objetivo

```c
e is Some                     // Bool
e is Some(value)              // Bool, e liga `value` onde o teste é verdadeiro
```

A construção que a **Q23** deixou em aberto: ler a carga de uma variante com
segurança, sem análise de fluxo e sem exceção de runtime.

## Escopo

**Entra:** `is` como palavra reservada, teste de variante, ligação da carga,
escopo da ligação, padrão com dono genérico (`e is Result<Int, ?>.Ok(v)`).

**Fica de fora:** `@match` como macro do prelude (Q22 mantém `match` no
compilador); padrões aninhados no `is` — `e is Some(Other(x))` fica para depois,
porque `match` já cobre e a forma composta pede menos.

---

## O que será construído

### 25.1 O requisito da Q23, satisfeito

A Q23 pedia três coisas de qualquer solução. `is` entrega as três:

| Requisito da Q23 | Como `is` entrega |
|---|---|
| o **tipo** da carga, derivado da variante nomeada | a variante está escrita no padrão; o tipo sai da declaração do enum |
| a **garantia** de que a variante é aquela | a ligação só existe onde o teste é verdadeiro — não há posição em que `value` exista e a variante seja outra |
| caminho definido quando não é | o `else`/o resto do programa, onde a ligação simplesmente não está em escopo |

E o que faz a garantia ser barata é a mesma coisa que faz `match` funcionar: **a
ligação e a prova nascem juntas**. Não há um passo em que se sabe a variante e
outro em que se lê a carga.

### 25.2 `is` é açúcar sobre `match` — e é isso que o torna barato

Nenhum nó novo na Core. As duas formas com ligação viram `match`:

```c
if e is Some(v) { A } else { B }
// ⇓
match e { Option.Some(v) => A, _ => B }

e is Some(v) && resto
// ⇓
match e { Option.Some(v) => resto, _ => false }
```

E a forma sem ligação é um `match` que devolve `Bool`:

```c
e is Some
// ⇓
match e { Option.Some => true, _ => false }
```

O ganho não é só de implementação. Como `is` **é** um `match`, o partial
evaluator, a análise de retorno e a exaustividade continuam valendo sem uma linha
nova — e o plano 14 continua com uma forma de controle a entender, que é
exatamente o que a Q22 protegeu.

### 25.3 O escopo da ligação — a parte que precisa de regra

`e is Some(v)` produz um `Bool` e liga `v`. Só que uma expressão booleana pode
aparecer em qualquer lugar, e a ligação **não** pode:

```c
def b = e is Some(v);      // e `v` vale onde, depois disto?
print(v);                  // aqui? em que estado?
```

> **A ligação só é permitida onde o desugar consegue lhe dar escopo.** São duas
> posições, e são exatamente as duas em que a forma tem leitura óbvia:
>
> 1. condição de `if` → liga no ramo verdadeiro;
> 2. operando esquerdo de `&&` → liga no operando direito e adiante.
>
> Em qualquer outra posição, `is` com ligação é `LAP0730`.
>
> Não há uma terceira posição para `goto ... if` — o plano 26 derrubou `goto`.
> Um `loop` não precisa de posição própria: o padrão é escrever a condição na
> posição 1, dentro do corpo do laço (`loop { if e is Some(v) { usa(v); } else
> { break; } }`), que já é coberta. É a mesma posição que `@while` novo usa
> (plano 26), e é por isso que `@while e is Some(v) { ... }` liga `v` de graça.

A forma **sem** ligação (`e is Some`) é um `Bool` como outro qualquer, e vai onde
`Bool` vai.

A composição que o autor pediu cai da regra 2:

```c
if bRes is Result<Int, Error>.Ok(value) && value == true { X }
// ⇓
match bRes {
    Result.Ok(value) => if value == true { X } else { },
    _ => { }
}
```

### 25.4 O problema que motivou esta pergunta, e como ele deixou de existir

A versão original desta seção discutia `goto L if e is Some(value)`: um `label`
é alcançável de vários lugares (é um join point por definição), o *fall-through*
alcança `L` sem nunca ter ligado `value`, e não havia resposta boa sem parâmetro
em join point ou análise de dominância — as duas saídas que a Q23 já tinha
recusado por peso.

A Q32 (Apêndice C) resolveu isso na raiz: `goto`/`label` saem da linguagem
(plano 26). Sem join point, a pergunta "quem mais chega aqui sem ter ligado
`value`" não faz sentido — todo lugar onde `is` pode ligar (`if`, `&&`) é um
bloco léxico de entrada única. O que a forma antiga queria dizer se escreve
direto:

```c
loop {
    if e is Some(value) {
        // `value` aqui, seguro
        break;
    }
}
```

### 25.5 O padrão

```ebnf
is_expr     = eq_expr ( "is" is_pattern )? ;
is_pattern  = ( IDENT generic_args? "." )? IDENT ( "(" IDENT ")" )? ;
```

O nome é `is_pattern`, e **não** `variant_pattern`: aquele já existe na gramática
de `match` (A.9) e é mais rico — aninha e liga vários nomes. A diferença é
deliberada (§25.8, `LAP0734`).

Três formas, todas do exemplo do autor:

```c
e is Some                              // enum vem do tipo de `e`
e is Some(value)
e is Result<Int, ?>.Ok(value)          // dono escrito, com curinga
```

**A variante pode vir sem qualificação, e isto relaxa a Q3.** A Q3 exige
`Color.Red` porque um `Red` solto em posição de expressão não tem como ser
resolvido. Aqui tem: o enum é o tipo do escrutinado, que o checker já conhece.
Escrever `e is Option.Some` continua válido e é o que se usa quando o leitor
precisa da dica.

> **`match` continua exigindo a forma qualificada.** Relaxá-lo também seria
> coerente, e é uma mudança separada — tocaria o corpus inteiro e não é
> necessária para esta feature. Fica anotado como pergunta, não como pendência.

### 25.6 `is` é palavra reservada

Decisão do autor. O custo é zero medido: `is` não aparece em nenhum `.ls` do
repositório, nem no prelude.

Um `is` **contextual** seria possível — depois de uma expressão completa, um
identificador `is` seguido de nome de variante só pode ser o operador. Está
registrado aqui porque a linguagem prefere contextual quando dá (`label`, `self`),
e porque a escolha é reversível nos dois sentidos. Reservado é mais simples de
ler, e é o que fica.

### 25.7 Precedência

`is` liga mais forte que `&&` e mais fraco que comparação:

```text
... 2 | &&
      | is          ← novo
    3 | == !=
```

É o que faz `e is Some(v) && v == 1` parsear como `(e is Some(v)) && (v == 1)`, e
é a única leitura que a §25.3 consegue desugarar.

Como `is` não encadeia (`a is P is Q` não tem sentido), a produção usa `?` e não
`*` — mesma decisão de `rel_expr` com `a < b < c`, e o mesmo diagnóstico de
encadeamento.

### 25.8 Diagnósticos

```text
LAP0730  a ligação de 'is' só vale em condição de 'if' ou à esquerda de '&&'
LAP0732  '{0}' não é variante de {1}
LAP0733  a variante '{0}' não carrega valor
LAP0734  a variante '{0}' carrega {1} valores; escreva um padrão de 'match'
```

`LAP0731` fica **retirado e não reciclado** (existia para "a ligação não
atravessa um salto" — ver §25.4). Sem `goto`, a situação que ele descrevia não
acontece mais; o número não volta ao pool, mesma regra de `LAP0301`/`LAP0706`.

`LAP0732` reusa a mensagem de `LAP0251` de propósito: é o mesmo erro visto de
outro lugar.

`LAP0734` é a fronteira com `match`: `is` liga **um** valor. Variante com dois ou
mais manda escrever o `match`, que é onde a forma completa vive. É o mesmo
princípio de `is` não aceitar padrão aninhado — a construção existe para o caso
comum, e o caso completo já tem casa.

---

## Decisões de design

### Por que `is` fecha a Q23 e não reabre a Q22

A Q22 decidiu que `if` e `match` ficam no compilador porque `@match` precisaria
ler carga de variante, e nenhuma macro consegue. `is` **não muda isso**: ele é
açúcar sobre `match`, e uma macro que o usasse continuaria dependendo do `match`
da Core.

O que `is` compra é o que a Q23 pedia — uma construção da linguagem para o caso de
**uma** variante, que é o caso em que `match` é cerimônia demais:

```c
match e { Option.Some(v) => v + 1, Option.None => 0 }   // hoje
if e is Some(v) { v + 1 } else { 0 }                    // com `is`
```

### Por que não `if let`

`if let e = Some(v)` (Rust) amarra a construção ao `if`. `is` é uma expressão
booleana, então serve a `if` e a `&&` sem sintaxe nova para cada posição — e o
preço é a regra de escopo da §25.3, que precisa ser dita de qualquer jeito.

---

## Testes necessários

### Forma sem ligação

| Teste | Fonte | Esperado |
|---|---|---|
| `Is_ProducesBool` | `def b = e is None;` | `Bool` |
| `Is_Qualified` | `e is Option.None` | idem |
| `Is_UnknownVariant` | `e is Nada` | `LAP0732` |
| `Is_OnNonEnum` | `1 is Some` | erro de tipo |
| `Is_InLoopBody` | `loop { if e is None { break; } }` | compila |
| `Is_DoesNotChain` | `a is P is Q` | `LAP0114` |

### Ligação e escopo

| Teste | Fonte | Esperado |
|---|---|---|
| `Bind_InIfThen` | `if e is Some(v) { v + 1 }` | `v: Int` no ramo |
| `Bind_NotInElse` | `else { v }` | `LAP0201` |
| `Bind_NotAfterTheIf` | `print(v);` depois | `LAP0201` |
| `Bind_InRightOfAnd` | `e is Some(v) && v == 1` | compila |
| `Bind_InPlainDef` | `def b = e is Some(v);` | `LAP0730` |
| `Bind_InLoopViaIf` | `loop { if e is Some(v) { usa(v); break; } else { break; } }` | compila; `v` visível só no ramo verdadeiro |
| `Bind_InWhileCondition` | `@while e is Some(v) { usa(v); }` | `v` visível no corpo, a cada volta |
| `Bind_NullaryVariant` | `e is None(v)` | `LAP0733` |
| `Bind_MultiPayload` | variante de 2 cargas | `LAP0734` |
| `Bind_Shadows` | `v` já existe fora | sombreia, sem diagnóstico |

### Genéricos e integração

| Teste | Asserção |
|---|---|
| `Is_WithGenericOwner` | `iRes is Result<Int, ?>.Ok(v)` ⇒ `v: Int` |
| `Is_WithWrongGenericOwner` | `bRes is Result<Int, ?>.Ok(v)` ⇒ erro de tipo |
| `Is_DesugarsToMatch` | a Core do `is` é indistinguível da do `match` equivalente |
| `Is_IsFoldedByPE` | escrutinado conhecido ⇒ ramo escolhido, como `match` |
| `Is_RoundTrips` | `desugar(parse(print(core))) ≡ core` |
| corpus | equivalência do PE inalterada |

---

## Critérios de conclusão

- [ ] `is` reservada, com precedência entre `&&` e `==`, sem encadeamento.
- [ ] `e is P` produzindo `Bool` em qualquer posição de `Bool`.
- [ ] `e is P(v)` ligando `v`, e **só** nas posições da §25.3.
- [ ] **Zero nós novos na Core** — `is` desugara para `Match`.
- [ ] Variante sem qualificação aceita, com a qualificada continuando válida.
- [ ] `LAP0730`–`LAP0734`, cada um com caso de conformidade.
- [ ] Q22 intacta: `if` e `match` seguem no compilador.

---

## Perguntas ao autor

A pergunta original sobre `goto L if e is Some(value)` (§25.4) não precisa mais
de resposta — a Q32 derrubou `goto`, e a situação que a pergunta descrevia não
existe mais. Ficam as duas que eram independentes dela:

1. **Carga nomeada na declaração.** Os exemplos escrevem
   `enum<T> { Some(T value), None }` e `Ok(Tval value); Fail(Terr error);` — com
   **nome** na carga e `;` como separador. Hoje é `Some(T)` separado por `,`.
   É mudança pretendida ou escrita solta? Nenhuma das duas features precisa dela.

2. **Ordem em parâmetro.** `fn<T>(T value)` aparece nos exemplos; hoje é
   `fn<T>(value: T)`. Mesma pergunta.

# Plano 25 — `is`: testar variante e desembrulhar carga

**Projeto:** `Lapis.Lexer`, `Lapis.Parser`, `Lapis.Desugar`, `Lapis.TypeChecker`
**Milestone:** M16
**Spec:** revisão da spec §22 — fecha **Q22** e **Q23**
**Depende de:** 03 (lexer), 04 (parser), 05 (desugar), 06 (checker), 23 (curinga)

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

> **A ligação só é permitida onde o desugar consegue lhe dar escopo.** São três
> posições, e são exatamente as três em que a forma tem leitura óbvia:
>
> 1. condição de `if` → liga no ramo verdadeiro;
> 2. operando esquerdo de `&&` → liga no operando direito e adiante;
> 3. condição de `goto ... if` → **ver §25.4**.
>
> Em qualquer outra posição, `is` com ligação é `LAP0730`.

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

### 25.4 `goto L if e is Some(value)` — onde eu divirjo do exemplo

O autor escreveu esta forma. **Recomendo recusar a ligação aqui**, e a razão não é
implementação — é a regra que o M11 já estabeleceu.

Um `label` é um join point, alcançável de vários lugares. O corpo dele roda no
ambiente do grupo. É por isso que um `var` declarado entre um `goto` e o seu
rótulo **não** está em escopo no destino: o salto pode ter pulado a declaração
(ver `tests/conformance/eval/mutation/scope_across_joins_with_jump.ls`).

`value` tem exatamente esse problema, e pior: mesmo que só um `goto` alcance `L`,
o *fall-through* também alcança, e por ele `value` nunca foi ligado. Fazer a
ligação existir ali exigiria uma das duas coisas que este projeto já recusou:

- **parâmetros em join point** — muda `CoreLabeled`, o evaluator e o PE, para uma
  forma que `if` já cobre;
- **análise de dominância** — que é a terceira saída da Q23, recusada porque
  "fazer a segurança de uma construção básica depender de análise de fluxo é peso
  demais".

Então:

```c
goto fim if e is None;              // ok — sem ligação
goto fim if e is Some(value);       // LAP0731
```

E o que a segunda queria dizer se escreve com a primeira regra:

```c
if e is Some(value) {
    // `value` aqui, seguro
    goto fim;
}
```

**É a única divergência do que o autor escreveu, e vale confirmar.** Se a intenção
for mesmo ligar através do salto, o custo é parâmetro em join point, e isso é uma
milestone própria — não um detalhe deste plano.

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
LAP0731  a ligação de 'is' não atravessa um salto
LAP0732  '{0}' não é variante de {1}
LAP0733  a variante '{0}' não carrega valor
LAP0734  a variante '{0}' carrega {1} valores; escreva um padrão de 'match'
```

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
booleana, então serve a `&&` e a `goto ... if` sem sintaxe nova para cada
posição — e o preço é a regra de escopo da §25.3, que precisa ser dita de
qualquer jeito.

---

## Testes necessários

### Forma sem ligação

| Teste | Fonte | Esperado |
|---|---|---|
| `Is_ProducesBool` | `def b = e is None;` | `Bool` |
| `Is_Qualified` | `e is Option.None` | idem |
| `Is_UnknownVariant` | `e is Nada` | `LAP0732` |
| `Is_OnNonEnum` | `1 is Some` | erro de tipo |
| `Is_InGotoCondition` | `goto L if e is None;` | compila |
| `Is_DoesNotChain` | `a is P is Q` | `LAP0114` |

### Ligação e escopo

| Teste | Fonte | Esperado |
|---|---|---|
| `Bind_InIfThen` | `if e is Some(v) { v + 1 }` | `v: Int` no ramo |
| `Bind_NotInElse` | `else { v }` | `LAP0201` |
| `Bind_NotAfterTheIf` | `print(v);` depois | `LAP0201` |
| `Bind_InRightOfAnd` | `e is Some(v) && v == 1` | compila |
| `Bind_InPlainDef` | `def b = e is Some(v);` | `LAP0730` |
| `Bind_InGoto` | `goto L if e is Some(v);` | `LAP0731` |
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

1. **§25.4** — `goto L if e is Some(value)` recusa a ligação. Confirma, ou a
   intenção é mesmo ligar através do salto (o que pede parâmetro em join point,
   milestone própria)?

2. **Carga nomeada na declaração.** Os exemplos escrevem
   `enum<T> { Some(T value), None }` e `Ok(Tval value); Fail(Terr error);` — com
   **nome** na carga e `;` como separador. Hoje é `Some(T)` separado por `,`.
   É mudança pretendida ou escrita solta? Nenhuma das duas features precisa dela.

3. **Ordem em parâmetro.** `fn<T>(T value)` aparece nos exemplos; hoje é
   `fn<T>(value: T)`. Mesma pergunta.

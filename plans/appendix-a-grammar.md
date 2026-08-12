# Apêndice A — Gramática (normativa)

Notação EBNF. `X*` = zero ou mais, `X?` = opcional, `X ("," X)*` = lista.
Terminais em maiúsculas são tokens do plano 03.

---

## A.1 Programa

```ebnf
program        = statement* EOF ;

statement      = def_statement
               | assign_statement
               | goto_statement
               | label_statement
               | macro_declaration
               | expr_statement ;

def_statement  = ( "def" | "var" ) IDENT ( ":" type )? "=" expression ";" ;

assign_statement = IDENT "=" expression ";" ;

goto_statement = "goto" IDENT ( "if" expression )? ";" ;

label_statement = "label" IDENT ";" ;

expr_statement = block_like_expression ";"?      (* ponto-e-vírgula opcional *)
               | expression ";" ;
```

**Expressões que terminam em bloco dispensam o `;`.** `block_like_expression` é
`block`, `if_expr` e `match_expr`. Sem essa regra o próprio exemplo `abs` da
spec §12 não parsearia:

```c
def abs = fn(x: Int) Int {
    if x < 0 {
        return -x;      // o `if` não é seguido de `;`
    }

    return x;
};
```

A regra é a mesma do Rust e não introduz ambiguidade: dentro de um bloco, a
decisão entre "cauda" e "statement" continua sendo tomada pelo token seguinte
(`}` ⇒ cauda).

---

## A.2 Blocos

```ebnf
block          = "{" statement* expression? "}" ;
```

A expressão final sem `;` é a **cauda** do bloco e determina seu valor (spec §9).
Regra de desambiguação do parser: ao terminar de parsear uma expressão dentro de
um bloco, se o próximo token é `}` ela é a cauda; se é `;`, é um statement.

---

## A.3 Expressões (por precedência, do menor para o maior)

```ebnf
expression     = return_expr ;

return_expr    = "return" expression?
               | "throw" expression          (* compile time — A.10b, plano 18 *)
               | or_expr ;

or_expr        = and_expr ( "||" and_expr )* ;
and_expr       = eq_expr  ( "&&" eq_expr  )* ;
eq_expr        = rel_expr ( ( "==" | "!=" ) rel_expr )* ;
rel_expr       = add_expr ( ( "<" | ">" | "<=" | ">=" ) add_expr )? ;   (* não encadeia *)
add_expr       = mul_expr ( ( "+" | "-" ) mul_expr )* ;
mul_expr       = unary    ( ( "*" | "/" ) unary    )* ;

unary          = ( "-" | "!" ) unary
               | postfix ;

postfix        = primary postfix_op* ;

postfix_op     = "(" arg_list? ")"                      (* chamada *)
               | generic_args                           (* instanciação — ver A.7 *)
               | "[" expression "]"                     (* indexação *)
               | "." IDENT ;                            (* acesso a membro *)

arg_list       = expression ( "," expression )* ","? ;
```

`rel_expr` usa `?` em vez de `*` deliberadamente: `a < b < c` é rejeitado com
`LAP0114` em vez de parsear como `(a<b)<c`.

---

## A.4 Primárias

```ebnf
primary        = INT | FLOAT | STRING | "true" | "false"
               | "(" ")"                                (* literal Void *)
               | "(" expression ")"
               | IDENT
               | block
               | span_literal
               | construct_expr                         (* ver A.8 *)
               | fn_expr
               | type_expr
               | enum_expr
               | if_expr
               | match_expr ;

span_literal   = span_list | span_repeat ;

span_list      = "." "[" ( expression ( "," expression )* ","? )? "]" ;
span_repeat    = "." "[" type ";" expression ";" expression "]" ;   (* .[Int; 0; 8] *)

(* O ponto é o mesmo de `.User { }` (Q2): `[` inicia um **tipo**, `.[` inicia um
   **valor**. A desambiguação é de um token, e é o que permite ao tamanho viver no
   tipo (A.6) sem colidir com a construção.

   Entre as duas formas de `.[`, quem decide é o **separador**: `,` é lista, `;` é
   repetição — os mesmos `;` de `[Int;8]`. A escolha exige especulação de um
   componente, porque o primeiro é um tipo na repetição e uma expressão na lista, e
   um IDENT parseia como os dois. Depois do `;` a forma está fixada, e os erros
   passam a ser reportados em vez de engolidos. *)

if_expr        = "if" expression block ( "else" ( block | if_expr ) )? ;

match_expr     = "match" expression "{" match_arm ( "," match_arm )* ","? "}" ;
match_arm      = pattern "=>" expression ;
```

Note que a condição de `if` e o escrutinado de `match` usam `expression` sem
restrição alguma. Isso é possível porque a construção de `type` começa com `.`
(A.8): não há mais como confundir o `{` de um bloco com o de um literal de
struct.

---

## A.5 Funções, tipos e enums

```ebnf
fn_expr        = "fn" generic_params? "(" param_list? ")" type? block ;
param_list     = param ( "," param )* ","? ;
param          = IDENT ":" type ;

type_expr      = "type" generic_params? "{" field_decl* "}" ;
field_decl     = IDENT ":" type ";" ;

enum_expr      = "enum" generic_params? "{" ( variant ( "," variant )* ","? )? "}" ;
variant        = IDENT ( "(" type ( "," type )* ")" )? ;
```

O tipo de retorno de `fn` é opcional; ausente ⇒ `Void` (spec §6).

---

## A.6 Tipos

```ebnf
type           = type_primary ;
type_primary   = "[" type ";" span_size "]"      (* [Int;3]  [Int;?] *)
               | IDENT generic_args?
               | "fn" "(" ( type ( "," type )* )? ")" type
               | "(" type ")" ;

span_size      = INT | "?" | IDENT ;             (* literal, desconhecido, const generic *)
```

`Int[]` não existe: manter as duas formas exigiria escolher qual delas carrega o
tamanho, e dois jeitos de escrever o mesmo tipo é o que este projeto evita. Não
há mais forma pós-fixa de tipo, então `type` e `type_primary` coincidem — e `[`
deixa de ser ambíguo: **`[` é tipo, `.[` é valor** (A.5).

`span_size` com `IDENT` é o parâmetro const genérico: `[T;N]` dentro de um
`fn<N: Int>` fala do `N` da assinatura.

`Int`, `Float`, `Bool`, `Str`, `Void` são `IDENT` resolvidos pelo checker — não
são palavras-chave. Isso permite ao usuário sombreá-los, com as consequências
descritas no plano 09 §9.4.

---

## A.7 Generics

```ebnf
generic_params = "<" generic_param ( "," generic_param )* ","? ">" ;
generic_param  = IDENT                       (* parâmetro de tipo:   <T>      *)
               | IDENT ":" type ;            (* parâmetro const:     <N: Int> *)

generic_args   = "<" generic_arg ( "," generic_arg )* ","? ">" ;
generic_arg    = fn_expr                     (* valor de função     *)
               | "-"? ( INT | FLOAT ) | STRING | "true" | "false"   (* valor const *)
               | type ;                      (* tipo, ou IDENT ambíguo *)
```

`generic_args` é um pós-fixo **independente da chamada**: `identity<Int>` é uma
expressão por si só, e uma única produção cobre `identity<Int>(10)`
(`Call(Instantiate(...))`) e `Result<Int, E>.Ok(1)`
(`Call(Member(Instantiate(...)))`).

O sinal de `-1` é absorvido no literal, como no desugar de `-10`: um argumento
const é uma constante, não uma expressão a avaliar.

**Q1 — declaração vs. uso.** A spec §13/§14 mostra a forma de *uso*
(`type<"value", 1, true, Int, ...>`) na posição de *declaração*. Isso é
sintaticamente impossível de interpretar como declaração (não há nomes para os
parâmetros). A gramática acima adota parâmetros nomeados na declaração e valores
no uso:

```c
def FixedArray = type<T, N: Int> { values: [T;N]; };  // declaração
def a: FixedArray<Int, 3> = ...;                     // uso
```

**Q5 — ambiguidade `<`.** Em `postfix_op`, a alternativa `generic_args` só é
aceita se, após consumir os argumentos genéricos, o token seguinte for:

| Token | Por quê |
|---|---|
| `(` | chamada genérica: `identity<Int>(10)` |
| `.` | variante de enum genérico: `Result<Int, E>.Ok(1)` |
| qualquer token que **não** inicie expressão | a leitura relacional ficaria sem operando à direita, então não há ambiguidade: `def t = SomeType<"v", 1>;` |

Caso contrário o parser faz backtrack e trata `<` como operador relacional.

O terceiro caso é o que faz o exemplo de const generics da spec §13 parsear —
ele termina em `>` seguido de `;`. Os tokens que iniciam expressão são exatamente
os que abrem `primary` (§A.4) mais `-` e `!`.

Consequência conhecida: `a < b > (c)` parseia como chamada genérica, porque `(c)`
serve às duas leituras e Q5 decide pela genérica. Contornável com
`(a < b) > (c)`.

**Ambiguidade `IDENT` em `generic_arg`.** `Int` e `N` casam tanto com "tipo"
quanto com "valor const". O parser produz `NameArgumentSyntax(name)` e o checker
decide pelo parâmetro correspondente na declaração: `LAP0291` se um valor apareceu
onde se esperava tipo, `LAP0292` no contrário, `LAP0294` se o nome designa algo
que não é resolvível em tempo de compilação (Q18).

**Ambiguidade `fn` em `generic_arg`.** `fn(Int) Int` é um tipo; `fn(a: Int) Int
{ ... }` é um valor. Discriminador: só o valor tem corpo `{`. O parser tenta o
tipo e faz backtrack se encontrar `{` após o tipo de retorno.

A ambiguidade só existe em **posição de expressão**. Dentro de um `type`
(`x: Foo<fn(Int) Int>`), `fn` é sempre um tipo de função, e uma função literal
não é escrevível ali — registrado como Q17 no [Apêndice C](appendix-c-decisions.md).

---

## A.8 Construção de struct — Q2

```ebnf
construct_expr  = "." IDENT generic_args? "{" field_init_list? "}" ;
field_init_list = field_init ( "," field_init )* ","? ;
field_init      = IDENT ":" expression ;
```

A spec §24 inclui `Construct` na Core AST mas nunca define a sintaxe de
superfície. Adotado: **ponto inicial** seguido do nome do tipo.

```c
def u = .User { id: 1, name: "Gabriel" };
def b = .Box<Int> { value: 1 };
def n = u.name;                              // acesso continua sem ponto inicial
```

O `.` inicial não é decoração: é o que torna a gramática livre de contexto neste
ponto. Nenhuma outra expressão começa com `.`, então o parser decide entre bloco
e construção olhando **um único token**, sem precisar saber se está numa condição
de `if`. Por isso `if .Point { x: 1 }.valid { ... }` é válido sem parênteses.

O `.` de construção (prefixo) e o `.` de acesso a membro (pós-fixo) nunca
colidem: um só aparece onde se espera o início de uma expressão, o outro só
depois de uma expressão completa.

---

## A.9 Padrões

```ebnf
pattern         = "_"
                | binding_pattern
                | variant_pattern
                | literal_pattern ;

binding_pattern = IDENT ;                                    (* liga um nome novo *)
variant_pattern = IDENT "." IDENT ( "(" pattern ( "," pattern )* ")" )? ;
literal_pattern = INT | FLOAT | STRING | "true" | "false" | "-" INT | "-" FLOAT ;
```

**Q3 tornou os padrões não ambíguos.** Como variantes exigem qualificação
completa, um `IDENT` sozinho é *sempre* um binding novo e `Enum.Variante` é
*sempre* um padrão de variante — o parser decide sem consultar o escopo, e o
`IdentifierPattern` ambíguo do projeto original deixa de existir.

```c
match resultado {
    Result.Ok(valor) => valor,
    Result.Err(erro) => 0
}

match cor {
    Color.Red => "vermelho",
    outra => "outra"          // `outra` liga um nome
}
```

---

## A.10 Comentários

```ebnf
line_comment   = "//" ~( "\n" )* ;
block_comment  = "/*" ... "*/" ;              (* não aninha *)
```

---

## A.10b Macros e controle de fluxo — proposta

Da [spec de macros](../spec/lapislang-macros-0.1.md), planos 16–20.

**`macro_declaration`, o padrão, a invocação (M8), `constraint` e `throw` (M9)
estão implementados**; o resto desta seção segue proposta.

```ebnf
(* declaração nomeada — Q19: macro não é valor, não passa por `def` *)
macro_declaration = "macro" IDENT macro_rule+ ";" ;
macro_rule     = "match" macro_pattern
                 ( "constraint" block )?
                 "expand" block ;

macro_pattern  = macro_item+ ;
macro_item     = IDENT ":" IDENT              (* captura: Expression:e     *)
               | repetition                   (* Q23: grupos, para @match  *)
               | IDENT ;                      (* literal sintático: `in`   *)

repetition     = ( IDENT ":" IDENT | "(" macro_pattern ")" ) "*" "separado" "por" token ;

(* invocação *)
macro_call     = "@" IDENT macro_tokens ;

(* compile time — plano 18. Vive na cadeia de expressões (A.3), no mesmo nível
   de `return`: os dois têm tipo `Never` e cabem em qualquer posição.

   Ao contrário de `return`, o valor é **obrigatório**: a mensagem é o
   diagnóstico, e um `throw;` não teria o que dizer. *)
throw_expr     = "throw" expression ;

```

`macro_tokens` **não é uma produção da gramática**: a invocação é delimitada por
contagem de aninhamento (até o `;` de nível 0, ou o `}` que fecha o último bloco de
nível 0) e os tokens são entregues crus ao matcher. É o que dá sentido à §9 da spec
de macros — "macros definem sua própria sintaxe": `@foreach user in users { }` não
parseia com a gramática da linguagem, e não deveria.

Categorias de captura: `Expression`, `Statement`, `Block`, `Type`, `Identifier`,
`Literal`, `Int`, `Float`, `Str`, `Bool`. **A proposta original usava `String`**; o
primitivo se chama `Str` desde a 0.2.

A repetição de **grupo** (`(Identifier:a Type:b)*`) existe para itens compostos e é
o que evita inventar categorias sob medida para cada macro.

`goto`/`label` saíram desta seção: estão implementados (M6) e vivem em A.1, junto
dos demais statements.

---

## A.11 Precedência resumida

| Nível | Construção | Assoc. |
|---|---|---|
| 0 | `return`, `throw` | prefixo |
| 1 | `\|\|` | esquerda |
| 2 | `&&` | esquerda |
| 3 | `==` `!=` | esquerda |
| 4 | `<` `>` `<=` `>=` | **não encadeável** |
| 5 | `+` `-` | esquerda |
| 6 | `*` `/` | esquerda |
| 7 | `-` `!` unários | prefixo |
| 8 | `()` `[]` `.` `<>` | esquerda |

---

## A.12 Palavras reservadas

```text
def  var  fn  type  enum  return  true  false  if  else  match  goto
macro  expand  constraint  throw
```

`macro`, `expand` e `constraint` entraram no M8; `throw`, no M9. `@` passou a ser token: inicia
uma invocação de macro, e — só depois do primeiro caractere de um identificador,
seguido de dígitos — é o sufixo de higiene que a expansão produz (`temp@1`).

`var` declara um binding reatribuível (Q25); `def` continua definitivo. A
atribuição é **statement**: `=` nunca aparece em posição de expressão, então
`if (x = 1)` não existe e não há como confundir `=` com `==`.

`label` **não** é reservada: é palavra-chave **contextual**, só reconhecida quando
inicia um statement e vem seguida de um identificador. Reservá-la quebraria
programa válido — o exemplo da própria spec §13 usa `label` como nome de campo
(`type<Label: Str, ...> { label: Str; }`) — e a ambiguidade não existe, porque dois
identificadores seguidos nunca formam expressão. `goto` é reservada de verdade:
ninguém a usa como nome, e reservá-la é o que permite dizer "esperado um rótulo"
em vez de deixar a linha virar uma expressão malformada.

`throw` é reservada de verdade, apesar de só ser **válida** dentro de um
`constraint`: onde ela vale é pergunta do checker (`LAP0507`), não do lexer, e
tratá-la como contextual só trocaria um diagnóstico exato por uma expressão
malformada. Quem escreve `throw` fora de lugar recebe a explicação, não o
silêncio.

**Nada sai:** `if` e `match` continuam construções do compilador (Q22), e
`match` é reaproveitada dentro de `macro`, onde inicia uma regra — sem ambiguidade,
porque ali só pode ser isso.

`if` aparece também em `goto ... if ...`, o salto condicional a partir do qual
`@unless` e `@while` são construídos.


`Int`, `Float`, `Bool`, `Str`, `Void`, `Result`, `ContextError`,
`Option`, `print` **não** são reservadas — são bindings do prelude
ou nomes resolvidos pelo checker.

`contextHas`, `contextGet`, `contextPut` e `contextKeys` também não: são nativas, e
só existem no escopo de um `constraint` (plano 18). Num programa normal são nomes
livres, e recebem o `LAP0201` que qualquer outro receberia.

`reflect` **também não é reservada**, e nem é binding: é um **intrínseco**
reconhecido pelo checker numa posição de chamada (plano 19). Um `def reflect = ...`
do usuário vence — sombrear é permitido em toda parte, e quem escreve a própria
função `reflect` quis a sua. Ela não pode ser um binding comum porque o argumento
tem de ser um **tipo**, e "um tipo" não é expressável na gramática de tipos: não há
como escrever a assinatura de `reflect` em LapisLang. É o mesmo estatuto da
indexação, cujo tipo de resultado depende de o tamanho estar ou não no tipo do
alvo — `T` num caso, `Option<T>` no outro (plano 24) — sem existir assinatura
escrita para ela.

`length` segue a mesma lógica pelo outro lado: não é campo nem método, é um
membro que o checker resolve sobre qualquer span. Um `type` do usuário com um
campo chamado `length` continua funcionando; a resolução de span só se aplica a
spans.

`TypeInfo`, `FieldInfo`, `VariantInfo` e `TypeKind` são bindings do prelude, como
`Result`.

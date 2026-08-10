# Apêndice A — Gramática (normativa)

Notação EBNF. `X*` = zero ou mais, `X?` = opcional, `X ("," X)*` = lista.
Terminais em maiúsculas são tokens do plano 03.

---

## A.1 Programa

```ebnf
program        = statement* EOF ;

statement      = def_statement
               | expr_statement ;

def_statement  = "def" IDENT ( ":" type )? "=" expression ";" ;

expr_statement = expression ";" ;
```

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
               | generic_args "(" arg_list? ")"         (* chamada genérica — ver A.7 *)
               | "[" expression "]"                     (* indexação *)
               | "." IDENT                              (* acesso a membro *)
               | "{" field_init_list? "}" ;             (* construção — ver A.8 *)

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
               | array_literal
               | fn_expr
               | type_expr
               | enum_expr
               | if_expr
               | match_expr ;

array_literal  = "[" ( expression ( "," expression )* ","? )? "]" ;

if_expr        = "if" expression_no_struct block ( "else" ( block | if_expr ) )? ;

match_expr     = "match" expression_no_struct "{" match_arm ( "," match_arm )* ","? "}" ;
match_arm      = pattern "=>" expression ;
```

`expression_no_struct` é a mesma gramática de `expression` com a produção
`postfix_op = "{" ... "}"` desabilitada (ver A.8).

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
type           = type_postfix ;
type_postfix   = type_primary ( "[" "]" )* ;
type_primary   = IDENT generic_args?
               | "fn" "(" ( type ( "," type )* )? ")" type
               | "(" type ")" ;
```

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
               | INT | FLOAT | STRING | "true" | "false"   (* valor const *)
               | type ;                      (* tipo, ou IDENT ambíguo *)
```

**Q1 — declaração vs. uso.** A spec §13/§14 mostra a forma de *uso*
(`type<"value", 1, true, Int, ...>`) na posição de *declaração*. Isso é
sintaticamente impossível de interpretar como declaração (não há nomes para os
parâmetros). A gramática acima adota parâmetros nomeados na declaração e valores
no uso:

```c
def FixedArray = type<T, N: Int> { values: T[]; };   // declaração
def a: FixedArray<Int, 3> = ...;                     // uso
```

**Q5 — ambiguidade `<`.** Em `postfix_op`, a alternativa
`generic_args "(" ...` só é aceita se, após consumir os argumentos genéricos, o
token seguinte for `(`. Caso contrário o parser faz backtrack e trata `<` como
operador relacional. Consequência conhecida: `a < b > (c)` parseia como chamada
genérica. Contornável com `(a < b) > (c)`.

**Ambiguidade `IDENT` em `generic_arg`.** `Int` e `N` casam tanto com "tipo"
quanto com "valor const". O parser produz `AmbiguousArgumentSyntax(name)` e o
checker decide pelo que o nome designa no escopo (`LAP0291`/`LAP0292` se não
bater com a posição esperada).

**Ambiguidade `fn` em `generic_arg`.** `fn(Int) Int` é um tipo; `fn(a: Int) Int
{ ... }` é um valor. Discriminador: só o valor tem corpo `{`. O parser tenta o
tipo e faz backtrack se encontrar `{` após o tipo de retorno.

---

## A.8 Construção de struct — Q2

```ebnf
field_init_list = field_init ( "," field_init )* ","? ;
field_init      = IDENT ":" expression ;
```

A spec §24 inclui `Construct` na Core AST mas nunca define a sintaxe de
superfície. Adotado: `TypeName { campo: valor, ... }`, com acesso via `.campo`.

Para evitar a ambiguidade com o `{` de `if`/`match`, a forma
`postfix_op = "{" ... "}"` é desabilitada dentro da condição de `if` e do
escrutinado de `match` (`expression_no_struct`). Para construir nessas posições,
parentetize: `if (P { a: 1 }).b { ... }`.

---

## A.9 Padrões

```ebnf
pattern        = "_"
               | path ( "(" pattern ( "," pattern )* ")" )?
               | literal_pattern ;

path           = IDENT ( "." IDENT )* ;

literal_pattern = INT | FLOAT | STRING | "true" | "false" | "-" INT | "-" FLOAT ;
```

Um `path` de um único `IDENT` sem argumentos é ambíguo (binding novo ou variante
nulária). O parser emite `IdentifierPattern`; o checker resolve pelo escopo
(Q3).

---

## A.10 Comentários

```ebnf
line_comment   = "//" ~( "\n" )* ;
block_comment  = "/*" ... "*/" ;              (* não aninha *)
```

---

## A.11 Precedência resumida

| Nível | Construção | Assoc. |
|---|---|---|
| 0 | `return` | prefixo |
| 1 | `\|\|` | esquerda |
| 2 | `&&` | esquerda |
| 3 | `==` `!=` | esquerda |
| 4 | `<` `>` `<=` `>=` | **não encadeável** |
| 5 | `+` `-` | esquerda |
| 6 | `*` `/` | esquerda |
| 7 | `-` `!` unários | prefixo |
| 8 | `()` `[]` `.` `<>()` `{}` | esquerda |

---

## A.12 Palavras reservadas

```text
def  fn  type  enum  return  true  false  if  else  match
```

`Int`, `Float`, `Bool`, `Str`, `Void`, `Result`, `IndexError`, `Option`, `print`,
`array_length` **não** são reservadas — são bindings do prelude ou nomes
resolvidos pelo checker.

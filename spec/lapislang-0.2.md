# LapisLang — Language & Research Specification

**Versão:** 0.2
**Extensão:** `.ls`
**CLI:** `lapis`
**Implementação inicial:** C#
**Paradigma:** expression-oriented, statically typed, functional-oriented
**Objetivo:** pesquisa de avaliação e partial evaluation

> Changelog 0.1 → 0.2:
> - Tipos primitivos renomeados: `int → Int`, `float → Float`, `bool → Bool`, `string → Str`, `unit → Void`.
> - Funções não retornam mais implicitamente pela última expressão do corpo: agora usam `return` explícito.
> - Dictionaries removidos da linguagem por enquanto (`KeyError`, `dict<K,V>`, dictionary literals, dictionary indexing).
> - Generics passam a aceitar valores constantes como argumentos de tipo, além de tipos (const generics), ex.: `SomeType<"value", 1, true, Int, fn(){return 1;}>`.

---

# 1. Objetivo

LapisLang é uma linguagem pequena, estaticamente tipada e orientada a expressões, criada como plataforma experimental para estudar:

* avaliação de programas;
* partial evaluation;
* constant propagation;
* constant folding;
* beta reduction;
* specialization;
* inlining;
* eliminação de código morto;
* análise de ranges;
* bounds-check elimination;
* residualização de programas;
* relação entre interpretação e compilação.

A linguagem não tem como objetivo inicial:

* possuir uma VM;
* gerar código nativo;
* possuir sistema de pacotes;
* possuir FFI;
* possuir uma biblioteca padrão extensa;
* ter garbage collector próprio;
* competir com linguagens de produção.

A primeira implementação será um **interpretador**.

Posteriormente será implementado um **partial evaluator** que trabalhará sobre a mesma representação intermediária utilizada pelo evaluator.

---

# 2. Princípio fundamental

A linguagem segue o princípio:

> Tudo que produz um valor é uma expressão.

E:

> Todo nome é introduzido através de `def`.

Portanto não existem declarações nomeadas específicas para funções, tipos ou enums.

Não existe:

```c
fn add(...) {}
```

Nem:

```c
type User {}
```

Nem:

```c
enum Color {}
```

A forma única de introduzir um nome é:

```c
def name = expression;
```

Consequentemente:

```c
def add = fn(...) ... {};
def User = type ... {};
def Color = enum ... {};
```

---

# 3. Arquivo fonte

Arquivos LapisLang possuem extensão:

```text
.ls
```

Exemplo:

```text
hello.ls
```

Execução:

```bash
lapis hello.ls
```

O próprio arquivo é o programa.

Não existe inicialmente um conceito especial de módulo ou função de entrada.

Expressões top-level são avaliadas na ordem em que aparecem.

Exemplo:

```c
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

def result = add(10, 20);

print(result);
```

Durante a execução do arquivo:

1. `add` é definido;
2. `result` é definido;
3. `print(result)` é executado.

Posteriormente poderá ser adicionado açúcar sintático para um ponto de entrada convencional.

---

# 4. `main`

A linguagem não exige `main` na versão 0.2.

Entretanto, a seguinte convenção é válida:

```c
def main = fn() Int {
    print("Hello");
    return 0;
};

main();
```

Como `main();` é uma expressão top-level, ela será executada normalmente.

Portanto, inicialmente:

```text
main
```

não possui nenhum tratamento especial pelo runtime.

Posteriormente poderá ser introduzido açúcar como:

```c
fn main() {}
```

ou:

```text
@main
```

sem alterar a semântica fundamental da linguagem.

---

# 5. Sintaxe geral

A sintaxe possui inspiração em C, mas a semântica é expression-oriented.

Exemplo completo:

```c
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

def numbers = [1, 2, 3];

def result = numbers[1];

print(result);
```

---

# 6. Literais

## Inteiros

```c
0
1
10
42
-10
```

Tipo:

```text
Int
```

## Floating point

```c
1.0
3.14
-0.5
```

Tipo:

```text
Float
```

## Boolean

```c
true
false
```

Tipo:

```text
Bool
```

## Strings

```c
"hello"
"hello world"
```

Tipo:

```text
Str
```

## Void

A ausência de valor explícito pode utilizar:

```c
()
```

Tipo:

```text
Void
```

Uma função sem `return type` explícito é implicitamente `Void`, e uma função `Void` pode usar `return;` sem expressão, ou simplesmente atingir o fim do corpo sem executar `return`.

---

# 7. Identificadores

Identificadores seguem:

```text
[A-Za-z_][A-Za-z0-9_]*
```

Exemplos:

```c
value
user
user_id
calculate
Result
```

Convenções de nomenclatura não fazem parte da semântica.

---

# 8. Bindings

A declaração de um nome utiliza:

```c
def <name> = <expression>;
```

Exemplos:

```c
def x = 10;

def name = "Gabriel";

def enabled = true;
```

Bindings são imutáveis.

Não existe atribuição na versão 0.2.

Não existe:

```c
x = 20;
```

---

# 9. Blocos

Um bloco é uma expressão.

```c
{
    def x = 10;
    def y = 20;

    x + y
}
```

O valor do bloco é o valor da última expressão:

```text
30
```

A ausência de expressão final produz:

```text
Void
```

Exemplo:

```c
{
    print("hello");
}
```

**Importante:** essa regra de "última expressão é o valor" continua válida para blocos em geral (por exemplo, o corpo de um `if`/`else` usado como expressão, ou um bloco atribuído diretamente a um `def`). Ela **não** se aplica mais ao corpo de funções — funções usam `return` explícito (veja seção 12).

---

# 10. Funções

Funções são valores de primeira classe.

A sintaxe é:

```c
fn<parameters>(arguments) return_type {
    body
}
```

Exemplo:

```c
def add = fn(a: Int, b: Int) Int {
    return a + b;
};
```

A função não possui nome próprio.

O nome pertence ao binding:

```c
def add = fn(...) ... {};
```

---

# 11. Funções como expressões

Uma função pode aparecer em qualquer lugar onde uma expressão seja válida:

```c
def increment = fn(x: Int) Int {
    return x + 1;
};
```

Ou:

```c
def values = [
    fn(x: Int) Int { return x + 1; },
    fn(x: Int) Int { return x * 2; }
];
```

Funções são closures.

Uma função pode capturar bindings do ambiente externo:

```c
def multiplier = 10;

def multiply = fn(x: Int) Int {
    return x * multiplier;
};
```

---

# 12. Retorno de função

Diferente da versão 0.1, o resultado de uma função **não** é mais determinado pela última expressão do corpo. A linguagem introduz `return` explícito:

```c
def add = fn(a: Int, b: Int) Int {
    return a + b;
};
```

Regras:

* `return expression;` encerra a execução da função imediatamente e produz `expression` como resultado.
* `return;` (sem expressão) só é válido quando o tipo de retorno da função é `Void`.
* Uma função cujo tipo de retorno não é `Void` deve garantir, estaticamente, que todo caminho de execução termine em `return expression;` — o type checker deve detectar caminhos sem `return` (ver seção 27).
* Uma função `Void` pode simplesmente atingir o fim do corpo sem `return`, produzindo `Void` implicitamente.
* `return` pode aparecer em qualquer ponto do corpo da função, inclusive dentro de blocos aninhados (`if`, `match`, blocos internos), e sempre encerra a função mais próxima que o envolve — não o bloco.

Exemplo com retorno antecipado:

```c
def abs = fn(x: Int) Int {
    if x < 0 {
        return -x;
    }

    return x;
};
```

---

# 13. Generics / Type Parameters

Funções, tipos e enums podem receber parâmetros genéricos. A partir da 0.2, um parâmetro genérico pode ser:

* um **tipo** (como antes); ou
* um **valor constante** (const generic) — string, inteiro, boolean, um tipo, ou até uma função literal conhecida em tempo de especialização.

Sintaxe:

```text
fn<P1, P2, ...>(parameters) return_type { body }
```

onde cada `Pn` pode ser um nome de tipo (`T`) ou um valor constante.

Exemplo tradicional (tipo):

```c
def identity = fn<T>(value: T) T {
    return value;
};
```

Uso:

```c
identity<Int>(10);
identity<Str>("hello");
```

Exemplo com valores constantes como argumentos de generic:

```c
def SomeType = type<"value", 1, true, Int, fn() Int { return 1; }> {
    label: Str;
};
```

Uso conceitual:

```c
SomeType<"value", 1, true, Int, fn() Int { return 1; }>
```

Nesse exemplo, os parâmetros genéricos misturam:

* `"value"` — um literal `Str` conhecido;
* `1` — um literal `Int` conhecido;
* `true` — um literal `Bool` conhecido;
* `Int` — um tipo;
* `fn() Int { return 1; }` — um valor de função conhecido em tempo de especialização.

O type checker deve validar que cada argumento genérico é compatível com a posição esperada (tipo vs. valor constante vs. tipo de valor constante esperado, quando aplicável).

A sintaxe genérica geral continua:

```text
fn<P1, P2, ...>(parameters) return_type { body }
type<P1, P2, ...> { fields }
enum<P1, P2, ...> { variants }
```

---

# 14. Tipos definidos pelo usuário

Tipos são expressões.

Sintaxe:

```c
type<P1, P2, ...> {
    fields
}
```

Um tipo recebe um nome através de `def`:

```c
def User = type {
    id: Int;
    name: Str;
};
```

Tipo genérico (parâmetro de tipo):

```c
def Box = type<T> {
    value: T;
};
```

Tipo genérico com valor constante (const generic):

```c
def FixedArray = type<T, 3> {
    values: T[];
};
```

O `type` não possui nome próprio.

O nome `User`, `Box` ou `FixedArray` é apenas o resultado do binding.

---

# 15. Enums

Enums também são expressões.

Sintaxe:

```c
enum<P1, P2, ...> {
    Variant
    Variant(Type)
    Variant(Type1, Type2)
}
```

Exemplo:

```c
def Color = enum {
    Red,
    Green,
    Blue
};
```

Enum com dados:

```c
def Result = enum<T, E> {
    Ok(T),
    Err(E)
};
```

Exemplo conceitual:

```c
Ok(10)
Err(error)
```

Enums são valores de primeira classe.

---

# 16. Result

A linguagem utilizará `Result<T, E>` para representar operações que podem falhar.

O tipo será definido pela própria linguagem:

```c
def Result = enum<T, E> {
    Ok(T),
    Err(E)
};
```

O runtime não deve possuir uma implementação semântica especial de `Result`.

Ele apenas precisa ser capaz de construir e manipular valores de enum.

---

# 17. Erros

Erros também podem ser representados por enums:

```c
def IndexError = enum {
    OutOfBounds
};
```

Esse tipo poderá fazer parte do prelude inicial da linguagem.

> Nota: `KeyError` foi removido na 0.2 junto com dictionaries (ver seção 19).

---

# 18. Arrays

Array literals:

```c
[1, 2, 3]
```

Todos os elementos devem possuir o mesmo tipo.

```c
def numbers = [1, 2, 3];
```

Tipo:

```text
Int[]
```

Array explícito:

```c
def numbers: Int[] = [1, 2, 3];
```

Arrays heterogêneos não são permitidos na versão 0.2:

```c
[1, "hello", true]
```

é inválido.

---

# 19. Dictionaries (não implementado)

Dictionaries **não fazem parte** da linguagem na versão 0.2.

Não existe:

```c
["key": value]
```

Não existe o tipo:

```text
dict<K, V>
```

Não existe `KeyError`.

Toda a semântica associada (dictionary literals, dictionary indexing, dictionary specialization no partial evaluator) fica adiada para uma versão futura, quando reintroduzida explicitamente na spec.

---

# 20. Indexadores

Arrays utilizam a sintaxe:

```c
collection[index]
```

Exemplo:

```c
def numbers = [10, 20, 30];

numbers[1];
```

---

# 21. Indexadores sempre retornam `Result`

Essa é uma decisão semântica fundamental da linguagem.

Array:

```text
T[][Int] -> Result<T, IndexError>
```

Conceitualmente:

```text
array[index]
```

retorna:

```text
Ok(value)
```

ou:

```text
Err(IndexError.OutOfBounds)
```

Portanto:

```c
def numbers = [10, 20, 30];

def result = numbers[1];
```

possui:

```text
Result<Int, IndexError>
```

e não:

```text
Int
```

---

# 22. Pattern matching

Pattern matching fará parte da Core Language apenas no nível mínimo necessário para trabalhar com enums.

Exemplo conceitual (como expressão de bloco comum):

```c
match result {
    Ok(value) => value,
    Err(error) => 0
}
```

Dentro de uma função, um `match` também pode ser usado junto com `return`:

```c
def unwrapOr = fn(result: Result<Int, IndexError>, fallback: Int) Int {
    match result {
        Ok(value) => return value,
        Err(error) => return fallback
    }
};
```

A sintaxe exata pode ser simplificada ou alterada posteriormente.

O mecanismo de `match` não precisa necessariamente existir como primitiva do evaluator final.

Ele pode ser desugared para construções mais primitivas.

---

# 23. Desugaring

A linguagem terá uma fase explícita de **syntactic desugaring**.

Objetivo:

> Transformar Surface AST em uma Core AST pequena e semanticamente uniforme.

Exemplos de construções que podem futuramente ser desugared:

```text
match
? / Result propagation
for
while
method syntax
main
syntactic sugar
```

A primeira versão deve conter apenas o mínimo necessário.

---

# 24. Core Language

A Core AST deve ser significativamente menor que a linguagem de superfície.

Uma possível estrutura:

```text
Expr
 ├── Literal
 ├── Variable
 ├── Let
 ├── Lambda
 ├── Call
 ├── Block
 ├── Return
 ├── If
 ├── Binary
 ├── Unary
 ├── Array
 ├── Index
 ├── Construct
 └── Match
```

A estrutura exata poderá mudar durante a implementação.

O objetivo é minimizar a quantidade de operações semânticas.

`Return` é um nó novo na 0.2: representa a saída explícita de uma função com um valor (ou vazio, para `Void`).

---

# 25. Type System

LapisLang será estaticamente tipada.

Tipos primitivos:

```text
Int
Float
Bool
Str
Void
```

Tipos compostos:

```text
T[]
```

Tipos definidos:

```text
type
enum
```

Tipos funcionais:

```text
fn(T1, T2) R
```

Tipos genéricos:

```text
T
```

Tipos genéricos com valor constante (const generics):

```text
"literal string"
1
true
Int
fn() Int { return 1; }
```

usados como argumentos de tipo, por exemplo em `SomeType<"value", 1, true, Int, fn() Int { return 1; }>`.

---

# 26. Type checking

Pipeline:

```text
Source
   ↓
Parser
   ↓
Surface AST
   ↓
Desugar
   ↓
Core AST
   ↓
Type Checker
   ↓
Typed Core AST
```

O type checker deverá detectar pelo menos:

* variável inexistente;
* quantidade incorreta de argumentos;
* tipos incompatíveis;
* retorno incompatível;
* caminho de execução em função não-`Void` sem `return`;
* uso de `return;` sem expressão em função não-`Void`;
* elementos heterogêneos em arrays;
* índice de array que não seja `Int`;
* aplicação inválida de função;
* uso inválido de parâmetros genéricos (tipo vs. valor constante incompatível com a posição esperada).

---

# 27. Runtime Values

O evaluator trabalhará com uma representação de valores aproximadamente:

```text
Value
 ├── Int
 ├── Float
 ├── Bool
 ├── Str
 ├── Void
 ├── Array
 ├── Closure
 ├── EnumValue
 └── TypeValue
```

A representação concreta será definida em C#.

---

# 28. Evaluator

O evaluator é a primeira implementação executável da linguagem.

Conceitualmente:

```text
evaluate : Expr × Environment → Value
```

O evaluator deve ser simples e previsível.

Ele deve tratar `Return` como um sinal de controle de fluxo que interrompe a avaliação do corpo da função corrente e produz o valor associado.

Exemplo:

```c
10 + 20
```

é simplesmente avaliado para:

```text
30
```

---

# 29. Runtime primitives

A quantidade de primitivas deve ser pequena.

Possíveis primitivas:

```text
array_get
array_length

integer operations
floating-point operations
string operations

function_call
enum construction
```

A semântica de `Result`, `Option`, etc. deve permanecer na linguagem.

O runtime fornece apenas as operações fundamentais necessárias para implementar essas abstrações.

---

# 30. Runtime error vs Language Result

Devem existir dois conceitos diferentes.

### Runtime/internal error

Erro da implementação do evaluator.

Exemplos:

```text
invalid internal AST
compiler bug
unexpected runtime state
```

### Language-level Result

Erro que faz parte da semântica do programa:

```text
IndexError
```

Portanto:

```c
numbers[100]
```

não deve gerar uma exceção interna do evaluator.

Deve produzir:

```text
Err(IndexError.OutOfBounds)
```

---

# 31. Environment

O evaluator manterá um ambiente:

```text
Environment
```

Conceitualmente:

```text
Environment {
    bindings: Map<Name, Value>
    parent: Environment?
}
```

Isso permitirá lexical scoping e closures.

---

# 32. Closures

Exemplo:

```c
def multiplier = 10;

def multiply = fn(x: Int) Int {
    return x * multiplier;
};
```

A closure deve armazenar:

```text
parameters
body
captured environment
```

---

# 33. CLI

O projeto produzirá um executável:

```text
lapis
```

Uso mínimo:

```bash
lapis program.ls
```

Comandos futuros poderão incluir:

```bash
lapis run program.ls
lapis check program.ls
lapis ast program.ls
lapis desugar program.ls
lapis eval program.ls
lapis pe program.ls
```

Na primeira versão, apenas:

```bash
lapis program.ls
```

é obrigatório.

---

# 34. Exemplo de programa

Arquivo:

```text
hello.ls
```

Conteúdo:

```c
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

def main = fn() Void {
    def result = add(10, 20);

    print(result);
};

main();
```

Execução:

```bash
lapis hello.ls
```

Saída:

```text
30
```

---

# 35. Arquitetura do projeto C#

Estrutura inicial recomendada:

```text
LapisLang/
│
├── src/
│   ├── Lapis.Cli/
│   ├── Lapis.Lexer/
│   ├── Lapis.Parser/
│   ├── Lapis.Ast/
│   ├── Lapis.Desugar/
│   ├── Lapis.TypeChecker/
│   ├── Lapis.Runtime/
│   ├── Lapis.Evaluator/
│   └── Lapis.PartialEvaluator/
│
├── tests/
│   ├── Lapis.Lexer.Tests/
│   ├── Lapis.Parser.Tests/
│   ├── Lapis.Desugar.Tests/
│   ├── Lapis.TypeChecker.Tests/
│   ├── Lapis.Evaluator.Tests/
│   └── Lapis.PartialEvaluator.Tests/
│
├── examples/
│   ├── hello.ls
│   ├── functions.ls
│   ├── arrays.ls
│   └── result.ls
│
└── LapisLang.sln
```

---

# 36. Responsabilidades dos projetos

## Lapis.Ast

Somente:

* nodes;
* expressions;
* statements/bindings;
* source locations;
* types.

Não deve conhecer evaluator.

---

## Lapis.Lexer

Responsável por:

```text
source → tokens
```

Exemplo:

```text
def add = fn(a: Int) Int { return a; };
```

vira tokens.

---

## Lapis.Parser

Responsável por:

```text
tokens → Surface AST
```

Não deve executar código.

---

## Lapis.Desugar

Responsável por:

```text
Surface AST → Core AST
```

Não deve executar código.

---

## Lapis.TypeChecker

Responsável por:

```text
Core AST → Typed Core AST
```

ou erro de tipo.

Inclui a verificação de que toda função não-`Void` possui `return` em todos os caminhos de execução.

---

## Lapis.Runtime

Responsável pelos valores:

```text
Value
Environment
Closure
Array
EnumValue
```

e primitivas.

---

## Lapis.Evaluator

Responsável por:

```text
Typed Core AST → Value
```

Esse será o **semantic reference implementation** da linguagem, incluindo o tratamento de `Return` como controle de fluxo.

---

## Lapis.PartialEvaluator

Responsável futuramente por:

```text
Core AST + Static Environment
            ↓
      Residual Core AST
```

---

# 37. Partial Evaluator

O partial evaluator não deve interpretar um programa inteiro simplesmente.

Ele deve separar:

```text
Static
```

de:

```text
Dynamic
```

Exemplo:

```c
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

add(10, x);
```

Se:

```text
10
```

é conhecido, mas:

```text
x
```

é desconhecido, o partial evaluator deve produzir algo equivalente a:

```c
fn(x: Int) Int {
    return 10 + x;
}
```

ou uma forma equivalente na Core AST.

---

# 38. Princípio de residualização

Quando o evaluator consegue determinar o resultado:

```text
evaluate
```

pode ser usado durante partial evaluation.

Quando não consegue:

```text
residualize
```

a operação.

Exemplo:

```text
10 + 20
```

→

```text
30
```

Enquanto:

```text
x + 20
```

→

```text
x + 20
```

---

# 39. Static Environment

O partial evaluator possuirá:

```text
StaticEnvironment
```

Exemplo:

```text
{
    x → Int(10),
    add → Closure(...)
}
```

A implementação deve permitir futuramente representar estados como:

```text
Known(Value)
Unknown(Type)
```

ou equivalente.

---

# 40. Relação entre evaluator e partial evaluator

O evaluator será a referência semântica.

O objetivo será demonstrar:

```text
evaluate(program)
```

e:

```text
evaluate(
    partialEvaluate(program, staticEnvironment)
)
```

produzem resultados equivalentes quando aplicável.

Essa propriedade será utilizada extensivamente nos testes.

---

# 41. Bounds checking

Arrays devem ser seguros por padrão.

A operação:

```c
array[index]
```

deve verificar:

```text
0 <= index < length(array)
```

Se válido:

```text
Ok(value)
```

Se inválido:

```text
Err(IndexError.OutOfBounds)
```

O evaluator sempre realiza a verificação.

O partial evaluator poderá futuramente provar que o acesso é seguro e eliminar o check.

---

# 42. Bounds-check elimination

Essa será uma das otimizações experimentais futuras.

Exemplo:

```c
def values = [10, 20, 30];

def x = values[1];
```

O partial evaluator pode determinar:

```text
length(values) = 3
index = 1
```

Logo:

```text
0 <= 1 < 3
```

e residualizar diretamente:

```c
def x = Ok(20);
```

Em uma etapa posterior, caso o sistema de tipos/representação permita, o `Ok` também poderá ser simplificado.

---

# 43. Estratégia de implementação

A implementação será incremental.

## Etapa 1 — Skeleton

Criar:

```text
LapisLang.sln
Lapis.Cli
Lapis.Ast
Lapis.Lexer
Lapis.Parser
Lapis.Runtime
Lapis.Evaluator
```

Fazer:

```bash
lapis hello.ls
```

funcionar.

---

# 44. Etapa 2 — Lexer

Implementar tokens:

```text
identifier
integer
float
string

def
fn
type
enum
return
true
false
if
else
match

(
)
{
}
[
]

:
,
;
=

+
-
*
/
==
!=
<
>
<=
>=
```

Adicionar source location:

```text
line
column
offset
```

desde o início.

---

# 45. Etapa 3 — Parser

Implementar primeiro:

```text
literals
identifiers
def
blocks
binary expressions
function expressions
function calls
return
```

Depois:

```text
arrays
indexing
type
enum
generics (tipos e const generics)
```

Testes devem ser predominantemente AST snapshots.

---

# 46. Etapa 4 — Evaluator mínimo

Primeiro:

```text
literal
variable
def
block
binary operations
```

Depois:

```text
lambda
closure
call
return (controle de fluxo)
```

Depois:

```text
array
index
enum
```

---

# 47. Etapa 5 — Type checker

Adicionar:

```text
primitive types (Int, Float, Bool, Str, Void)
function types
array types
enum types
generic parameters (tipo e const generic)
verificação de return em todos os caminhos
```

Não implementar inferência sofisticada inicialmente.

O objetivo é ter um type checker simples e previsível.

---

# 48. Etapa 6 — Desugar

Criar a primeira versão do Core AST.

Por exemplo:

```c
def x = 10;
```

pode ser convertido para uma construção:

```text
Let(
    x,
    Literal(10),
    ...
)
```

E:

```c
return a + b;
```

pode ser convertido para:

```text
Return(
    Binary(+, a, b)
)
```

A partir desse ponto, evaluator e partial evaluator trabalharão preferencialmente sobre a Core AST.

---

# 49. Etapa 7 — Result

Implementar na linguagem:

```c
def Result = enum<T, E> {
    Ok(T),
    Err(E)
};

def IndexError = enum {
    OutOfBounds
};
```

A implementação do evaluator deverá ser capaz de construir esses valores.

---

# 50. Etapa 8 — Indexing

Implementar:

```text
Array[index]
```

retornando `Result`.

Testes:

```text
[1,2,3][0] → Ok(1)
[1,2,3][2] → Ok(3)
[1,2,3][3] → Err(OutOfBounds)
```

---

# 51. Etapa 9 — Testes de semântica

Criar testes de execução completos.

Exemplo:

```text
source
    ↓
lexer
    ↓
parser
    ↓
desugar
    ↓
type checker
    ↓
evaluator
```

Resultado esperado:

```text
Value
```

Esses testes serão os testes de integração fundamentais, incluindo casos de `return` antecipado e caminhos sem `return`.

---

# 52. Etapa 10 — Partial evaluator

Somente depois que o evaluator estiver estável.

Primeiro implementar:

```text
literal reduction
constant folding
variable propagation
```

Depois:

```text
beta reduction
closure specialization
function specialization
```

Depois:

```text
conditional specialization
dead branch elimination
array specialization
bounds-check elimination
```

---

# 53. Testes do Partial Evaluator

Cada teste deve comparar:

```text
program
```

com:

```text
residualProgram
```

e depois executar ambos.

Exemplo:

```text
original:
    10 + 20

residual:
    30
```

Verificar:

```text
evaluate(original) == evaluate(residual)
```

---

# 54. Testes de equivalência

Esse será um dos conjuntos mais importantes.

Para cada programa:

```text
P
```

e ambiente estático:

```text
S
```

calcular:

```text
R = partialEvaluate(P, S)
```

Depois verificar:

```text
evaluate(P, S) == evaluate(R, S)
```

Quando ambos forem definidos.

Isso permite testar o partial evaluator como uma transformação semântica.

---

# 55. Testes de propriedades

Posteriormente podem ser utilizados property-based tests.

Propriedade principal:

```text
PE(P, S) ≡ P
```

em termos de comportamento observável.

Isso é mais importante do que simplesmente testar exemplos individuais.

---

# 56. CLI de pesquisa

Depois da primeira implementação:

```bash
lapis program.ls
```

Posteriormente:

```bash
lapis ast program.ls
```

Mostra:

```text
Surface AST
```

E:

```bash
lapis desugar program.ls
```

mostra:

```text
Core AST
```

Depois:

```bash
lapis pe program.ls
```

mostra o programa residualizado.

E eventualmente:

```bash
lapis pe --trace program.ls
```

poderá mostrar:

```text
[known] x = 10
[reduce] 10 + 20 -> 30
[residualize] x + y
[eliminate] bounds check
```

Isso será extremamente útil para pesquisa.

---

# 57. O que deliberadamente NÃO implementar inicialmente

A versão 0.2 não terá:

```text
classes
interfaces
inheritance
methods
exceptions
async/await
generators
modules
packages
macros
reflection
FFI
ownership
borrow checker
GC próprio
native compiler
JIT
LLVM
SSA
optimizer complexo
dictionaries
```

Também não haverá açúcar sintático desnecessário.

---

# 58. Filosofia da implementação

A implementação deve seguir três princípios.

### 1. Semântica antes de otimização

Primeiro:

```text
"Como o programa funciona?"
```

Depois:

```text
"Como podemos avaliá-lo parcialmente?"
```

Só depois:

```text
"Como podemos gerar código melhor?"
```

### 2. Runtime mínimo

Sempre que uma funcionalidade puder ser expressa pela própria linguagem, ela deve ser.

### 3. Evaluator como referência

O evaluator deve ser simples o suficiente para ser considerado a implementação de referência da semântica.

O partial evaluator não deve possuir uma semântica independente.

---

# 59. Arquitetura final

A arquitetura conceitual da LapisLang será:

```text
                       ┌─────────────┐
                       │   .ls File  │
                       └──────┬──────┘
                              │
                              ▼
                       ┌─────────────┐
                       │    Lexer    │
                       └──────┬──────┘
                              │
                              ▼
                       ┌─────────────┐
                       │   Parser    │
                       └──────┬──────┘
                              │
                              ▼
                       ┌─────────────┐
                       │ Surface AST │
                       └──────┬──────┘
                              │
                              ▼
                       ┌─────────────┐
                       │  Desugar    │
                       └──────┬──────┘
                              │
                              ▼
                       ┌─────────────┐
                       │  Core AST   │
                       └──────┬──────┘
                              │
                    ┌─────────┴─────────┐
                    │                   │
                    ▼                   ▼
             ┌─────────────┐    ┌────────────────┐
             │Type Checker  │    │Partial         │
             └──────┬──────┘    │Evaluator       │
                    │           └───────┬────────┘
                    ▼                   │
             ┌─────────────┐            ▼
             │ Typed Core  │     Residual Core AST
             └──────┬──────┘
                    │
                    ▼
             ┌─────────────┐
             │  Evaluator  │
             └──────┬──────┘
                    │
                    ▼
                 Runtime
                  Value
```

---

# 60. Primeiro marco do projeto

O primeiro milestone não será partial evaluation.

Será:

```text
lapis hello.ls
```

executando corretamente:

```c
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

def main = fn() Void {
    print(add(10, 20));
};

main();
```

com a cadeia:

```text
Lexer
→ Parser
→ AST
→ Desugar
→ Type Checker
→ Evaluator
```

funcionando de ponta a ponta.

Depois disso, a linguagem já terá uma semântica executável e poderemos começar a pesquisa.

---

# 61. Primeiro objetivo científico

Uma vez que o evaluator esteja funcionando, a primeira pergunta experimental da LapisLang será:

> Quanto de um programa pode ser executado antecipadamente quando parte de seus valores é conhecida?

A partir daí, o projeto pode evoluir empiricamente:

```text
Evaluator
     ↓
Constant Folding
     ↓
Partial Evaluation
     ↓
Specialization
     ↓
Static Analysis
     ↓
Optimization
```

Sem precisar aumentar significativamente a linguagem.

---

# 62. Resumo da LapisLang 0.2

A linguagem pode ser resumida em poucas regras:

```text
1. Tudo que produz valor é expressão.

2. Nomes são introduzidos por:
       def name = expression;

3. Funções são expressões:
       fn<P1, P2, ...>(args) ReturnType { body }

4. Funções retornam via `return` explícito, não pela
   última expressão do corpo:
       return expression;
       return; // apenas se ReturnType == Void

5. Tipos são expressões:
       type<P1, P2, ...> { fields }

6. Enums são expressões:
       enum<P1, P2, ...> { variants }

7. Funções, tipos e enums são valores/entidades de primeira classe
   dentro das limitações do type system.

8. Arrays são expressões:
       [a, b, c]

9. Indexação sempre pode falhar:
       array[index] -> Result<T, IndexError>

10. Dictionaries não fazem parte da linguagem por enquanto.

11. Generics aceitam tanto tipos quanto valores constantes
    (const generics):
       SomeType<"value", 1, true, Int, fn(){ return 1; }>

12. O arquivo .ls é o programa.

13. Expressões top-level são avaliadas sequencialmente.

14. Não existe main especial na v0.2.

15. O evaluator é a referência semântica.

16. Desugaring reduz a linguagem de superfície a uma Core AST.

17. Partial evaluation será implementado posteriormente sobre a Core AST.

18. O runtime deve ser mínimo.

19. Otimizações devem ser consequência da análise/partial evaluation,
    e não requisitos da primeira implementação.
```

Essa especificação mantém a LapisLang **pequena o suficiente para ser implementada rapidamente em C#**, mas já contém exatamente os elementos necessários para tornar a pesquisa interessante: funções de primeira classe com `return` explícito, closures, generics (incluindo const generics), tipos e enums como construções da linguagem, coleções, operações potencialmente falíveis e uma fronteira explícita entre **interpretação e partial evaluation**.

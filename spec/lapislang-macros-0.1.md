# LapisLang — Macros, Reflection e Controle de Fluxo

**Versão:** 0.1 (proposta) · **Requer:** [`lapislang-0.2.md`](lapislang-0.2.md) ≥ 0.2.3

> Esta spec estende a LapisLang 0.2 com três coisas que se sustentam mutuamente:
> **macros** (sintaxe → sintaxe), **reflection** (programa → metadados) e
> **`goto`/`label`** (a primitiva de controle de fluxo sobre a qual as macros de
> controle são construídas).
>
> O objetivo é manter o núcleo pequeno permitindo que construções de alto nível
> sejam definidas pela própria linguagem — o princípio §58.2 ("runtime mínimo")
> aplicado à *sintaxe*.
>
> **Estado:** Q19–Q22 e Q24 decididas pelo autor. `@if` e `@match` ficaram **fora do
> escopo**: `if` e `match` continuam no compilador até a linguagem ter uma
> construção própria para ler a carga de uma variante com segurança (Q23).

---

## Changelog em relação à proposta original

Esta spec nasceu de um documento escrito fora do contexto do projeto. As mudanças
abaixo são adaptações necessárias, não preferências de estilo — cada uma corrige
um conflito real com a 0.2 já implementada.

| # | Mudança | Motivo |
|---|---|---|
| 1 | `emit` → **`expand`** | pedido do autor |
| 2 | Reflection também em **runtime**, somente leitura | pedido do autor; a proposta original a restringia a compile time |
| 3 | `macro nome ...` **mantido** | decisão do autor (Q19): macro não é first-class citizen, então não passa por `def` |
| 4 | `$end` → **`end`** | `$` não é lexável: identificadores são `[A-Za-z_][A-Za-z0-9_]*` (§7) |
| 5 | `String:path` → **`Str:path`** | o primitivo se chama `Str` desde a 0.2 |
| 6 | `if (cond) { }` → **`if cond { }`** | a 0.2 não usa parênteses na condição |
| 7 | `routes.contains(...)`/`routes.add(...)` → **primitivas de contexto** | não há method syntax nem mutação na linguagem (§8, §23) |
| 8 | Regras de macro: ordenadas → **todas testadas, exatamente uma deve casar** | a proposta pedia ordem *e* detecção de ambiguidade, o que é contraditório |
| 9 | `goto`/`label` ganham semântica de **join point**, não de salto arbitrário | a Core é orientada a expressões e não tem nó `Block` (Q10) |
| 10 | `@if` e `@match` **fora do escopo**; `if`/`match` continuam no compilador | ler a carga de uma variante com segurança é problema de linguagem, não de macro (Q22, Q23) |
| 11 | `goto` pode saltar **para trás**, mas nunca para fora do próprio escopo | decisão do autor; traz laços e, com eles, o fim da garantia de terminação |
| 12 | `@while` e `@unless` viram macros do prelude | são construções que a linguagem **não** tem; nada é retirado do compilador |
| 13 | Fases de implementação reescritas | a proposta mandava construir a Surface AST, que já existe desde o M1 |

---

# 1. Onde isto entra no pipeline

A 0.2 tem:

```text
.ls → Lexer → Parser → Surface AST → Desugar → Core AST → TypeChecker → Typed Core → Evaluator
```

Macros entram **entre o parser e o desugar**, operando sobre a Surface AST:

```text
.ls → Lexer → Parser → Surface AST → Macro Expansion → Expanded AST → Desugar → Core AST → ...
                                            │
                                            ├── match       (sintaxe → bindings)
                                            ├── constraint  (validação em compile time)
                                            ├── reflection  (metadados das declarações)
                                            └── expand      (bindings → sintaxe)
```

A Expanded AST é **da mesma classe** que a Surface AST: expansão é um endomorfismo
`Surface → Surface`. Não há um terceiro conjunto de nós.

O evaluator nunca vê macros. O type checker também não: quando ele roda, toda
macro já virou sintaxe comum.

---

# 2. Princípio fundamental

```text
Macro = Sintaxe → Sintaxe
```

Uma macro reconhece uma estrutura sintática, captura partes dela, valida a
construção em tempo de compilação e produz nova sintaxe.

Isto **não substitui** o type checker nem o partial evaluator. A separação de
responsabilidades da 0.2 §36 continua valendo, com uma fase a mais:

```text
Macro:              Sintaxe → Sintaxe
Desugar:            Surface AST → Core AST
Type Checker:       Core AST → Typed Core AST
Partial Evaluator:  Core AST × valores estáticos → Core AST residual
Evaluator:          Typed Core AST → Value
```

---

# 3. Declaração de macro

Uma macro é uma **declaração nomeada**, e não uma expressão ligada por `def`:

```c
macro unless
    match Expression:condition Block:body
    expand {
        goto done if condition;
        body;
        label done;
    };
```

Forma geral:

```text
macro <nome>
    match <padrão>
    [constraint { <statements> }]
    expand { <sintaxe> }
    [ match ... [constraint ...] expand { ... } ]*
    ;
```

Uma macro define uma ou mais **regras**. Cada regra é um trio
`match` / `constraint`? / `expand`.

> **✅ Decisão Q19 — do autor.** Esta é a única exceção ao princípio §2 da 0.2
> ("todo nome é introduzido através de `def`"), e ela é justificada: `def` liga
> **valores**, e uma macro **não é first-class citizen**. Não existe em runtime,
> não pode ser passada como argumento, não pode ser devolvida por uma função.
>
> Forçá-la a passar por `def` exigiria um `MacroType` que só existe para proibir
> tudo o que `def` normalmente permite — a uniformidade seria aparente e a
> assimetria, real. `macro nome` diz a verdade sobre o que a coisa é.
>
> Macros ocupam, portanto, um **espaço de nomes próprio**: `macro log` e
> `def log = fn ...` convivem sem colidir, porque `@log` e `log` nunca se
> confundem.

## 3.1 Escopo e visibilidade

Macros são visíveis **do ponto da declaração em diante**, no arquivo onde foram
declaradas — a mesma regra de `def` (§8, Q8). Uma macro não pode se invocar, nem
direta nem indiretamente: não há recursão na 0.2, e isso vale para a expansão.

Como macros vivem num espaço de nomes separado, `@log` nunca é confundido com um
`log` de valor, e sombrear um não afeta o outro.

A expansão é aplicada repetidamente até não restar invocação (uma macro pode
expandir para código que usa outras macros), com limite de profundidade de **64**;
ultrapassá-lo é `LAP0505`.

---

# 4. Invocação

Toda invocação começa com `@`:

```c
@unless x > 0 {
    print("negativo");
}
```

O `@` separa invocação de macro de chamada de função sem ambiguidade alguma:

```c
add(1, 2);      // chamada
@add 1 2;       // macro
```

Macros **não exigem parênteses**. O que é consumido após o nome é determinado
pelo `match` da própria macro.

## 4.1 Até onde vai um argumento

Uma captura `Expression:e` consome uma expressão usando a **gramática de
expressão normal**, que já para nos lugares certos:

```c
def v = @square x + 1;      // captura `x + 1` — `+` continua a expressão
f(@square x, y);            // captura `x` — `,` encerra
@unless x > 0 { ... }       // captura `x > 0` — `{` não é operador
```

O último caso funciona por causa de **Q2**: como a construção de `type` leva ponto
inicial (`.Point { }`), um `{` depois de uma expressão completa nunca é
continuação dela. A mesma decisão que fez `if p { }` funcionar faz
`@unless x > 0 { }` funcionar, sem regra contextual nova.

---

# 5. Padrões (`match`)

Um padrão é uma sequência de **capturas** e **literais sintáticos**.

```c
macro foreach
    match Identifier:item in Expression:collection Block:body
    expand { ... };
```

Aqui `in` é literal: pertence à sintaxe *desta macro* e não vira palavra reservada
da linguagem. É o que permite a uma biblioteca definir construções próprias sem
tocar no lexer.

```c
@foreach user in users { ... }     // casa
@foreach user from users { ... }   // não casa
```

## 5.1 Capturas

```text
<Categoria>:<nome>
```

As categorias da primeira versão:

| Categoria | Captura | Nó da Surface AST |
|---|---|---|
| `Expression` | qualquer expressão | `Expression` |
| `Statement` | um statement | `Statement` |
| `Block` | `{ ... }` | `BlockExpression` |
| `Type` | uma anotação de tipo | `TypeSyntax` |
| `Identifier` | um identificador nu | `IdentifierExpression` |
| `Literal` | qualquer literal | `IntLiteral` \| `FloatLiteral` \| `StrLiteral` \| `BoolLiteral` |
| `Int` `Float` `Str` `Bool` | um literal daquele tipo | o literal correspondente |

Uma categoria `Pattern` — para `Enum.Variante`, literal ou `_` — chegaria junto com
`@match`, que está fora do escopo (§12.3). §7 já prevê categorias adicionais.

As capturas de literal tipado (`Str:path`) usam os nomes dos primitivos da 0.2 —
`Str`, não `String`.

**Capturas guardam AST, nunca valores avaliados.** `Expression:e` casando com
`f()` captura a árvore da chamada; nada é executado.

## 5.2 Repetição

```text
<Categoria>:<nome>*        separado por <token>
( <sub-padrão> )*          separado por <token>
```

A primeira forma repete **uma** captura; a segunda repete um **grupo**, quando o
item repetido é composto:

```c
match { (Identifier:campo Type:tipo)* separado por , }
```

Cada captura do grupo liga uma **lista**, e as listas são paralelas: `campo[i]`
acompanha `tipo[i]`. No `expand`, `campo...` e `tipo...` expandem em paralelo.

> A repetição de grupo é o que evita inventar categorias sintáticas sob medida para
> cada macro. Categorias pertencem ao sistema e servem a todas; privilegiar uma
> construção contrariaria §9, que diz que macros definem sua própria sintaxe.

---

# 6. `expand`

`expand` produz sintaxe. A saída é AST, nunca texto.

```c
macro square
    match Expression:e
    expand { (e * e) };
```

```c
def value = @square x + 1;      //  ((x + 1) * (x + 1))
```

Note os parênteses no `expand`: sem eles a expansão de `x + 1` produziria
`x + 1 * x + 1`. **A macro é responsável pela sua própria precedência** — o
expansor não parenteza nada automaticamente, porque a árvore capturada é inserida
como árvore, e o `expand` também é uma árvore. Na prática o problema desaparece:
`(e * e)` já é a árvore certa. O aviso vale para quem escreve `expand` pensando em
texto.

## 6.1 Contexto de expansão

O resultado precisa ser válido no contexto da invocação:

| Invocada como | `expand` deve produzir |
|---|---|
| expressão | uma expressão |
| statement | um ou mais statements |

Um `expand` com vários statements usado em posição de expressão é `LAP0506`.
A validação é **sintática**; tipos continuam sendo problema do type checker.

---

# 7. Resolução de regras

Todas as regras de uma macro são testadas. O resultado deve ser **exatamente uma**:

| Regras que casam | Resultado |
|---|---|
| 0 | `LAP0501` — nenhum padrão de `@nome` corresponde |
| 1 | expande |
| 2+ | `LAP0502` — padrões ambíguos |

> **Mudança em relação à proposta.** O documento original dizia que as regras são
> avaliadas na ordem declarada *e* que ambiguidade deve ser detectada. As duas
> coisas não convivem: detectar ambiguidade obriga a testar todas as regras, e aí
> a ordem não decide nada. Escolhemos ambiguidade explícita — é a opção que falha
> ruidosamente, coerente com a 0.2, que rejeita `a < b < c` em vez de adivinhar.

## 7.1 Falha de `match` ≠ falha de `constraint`

| | Significado | Efeito |
|---|---|---|
| `match` falha | "esta regra não se aplica" | tenta as outras |
| `constraint` falha | "esta construção é inválida" | erro de compilação, ponto final |

Uma `constraint` nunca faz o mecanismo tentar outra regra. É o que distingue
"não é esta a forma" de "esta forma está errada".

---

# 8. `constraint` e compile time

`constraint` roda **depois** do `match` e **antes** do `expand`:

```text
Invocação → match → bindings → constraint → expand → Sintaxe
```

## 8.1 Que linguagem roda dentro de `constraint`

**A própria LapisLang**, avaliada pelo evaluator da 0.2 (plano 08). Não há uma
segunda linguagem nem uma segunda semântica — princípio §58.3, "o evaluator é a
referência". O que muda é o **ambiente**:

| | Runtime | Compile time |
|---|---|---|
| Evaluator | o mesmo | o mesmo |
| Nativas | `print` | `print`, `throw`, contexto, `reflect` |
| Valores visíveis | o programa | os bindings do `constraint` + o contexto |

Uma `constraint` **não executa código da aplicação**: os bindings capturados são
árvores, não valores, e as funções do programa não estão no escopo dela.

## 8.2 `throw`

```c
constraint {
    if contextHas(key) {
        throw "rota POST já registrada: " + path;
    }
}
```

`throw e` interrompe a compilação com a mensagem `e` (um `Str`), no span da
invocação da macro. Seu tipo é **`Never`** — cabe em qualquer posição, exatamente
como `return` (Q13).

`throw` é **exclusivo de compile time**: fora de um `constraint` é `LAP0507`. A
0.2 não tem exceções de runtime, e Q9 tornou a divisão total justamente para
eliminar caminhos de aborto; introduzi-los pela porta dos fundos seria um
retrocesso. Exceções de runtime, se um dia existirem, serão uma feature
independente.

## 8.3 Estado de compilação

Frameworks precisam acumular informação durante a compilação — a tabela de rotas
de `@post` é o exemplo canônico. Como a linguagem não tem mutação nem method
syntax, o estado vive num **contexto de compilação** acessível por primitivas:

```text
contextHas(key: Str) Bool
contextGet(key: Str) Result<Str, ContextError>
contextPut(key: Str, value: Str) Void
contextKeys(prefix: Str) Str[]
```

```c
macro post
    match Str:path Block:handler

    constraint {
        def key = "route.POST." + path;

        if contextHas(key) {
            throw "rota POST já registrada: " + path;
        }

        contextPut(key, path);
    }

    expand { ... };
```

O contexto é a **única** coisa mutável do sistema, e só durante a compilação. Não
é um recurso da linguagem: é estado do compilador, exposto por nativas, do mesmo
jeito que `print` expõe I/O.

> **Escopo da v1.** Chaves e valores são `Str`. Estruturas ricas no contexto
> ficam para depois; `Str` cobre os casos reais (registro, deduplicação,
> contagem) sem inventar um sistema de serialização.

---

# 9. Higiene

Um identificador **introduzido** por uma macro não pode capturar nem ser capturado
por identificadores do programa:

```c
macro example
    match Block:body
    expand {
        def temp = 10;
        body;
    };
```

```c
def temp = "meu";
@example { print(temp); }     // imprime "meu", não 10
```

Um identificador **capturado** do programa mantém seu contexto léxico original:

```c
macro example
    match Identifier:x
    expand { print(x); };

def value = 10;
@example value;               // o `value` expandido é o `value` do programa
```

A implementação usa **marcas de expansão**: cada expansão recebe um número, e todo
identificador introduzido pelo `expand` (não vindo de captura) é renomeado para
`nome@marca`. A marca não é escrevível em código-fonte, então colisão é impossível.

Isso reaproveita o que já existe: o desugar já gera nomes sintéticos que o
`FindSuggestion` filtra, e `LAP0202` (redefinição no mesmo bloco) já é detectado no
desugar — depois da expansão, portanto sobre nomes já higienizados.

---

# 10. `goto` e `label`

A primitiva de controle de fluxo sobre a qual `@unless` e `@while` são construídos
(§12) — e a forma única que a análise de fluxo do partial evaluator precisa
entender.

## 10.1 Sintaxe

```c
goto nome;              // salto incondicional
goto nome if cond;      // salto condicional
label nome;             // destino
```

`goto ... if ...` é **uma primitiva só**, não um `if` pós-fixo disponível em outros
lugares. Ela é primitiva de propósito: uma macro de controle construída sobre `goto`
não pode depender do `if` da linguagem, ou a construção seria circular.

Rótulos vivem num espaço de nomes próprio — um `label x` e um `def x` não colidem.

## 10.2 Regra: o salto não sai do próprio escopo

Um `goto` pode nomear um `label` **em qualquer direção** — para frente ou para trás
—, desde que o rótulo esteja no **mesmo bloco ou num bloco que o contenha, dentro
da mesma função**. Sair do escopo é `LAP0521`; rótulo inexistente é `LAP0520`.

```c
def f = fn() Void {
    label topo;
    goto topo if cond;      // ok: para trás, mesmo escopo
};

def g = fn() Void {
    goto topo;              // LAP0521: `topo` é de outra função
};
```

Um `label` é local à função, como `return`. Não há salto entre funções, entre
closures, nem para dentro de um bloco que ainda não foi aberto — entrar no meio de
um escopo pularia as declarações que ele introduz, e não haveria como dar sentido
aos nomes lá dentro.

### O que o salto para trás custa

Salto para trás é o que permite `@while`, e é também **o fim da garantia de
terminação** da 0.2. Até aqui a linguagem não tinha recursão (Q8) nem laços, então
todo programa terminava por construção. Com `goto` para trás, não termina mais:

```c
label sempre;
goto sempre;
```

Três consequências, todas assumidas de propósito:

1. **O evaluator ganha um limite de saltos** (`LAP0303`), do mesmo tipo que o
   limite de profundidade de chamada que já existe. Um programa que não termina
   aborta com diagnóstico em vez de travar.
2. **O partial evaluator passa a enfrentar laços.** Especializar um laço exige
   *widening* ou combustível, e é problema genuinamente mais difícil que
   especializar `If` — os planos 13 e 14 registram isso.
3. **`Never` deixa de significar "não retorna".** Um `goto` para trás pode
   divergir sem sair da função; o tipo continua `Never`, mas a leitura passa a ser
   "não continua daqui", que é o que sempre foi de fato.

## 10.3 Semântica: join points, não saltos

A Core não tem nó `Block` (Q10) — um bloco é uma cadeia de `Let`. Um salto
arbitrário não faria sentido nela. `goto`/`label` são, portanto, **join points**:

```c
goto done if c;
a();
label done;
b();
```

vira, na Core:

```text
Labeled(
    entry: Let(_, GotoIf(done, c), Let(_, a(), Goto(done))),
    joins: [ done → Let(_, b(), ()) ]
)
```

Um `label` **encerra** o segmento corrente com um salto implícito para ele mesmo, e
abre um novo segmento. O resultado é decomposição em blocos básicos, feita pelo
desugar, sem que a Core precise de um nó de sequência.

O `Labeled` é aberto no ponto do **primeiro** `goto` do bloco, de modo que tudo
declarado antes continua em escopo nos dois lados:

```c
def x = 1;
goto done if c;
def y = 2;          // não visível em `done` — o salto a pula
label done;
print(x);           // visível: `x` foi declarado antes do Labeled
```

Um nome declarado entre o `goto` e o `label` **não** está em escopo no destino, o
que é a leitura correta: o salto pode tê-lo pulado. Usá-lo lá é `LAP0201`.

## 10.4 Tipos

| Nó | Tipo |
|---|---|
| `Goto` | `Never` — diverge, como `return` (Q13) |
| `GotoIf` | `Void` — pode não saltar |
| `Labeled` | junção do tipo da entrada com o de todos os joins |

Com saltos para trás, os joins de um mesmo grupo podem se referenciar mutuamente. A
junção é calculada sobre o conjunto todo, não em ordem — um join que só diverge
contribui `Never` e não atrapalha.

Na prática a junção nunca falha: a entrada e todo join que não é o último terminam
em salto, e salto é `Never`. O valor do grupo é o do **último** join — o único
caminho que produz valor. A junção fica no checker mesmo assim, porque é a operação
correta, e porque deixaria de ser trivial no dia em que um join tiver parâmetros
(§10.6).

`Never` já se propaga por `Let`, `If`, `Binary`, `Unary` e `Call` desde o M1, então
`goto` cabe em qualquer posição sem regra nova.

## 10.5 Execução

O evaluator representa `return` como **completion record**, não como exceção
(plano 08). `goto` entra no mesmo mecanismo: `Completion` ganha o caso
`Goto(rótulo)`. Um `Labeled` que define aquele rótulo captura a completion e segue
pelo corpo do join — exatamente como a fronteira de chamada captura `Return`.

O laço que consome as completions é **iteração**, não recursão: um salto para trás é
mais uma volta do `while` em C#, e a pilha do interpretador não cresce. O que
termina a execução no pior caso é o orçamento de saltos (`LAP0303`).

## 10.6 O que dá progresso ao laço: `var` (Q25)

Salto para trás sozinho **não** produz laço com progresso. O corpo de um join é
avaliado no ambiente **do grupo**, o mesmo em toda volta; com bindings imutáveis
nada mudava entre iterações, então a condição de saída também não mudava — o laço
ou não rodava, ou rodava até o orçamento acabar.

A peça que faltava é a mutação, decidida na Q25:

```c
var i = 0;
label repete;
i = i + 1;
print(i);
goto repete if i < 3;   // 1, 2, 3
```

`var` declara um binding reatribuível; `x = e;` é a atribuição, **statement** e não
expressão. Um `var` declarado **antes** do rótulo é o slot que atravessa as voltas.

**Um `var` não atravessa fronteira de função** (`LAP0207`): nenhuma closure captura
`var`. É o que dispensa escolher entre captura por valor e por referência, e o que
mantém a closure como (código, ambiente imutável) para o partial evaluator.

**Escopo entre joins:** o que é declarado depois de um `label` vive dentro daquele
join e não é visível no próximo — a mesma regra que vale entre o `goto` e o
`label`. Na prática, todo `var` que o laço usa é declarado antes do primeiro
rótulo do bloco. É a restrição que *join com parâmetros* (`label L(x: Int)` /
`goto L(x + 1)`) resolveria, e ela segue como evolução possível: não conflita com
`var` e daria ao partial evaluator um grafo em forma canônica.

---

# 11. Reflection

```text
Reflection = Informação do programa → Metadados
```

**Somente leitura.** Reflection lê; quem transforma sintaxe é macro. Ela não
altera AST, não registra símbolos, não altera tipos e não altera bindings.

## 11.1 Disponível nas duas fases

> **Mudança em relação à proposta**, a pedido do autor. O documento original dizia
> "reflection não precisa existir no runtime". Aqui ela existe nas duas fases.

| Fase | Onde | Fidelidade |
|---|---|---|
| Compile time | dentro de `constraint` | metadados **sintáticos**: nomes, aridade, tipos como escritos |
| Runtime | qualquer expressão | metadados **resolvidos**: tipos já checados |

A diferença de fidelidade é honesta e inevitável: durante a expansão o type checker
ainda não rodou, então `fields[0].type` é o tipo *como escrito*, não o tipo
resolvido. Para o que a expansão precisa — nomes de variantes, aridade de carga —
a informação sintática basta.

## 11.2 Os valores são valores comuns

`reflect` devolve uma instância de `TypeInfo`, um `type` declarado no
**prelude, na própria linguagem**:

```c
def TypeKind = enum { Struct, Enum };

def FieldInfo = type {
    name: Str;
    typeName: Str;
};

def VariantInfo = type {
    name: Str;
    arity: Int;
    payloadTypeNames: Str[];
};

def TypeInfo = type {
    name: Str;
    kind: TypeKind;
    typeParameterNames: Str[];
    fields: FieldInfo[];
    variants: VariantInfo[];
};
```

Isso é o princípio §58.2 aplicado: nada de um sistema de metadados paralelo.
Valores de reflection são structs e arrays comuns e, portanto, **imutáveis por
construção** — a 0.2 não tem mutação (§8). A exigência do autor de que "valores
são constantes não modificáveis" não precisa de regra própria: ela já vale para
tudo na linguagem.

Consequência boa para a pesquisa: `reflect(User)` sobre um tipo conhecido é
inteiramente estático, então o **partial evaluator dobra a chamada inteira** e o
programa residual não paga nada por usar reflection.

## 11.3 `reflect`

```c
reflect(User)         // TypeInfo do struct
reflect(Color)        // TypeInfo do enum, com `variants` preenchido
```

O argumento tem de ser um tipo (um `MetaType`); qualquer outra coisa é `LAP0601`.
`reflect` é **intrínseco do checker**, e não uma nativa comum, porque seu parâmetro
não é expressável na gramática de tipos — é o mesmo tratamento que a indexação já
recebe ao produzir `Result<T, IndexError>` (§21).

Reflection sobre funções (`FunctionInfo`) e sobre AST ficam para depois. A primeira
versão prioriza `type` e `enum`.

---

# 12. Macros de controle

`goto`/`label` (§10) tornam construíveis, como macros do prelude, as construções de
controle que a linguagem não tem. As que a linguagem **já** tem — `if` e `match` —
ficam onde estão, e §12.3 explica por quê.

## 12.1 `@unless`

```c
macro unless
    match Expression:condition Block:body
    expand {
        goto done if condition;
        body;
        label done;
    };
```

`goto`, `label` e `!` (Q4) bastam. Nada de novo no compilador.

## 12.2 `@while`

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

`@while` é a razão de `goto` poder voltar (§10.2), e é o primeiro programa
LapisLang capaz de não terminar — o evaluator aborta com `LAP0303`.

É também a demonstração mais forte da tese: **um laço, que em qualquer outra
linguagem é trabalho de compilador, aqui é seis linhas de `prelude.ls`.**

## 12.3 `if` e `match` continuam no compilador

A proposta inicial previa `@if` e `@match` substituindo os nós da Core. **Não vão,
e o motivo é `match`.**

`@if` sozinho é trivial — `goto`, `label` e `!` bastam. Mas `@match` precisa **ler a
carga de uma variante**, e nenhuma das saídas examinadas se sustentou:

| Saída examinada | Por que caiu |
|---|---|
| `enumTag`/`enumPayload` como nativas | o tipo da carga depende da variante; `fn(Any, Int) Any` tornaria `@match` **menos** tipado que o `Match` de hoje |
| Carga como campo do escrutinado (`result.value`) | o tipo sai fácil, mas nada garante que a variante seja a certa: `r.value` sobre um `Err` é indefensável |
| Carga como campo + análise de dominância | funciona, mas faz a segurança de uma construção básica depender de análise de fluxo — muito para o que se ganha |

O padrão comum às três é o mesmo: **extrair carga com segurança é um problema de
linguagem, não de macro.** Uma macro transforma sintaxe; ela não tem como
estabelecer que um valor é da variante `Ok` no ponto do acesso.

**Decisão:** `if` e `match` permanecem construções do compilador. Continuam sendo
palavras reservadas, `CoreIf` e `CoreMatch` continuam na Core, e a desestruturação
por padrão (`Result.Ok(value) => ...`) continua sendo como se lê uma carga.

Manter `@if` sem `@match` seria pior que não ter nenhum dos dois: metade do controle
de fluxo viraria prelude e metade continuaria no compilador, e a análise teria duas
formas a entender em vez de uma.

## 12.4 O caminho de volta: acesso seguro à carga

A retirada volta à mesa quando a linguagem tiver uma **construção própria para ler a
carga de uma variante com segurança** — uma palavra reservada, não uma macro e não
um acesso a campo.

O que ela precisa entregar:

- o **tipo** da carga, derivado da variante nomeada no ponto do acesso;
- a **garantia** de que a variante é aquela, sem depender de análise de fluxo;
- um caminho definido quando não é — sem exceção de runtime, que a 0.2 não tem
  (§30, Q9).

Com ela no lugar, `@match` volta a ser possível, e aí sim a conta de §58.2 fecha:
`CoreMatch`, `CorePattern`, o `TryMatch` do evaluator e a máquina de exaustividade
saem, e entra uma construção só.

**Registrado como Q23**, sem forma definida. Especificá-la antes de precisar dela
seria projetar no escuro — o que esta seção documenta é o requisito, não a solução.

---

# 13. Diagnósticos

Faixas novas, seguindo a convenção do
[Apêndice B](../plans/appendix-b-diagnostics.md): um código publicado nunca é
reciclado, e os testes asseveram código + span, nunca a mensagem.

| Faixa | Assunto |
|---|---|
| `LAP05xx` | macros e controle de fluxo |
| `LAP06xx` | reflection |

`LAP04xx` já era do partial evaluator, então as faixas novas começam em `LAP05xx`.

Toda mensagem preserva o span da **invocação**, não o do corpo do `expand` — quem
lê o erro escreveu a invocação, não a macro:

```text
error LAP0503: rota POST já registrada: /products
  app.ls:24:1
  @post "/products" {
  ^^^^^^^^^^^^^^^^^
      = nota: registro anterior em app.ls:12:1
```

Para código que veio de um `expand`, o diagnóstico carrega **os dois** spans: o da
invocação e o do ponto dentro da macro que o originou.

---

# 14. Filosofia

1. Macros trabalham com **sintaxe**, não com valores.
2. Macros são identificadas por `@` e declaradas por `macro` — não são valores (Q19).
3. A macro define sua própria forma sintática via `match`.
4. Capturas preservam AST.
5. `constraint` roda em compile time, na **mesma** linguagem e no **mesmo** evaluator.
6. `throw` é erro de compilação, e só existe em compile time.
7. Reflection é somente leitura, nas duas fases, e devolve valores comuns.
8. Expansões são higiênicas.
9. Ambiguidade entre padrões é erro; ausência de match é erro.
10. Macro expansion não substitui type checking nem partial evaluation.
11. O runtime não conhece macros.
12. A Core permanece pequena — e só encolhe quando o substituto está pronto.
13. Macros **acrescentam** construções; não substituem as que o compilador já tem.
14. Extrair carga com segurança é problema de linguagem, não de macro (Q23).

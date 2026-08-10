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
> **Estado:** Q19–Q24 decididas pelo autor. O único ponto ainda em aberto é o
> **mecanismo** que garante a variante no acesso à carga (Q25), registrado em
> [`../plans/appendix-c-decisions.md`](../plans/appendix-c-decisions.md) — a spec
> adota a análise de dominância como resposta padrão.

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
| 10 | `@match` compara **variantes**; a carga vira **campo** (`result.value`) | decisão do autor (Q20, Q23): elimina `enumTag`/`enumPayload` e reaproveita a máquina de campos do M3 |
| 11 | `goto` pode saltar **para trás**, mas nunca para fora do próprio escopo | decisão do autor; traz laços e, com eles, o fim da garantia de terminação |
| 12 | `if`, `while` e `match` viram macros do prelude | decisão do autor (Q22); o token `if` sobrevive dentro de `goto ... if ...` |
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
def foreach = macro
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
| `Pattern` | `Enum.Variante`, um literal, ou `_` | o cabeçalho de um braço |

`Pattern` existe porque despacho por caso é comum e as categorias primitivas não o
cobrem: `_` não é expressão, e `Result.Ok` precisa ser lido como caminho de variante,
não como acesso a membro. É a categoria que `@match` usa, e qualquer macro de
despacho pode usar.

As capturas de literal tipado (`Str:path`) usam os nomes dos primitivos da 0.2 —
`Str`, não `String`.

**Capturas guardam AST, nunca valores avaliados.** `Expression:e` casando com
`f()` captura a árvore da chamada; nada é executado.

## 5.2 Repetição

```text
<Categoria>:<nome>*        separado por <token>
( <sub-padrão> )*          separado por <token>
```

A primeira forma repete **uma** captura; a segunda repete um **grupo**, e é o que
`@match` precisa, porque um braço são duas coisas:

```c
match Expression:scrutinee { (Pattern:arm Block:body)* separado por , }
```

Cada captura do grupo liga uma **lista**, e as listas são paralelas: `arm[i]` é o
cabeçalho do braço cujo corpo é `body[i]`. No `expand`, `arm...` e `body...`
expandem em paralelo.

> A repetição de grupo evita a saída fácil de inventar uma categoria `MatchArm` só
> para `@match`. Categorias são do sistema sintático e servem a qualquer macro;
> privilegiar uma construção seria contrariar §9, que diz que macros definem sua
> própria sintaxe.

---

# 6. `expand`

`expand` produz sintaxe. A saída é AST, nunca texto.

```c
def square = macro
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
def post = macro
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
def example = macro
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
def example = macro
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

A primitiva de controle de fluxo sobre a qual `@if`, `@unless` e `@match` são
construídos.

## 10.1 Sintaxe

```c
goto nome;              // salto incondicional
goto nome if cond;      // salto condicional
label nome;             // destino
```

`goto ... if ...` é **uma primitiva só**, não um `if` pós-fixo: se `@if` vai ser
construído a partir de `goto`, o salto condicional não pode depender de `if`.

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

`Never` já se propaga por `Let`, `If`, `Binary`, `Unary` e `Call` desde o M1, então
`goto` cabe em qualquer posição sem regra nova.

## 10.5 Execução

O evaluator representa `return` como **completion record**, não como exceção
(plano 08). `goto` entra no mesmo mecanismo: `Completion` ganha o caso
`Goto(rótulo)`. Um `Labeled` que define aquele rótulo captura a completion e segue
pelo corpo do join — exatamente como a fronteira de chamada captura `Return`.

Como os saltos são para frente e os joins são sufixos, a sequência de saltos é
estritamente crescente e sempre termina.

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
versão prioriza `type` e `enum`, que é o que `@match` precisa.

---

# 12. As macros de controle

`if`, `while` e `match` deixam de ser construções do compilador e passam a ser
macros do `prelude.ls`, escritas na própria linguagem.

O token `if` **sobrevive**, mas só dentro de `goto ... if ...`: é o salto
condicional, a primitiva a partir da qual as três são construídas. Derivá-lo do
`if` seria circular.

## 12.1 `@if` e `@unless`

```c
macro unless
    match Expression:condition Block:body
    expand {
        goto done if condition;
        body;
        label done;
    };
```

```c
macro if
    match Expression:condition Block:then else Block:otherwise
    expand {
        goto alt if !condition;
        then;
        goto done;
        label alt;
        otherwise;
        label done;
    }

    match Expression:condition Block:then
    expand {
        goto done if !condition;
        then;
        label done;
    };
```

Nada de novo é necessário: `goto`, `label` e `!` (Q4) bastam.

## 12.2 `@while`

Aqui o salto para trás paga o seu preço:

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

`@while` é a razão de `goto` poder voltar (§10.2), e é também o primeiro programa
LapisLang capaz de não terminar. O evaluator conta saltos e aborta com `LAP0303`.

## 12.3 `@match` compara variantes; a carga é campo

**Decisão do autor (Q20 + Q23).** Um braço nomeia **só a variante**, e a carga é
lida como **campo do escrutinado**:

```c
@match result {
    Result.Ok  { return result.value },
    Result.Err { return result.error }
}
```

Duas consequências, e as duas simplificam:

1. `enumTag` e `enumPayload` **não são necessários**. Comparar variantes basta, e
   enums já são comparáveis desde o M3 — o comentário do `CoreNodes.cs` que
   rejeitava aquelas primitivas continua correto.
2. A carga cai na **máquina de campos que o M3 já construiu** para `type`. Ler
   `result.value` é a mesma operação que ler `user.name`.

### Carga nomeada

Para haver campo, a carga precisa de nome. A declaração de `enum` ganha a mesma
forma que `type` já usa:

```c
def Result = enum<T, E> {
    Ok(value: T),
    Err(error: E)
};
```

O nome é **opcional**: `Ok(T)` continua válido, e apenas não é legível por campo.
Isso mantém todo programa 0.2 funcionando — mas o prelude passa a nomear as cargas
de `Result` e `Option`, porque é o que torna `@match` utilizável.

**Nomes de carga são únicos dentro do enum** (`LAP0523`). É o que faz o campo
determinar a variante sozinho: `result.value` só pode ser `Ok`. Sem essa regra o
checker precisaria de análise de fluxo só para saber o *tipo*, e não só para
garantir a segurança.

### O braço

```text
macro_match_arm = variant_path block
                | literal block
                | "_" block ;
```

Sem `=>` e sem parênteses de padrão: o braço é um caminho de variante seguido de um
bloco. `Result.Ok { ... }` não colide com a construção de `type`, que leva ponto
inicial (`.Result { ... }`, Q2).

### A expansão

```c
macro match
    match Expression:scrutinee { (Pattern:arm Block:body)* separado por , }
    constraint { /* exaustividade — §12.4 */ }
    expand {
        goto arm0 if scrutinee == Result.Ok;
        goto arm1 if scrutinee == Result.Err;
        goto done;

        label arm0;  /* corpo 0 */  goto done;
        label arm1;  /* corpo 1 */  goto done;
        label done;
    };
```

Uma cadeia de `goto ... if ...` sobre `==`. Nenhuma primitiva nova, nenhum nó novo
na Core.

### Comparação com variante portadora de carga

Para variantes nulárias (`Color.Red`) o `==` já funciona hoje. Para variantes com
carga, `Result.Ok` é um **construtor**, não um valor — e funções não são comparáveis
(`LAP0281`).

A regra nova, mínima:

> Comparar um valor de enum com um **construtor de variante** não aplicado compara
> **apenas a variante**, ignorando a carga. `r == Result.Ok` tem tipo `Bool`.

Uma regra de tipo e uma linha no evaluator.

## 12.4 Ler a carga com segurança

`result.value` tem tipo conhecido — `value` só existe em `Ok`, e nomes de carga são
únicos. Falta a outra metade: **garantir que a variante seja mesmo `Ok`** ali.

Fora de um braço, o acesso é indefensável:

```c
def r: Result<Int, IndexError> = xs[0];

print(r.value);        // e se for Err?
```

**A regra:** o acesso a um campo de carga só é aceito quando **dominado** por uma
comparação que fixa a variante — `scrutinee == Result.Ok`. Não sendo, é `LAP0524`.

É uma consulta de dominância sobre o grafo que `Labeled` já expõe (§10.3): um join
alcançado **apenas** por arestas guardadas por `x == E.V` pode assumir a variante.

> **A análise não é custo extra.** É a mesma que o plano 14 constrói para
> *bounds-check elimination* — a pesquisa que motiva o projeto. Ela chega um
> milestone antes e se paga duas vezes: elimina a checagem de limites **e** torna a
> leitura de carga segura sem `Result` aninhado.

**Enquanto essa análise não existir**, `Match` permanece na Core para os casos com
carga. É o critério de saída que o autor já estabeleceu, aplicado a uma parte
específica: o nó só sai quando o substituto estiver funcional.

## 12.5 Exaustividade por `constraint` e reflection

Q6 exige `match` exaustivo. Com `@match` sendo macro, quem impõe é a `constraint`,
usando reflection para descobrir as variantes:

```c
constraint {
    def enumName = enumNameOf(arms);

    if enumName == "" {
        if !hasWildcard(arms) {
            throw "match sobre valor não-enum exige um braço '_'";
        }
    } else {
        def faltando = missingVariants(reflect(enumName).variants, arms);

        if arrayLength(faltando) > 0 {
            throw "match não é exaustivo; faltam: " + join(faltando, ", ");
        }
    }
}
```

**O que faz isso funcionar é uma decisão já tomada.** Q3 exige variantes
qualificadas — `Color.Red`, nunca `Red`. Então os próprios braços nomeiam o enum, e
a `constraint` descobre qual é **sem precisar do tipo do escrutinado**, que em tempo
de expansão ainda não existe.

Sem Q3 isto seria impossível: a exaustividade dependeria do type checker, que roda
depois da expansão. É um caso em que uma decisão tomada por ergonomia acabou pagando
uma dívida arquitetural que ninguém tinha visto.

**Limite conhecido:** braços só de literais ou só `_` não nomeiam enum nenhum. Aí a
`constraint` exige `_` — a mesma regra que o checker aplica hoje a `Int`, `Float` e
`Str`.

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
13. `if`, `while` e `match` são prelude, não compilador (Q22).
14. `@match` compara variantes; a carga é campo do escrutinado (Q20, Q23).

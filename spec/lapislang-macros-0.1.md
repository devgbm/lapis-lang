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
> **Estado:** proposta. As decisões marcadas 🔴 aguardam confirmação do autor e
> estão registradas em [`../plans/appendix-c-decisions.md`](../plans/appendix-c-decisions.md).

---

## Changelog em relação à proposta original

Esta spec nasceu de um documento escrito fora do contexto do projeto. As mudanças
abaixo são adaptações necessárias, não preferências de estilo — cada uma corrige
um conflito real com a 0.2 já implementada.

| # | Mudança | Motivo |
|---|---|---|
| 1 | `emit` → **`expand`** | pedido do autor |
| 2 | Reflection também em **runtime**, somente leitura | pedido do autor; a proposta original a restringia a compile time |
| 3 | `macro nome ...` → **`def nome = macro ...`** 🔴 | §2 da 0.2: "todo nome é introduzido através de `def`", e não existem declarações nomeadas |
| 4 | `$end` → **`end`** | `$` não é lexável: identificadores são `[A-Za-z_][A-Za-z0-9_]*` (§7) |
| 5 | `String:path` → **`Str:path`** | o primitivo se chama `Str` desde a 0.2 |
| 6 | `if (cond) { }` → **`if cond { }`** | a 0.2 não usa parênteses na condição |
| 7 | `routes.contains(...)`/`routes.add(...)` → **primitivas de contexto** | não há method syntax nem mutação na linguagem (§8, §23) |
| 8 | Regras de macro: ordenadas → **todas testadas, exatamente uma deve casar** | a proposta pedia ordem *e* detecção de ambiguidade, o que é contraditório |
| 9 | `goto`/`label` ganham semântica de **join point**, não de salto arbitrário | a Core é orientada a expressões e não tem nó `Block` (Q10) |
| 10 | `If` e `Match` **permanecem** na Core | pedido do autor: só saem quando `@if`/`@match` estiverem funcionais |
| 11 | Fases de implementação reescritas | a proposta mandava construir a Surface AST, que já existe desde o M1 |

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

Uma macro é uma expressão, ligada a um nome por `def` — como `fn`, `type` e
`enum` (§2):

```c
def unless = macro
    match Expression:condition Block:body
    expand {
        goto done if condition;
        body;
        label done;
    };
```

Forma geral:

```text
def <nome> = macro
    match <padrão>
    [constraint { <statements> }]
    expand { <sintaxe> }
    [ match ... [constraint ...] expand { ... } ]*
    ;
```

Uma macro define uma ou mais **regras**. Cada regra é um trio
`match` / `constraint`? / `expand`.

> **🔴 Decisão Q19.** A proposta original escrevia `macro unless ...`, uma
> declaração nomeada. Isso contraria o princípio §2 da 0.2 — "não existem
> declarações nomeadas específicas para funções, tipos ou enums", e a forma única
> de introduzir um nome é `def name = expression;`. Adotar `def x = macro ...`
> mantém uma regra só para todo o idioma e reaproveita escopo, sombreamento e
> diagnósticos que já existem.
>
> A ressalva: uma macro **não é um valor de runtime**. `def m = macro ...;`
> seguido de `print(m)` não faz sentido. O tipo de uma macro é `MacroType`, um
> tipo interno de compile time; usá-la em posição de valor é `LAP0510`. É o mesmo
> tratamento que `MetaType` já recebe para `type`/`enum`, então não é um conceito
> novo.

## 3.1 Escopo e visibilidade

Macros são visíveis **do ponto da declaração em diante**, no arquivo onde foram
declaradas — a mesma regra de `def` (§8, Q8). Uma macro não pode se invocar, nem
direta nem indiretamente: não há recursão na 0.2, e isso vale para a expansão.

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

As capturas de literal tipado (`Str:path`) usam os nomes dos primitivos da 0.2 —
`Str`, não `String`.

**Capturas guardam AST, nunca valores avaliados.** `Expression:e` casando com
`f()` captura a árvore da chamada; nada é executado.

## 5.2 Repetição

```text
<Categoria>:<nome>*  separado por <token>
```

Necessário para `@match`, que precisa de N braços:

```c
match Expression:scrutinee { MatchArm:arms* separado por , }
```

Uma captura repetida liga uma **lista** de árvores, e no `expand` é expandida com
`arms...` (elipse).

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

## 10.2 Regra: saltos são para frente

Um `goto` só pode nomear um `label` declarado **depois** dele, no mesmo bloco ou
num bloco que o contenha, dentro da mesma função. Saltar para trás é `LAP0521`.

Isto não é timidez: sem recursão (Q8), salto para trás é a única forma de escrever
um programa que não termina. Proibi-lo mantém a garantia de terminação da 0.2
intacta, e é **exatamente o suficiente** para `@if`, `@unless` e `@match`, que só
saltam para frente. Laços chegam com a recursão, na 0.3.

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

## 12.1 `@if` e `@unless`

```c
def unless = macro
    match Expression:condition Block:body
    expand {
        goto done if condition;
        body;
        label done;
    };
```

```c
def ifm = macro
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

## 12.2 `@match` — e o preço dele

Aqui a proposta encontra o princípio §58.2 de frente, e vale escrever a conta em
vez de escondê-la.

`@match` precisa de duas coisas que `goto`/`label` não dão: **testar qual variante
um valor é** e **extrair a carga**. Isso exige duas primitivas novas:

```text
enumTag(valor) Int
enumPayload(valor, índice) <tipo da carga>
```

O comentário que hoje está em `CoreNodes.cs` dizia exatamente por que `Match`
permaneceu primitivo:

> desugará-lo exigiria primitivas `enum_tag` e `enum_payload`, aumentando o
> runtime — contra a spec §58 ("runtime mínimo")

A troca é: **2 nativas a mais** contra **um nó da Core, a máquina de padrões do
evaluator e a de exaustividade do checker a menos**. A conta favorece as macros —
mas há um obstáculo real, e ele decide o cronograma:

> **O tipo de `enumPayload` depende da variante.** Em
> `Result.Ok(v) => ...`, `v` é `T`; em `Result.Err(e) => ...`, `e` é `E`. Uma
> assinatura `fn(Any, Int) Any` perderia isso, e um `@match` expandido seria
> **menos** tipado que o `Match` de hoje. Fazer `enumPayload` ser outro intrínseco
> do checker, com tipo dependente da variante testada no caminho, é possível — mas
> é trabalho de verdade, não um detalhe.

**Por isso `If` e `Match` continuam na Core.** Eles saem quando `@if` e `@match`
estiverem funcionais e tipando tão bem quanto os nós que substituem — não antes.
É um milestone com critério de saída objetivo, não uma intenção.

## 12.3 Exaustividade de `@match` por `constraint` e reflection

Q6 exige que `match` seja exaustivo. Com `@match` sendo macro, quem impõe isso é a
`constraint` — usando reflection para descobrir as variantes:

```c
def matchm = macro
    match Expression:scrutinee { MatchArm:arms* separado por , }

    constraint {
        def enumName = enumNameOfFirstVariantPattern(arms);
        def info = reflect(enumName);

        // toda variante declarada tem de aparecer, ou há um `_`
        ...
    }

    expand { ... };
```

O detalhe que faz isso funcionar é uma decisão já tomada: **Q3 exige variantes
qualificadas** (`Color.Red`, nunca `Red`). Então os próprios braços nomeiam o enum,
e a `constraint` descobre qual é sem precisar do tipo do escrutinado — que, em
tempo de expansão, ainda não existe.

Sem Q3 isto seria impossível: a exaustividade dependeria do type checker, que roda
depois. É um caso em que uma decisão de ergonomia acabou pagando uma dívida
arquitetural.

**Limite conhecido:** um `@match` cujos braços sejam só literais ou só `_` não
nomeia enum nenhum, e a `constraint` não tem o que reflectir. Nesse caso ela exige
um braço `_` — a mesma regra que o checker aplica hoje a `Int`, `Float` e `Str`.

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
2. Macros são identificadas por `@` e ligadas por `def`.
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

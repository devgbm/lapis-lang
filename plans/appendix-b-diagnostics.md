# Apêndice B — Catálogo de Diagnósticos

Formato: `LAP` + 4 dígitos. Faixas por fase, para que o código já indique onde
investigar.

| Faixa | Fase |
|---|---|
| `LAP00xx` | Lexer |
| `LAP01xx` | Parser |
| `LAP02xx` | Type Checker |
| `LAP03xx` | Evaluator (runtime da linguagem) |
| `LAP04xx` | Partial Evaluator |
| `LAP05xx` | Macros e controle de fluxo |
| `LAP06xx` | Reflection |
| `LAP09xx` | CLI / uso |

**Regra de estabilidade:** um código, uma vez publicado, nunca é reciclado com
outro significado. Diagnóstico removido ⇒ código aposentado.

**Regra de teste:** todo código desta tabela tem pelo menos um teste que o
dispara, asseverando **código + span** — nunca a mensagem.

---

## LAP00xx — Lexer

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0001` | error | caractere inesperado `'{0}'` |
| `LAP0002` | error | literal inteiro fora do intervalo de `Int` |
| `LAP0003` | error | sequência de escape desconhecida `'\{0}'` |
| `LAP0004` | error | literal de string não terminado |
| `LAP0005` | error | comentário de bloco não terminado |
| `LAP0006` | error | literal float malformado |

---

## LAP01xx — Parser

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0101` | error | esperado `{0}`, encontrado `{1}` |
| `LAP0102` | error | esperado `;` ao final da declaração |
| `LAP0103` | error | esperado `=` em `def` |
| `LAP0104` | error | parâmetro de função requer anotação de tipo |
| `LAP0105` | error | esperado `]` para fechar a indexação |
| `LAP0106` | error | campo de `type` requer `;` |
| `LAP0107` | error | `match` requer ao menos um braço |
| `LAP0108` | error | `{` sem `}` correspondente |
| `LAP0109` | error | fim de arquivo inesperado |
| `LAP0110` | error | esperado uma expressão |
| `LAP0111` | error | esperado um tipo |
| `LAP0112` | error | esperado um identificador |
| `LAP0113` | error | esperado um padrão |
| `LAP0114` | error | comparações não podem ser encadeadas; use parênteses |
| `LAP0115` | error | expressão aninhada profundamente demais (limite {0}) |
| `LAP0116` | error | lista de argumentos genéricos vazia |
| `LAP0117` | warning | vírgula extra ignorada |

---

## LAP02xx — Type Checker

### Nomes e escopo

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0201` | error | variável `'{0}'` não existe |
| `LAP0202` | error | `'{0}'` já foi definido neste escopo |
| ~~`LAP0203`~~ | — | **aposentado** (Q3): com variantes sempre qualificadas não há injeção no escopo, logo não há colisão possível |
| `LAP0204` | error | tipo `'{0}'` não existe |
| `LAP0205` | warning | `'{0}'` foi definido mas nunca usado — **reservado**, nenhum milestone o emite ainda |
| `LAP0206` | error | não é possível atribuir a `'{0}'` |
| `LAP0207` | error | `'{0}'` é `var` e não pode ser usado dentro de outra função |

`LAP0206` traz nota apontando a declaração: só um `var` pode ser reatribuído, e um
`def` ou parâmetro é definitivo.

`LAP0207` é a restrição que faz a mutação caber na linguagem (Q25): **nenhuma
closure captura `var`**, lendo ou escrevendo. Sem isso seria preciso decidir entre
captura por valor e por referência — e a segunda traria aliasing, que o partial
evaluator teria de modelar antes de especializar qualquer coisa com closure.

### Anotações e atribuição

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0210` | error | esperado `{0}`, encontrado `{1}` |

### Funções e chamadas

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0220` | error | `{0}` não é uma função e não pode ser chamado |
| `LAP0221` | error | esperados {0} argumentos, fornecidos {1} |
| `LAP0222` | error | argumento {0}: esperado `{1}`, encontrado `{2}` |

### Condicionais e junção

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0230` | error | condição de `if` deve ser `Bool`, encontrado `{0}` |
| `LAP0231` | error | ramos de `if` têm tipos incompatíveis: `{0}` e `{1}` |

### Spans e indexação

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0240` | error | elementos de span devem ter o mesmo tipo: `{0}` e `{1}` |
| `LAP0241` | error | span vazio requer anotação de tipo |
| `LAP0242` | error | `{0}` não é indexável |
| `LAP0243` | error | índice deve ser `Int`, encontrado `{0}` |
| `LAP0244` | error | índice {0} fora dos limites de `{1}` |
| `LAP0245` | error | o tamanho de um span deve ser um `Int` não negativo |

`LAP0244` só existe porque o tamanho vive no tipo (plano 24): com `[Int;3]`,
`s[3]` é erro **de tipo**, checado pela mesma maquinaria que rejeita
`def a: Str = 1;`. Índice dinâmico ou span `[T;?]` devolvem `Option<T>` — provar
`i < n` para um `i` derivado de laço é trabalho do partial evaluator (plano 14),
não do checker. A nota do diagnóstico aponta o alvo e diz quantos elementos ele
tem.

**A indexação não devolve `Result<T, IndexError>`** e sim `Option<T>` (Q31):
`IndexError.OutOfBounds` era um enum de uma variante cujo significado é "falhou",
e `Result` existe para o erro que diz alguma coisa. `IndexError` saiu do prelude
junto. As mensagens desta seção falam de **span**, não de array — `Array` e
`List` ficam reservados para a biblioteca.

`LAP0240` usa a **junção** dos tipos, não a igualdade: `.[.[1], .[2, 3]]` é um
span de spans de tamanhos diferentes, e o elemento comum é `[Int;?]` — a mesma
regra que dá tipo a `if c { .[1] } else { .[1, 2] }`. O diagnóstico sobra para o
que junção nenhuma resolve, como `.[1, "x"]`.

`LAP0241` continua valendo: `.[]` diz o tamanho, não o tipo do elemento.

`LAP0245` é do **tipo** `[T;N]`, escrito em posição de anotação — `[Int;true]`. A
quantidade de `.[T; inicial; n]` **não** passa por ele: negativa produz span
vazio, e grande demais aborta em execução com `LAP0304`. Os dois são de execução
de propósito. Se o checker rejeitasse a quantidade constante, o partial evaluator
poderia transformar um programa que compila num que não compila, só por dobrar
`0 - 1` em `-1` — e nenhuma transformação dele pode mudar se um programa é bem
tipado. Span vazio para quantidade negativa é a mesma escolha da divisão inteira
por zero (Q9): a operação é total.

`LAP0304` mora na família de execução, junto de `LAP0303`, e pelo mesmo motivo: a
garantia não é "nunca falta memória", é "o programa termina e diz o que houve".

### Campos, variantes e construção

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0250` | error | `{0}` não possui o campo `'{1}'` |
| `LAP0251` | error | `{0}` não possui a variante `'{1}'` |
| `LAP0252` | error | `{0}` não é um tipo construível |
| `LAP0253` | error | campo `'{0}'` ausente na construção de `{1}` |
| `LAP0254` | error | campo `'{0}'` não existe em `{1}` |
| `LAP0255` | error | campo `'{0}'` inicializado mais de uma vez |

### Match

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0260` | error | padrão incompatível com o tipo `{0}` |
| `LAP0261` | error | braços de `match` têm tipos incompatíveis: `{0}` e `{1}` |
| `LAP0262` | error | `match` não é exaustivo; faltam: {0} |
| `LAP0263` | warning | braço inalcançável |
| `LAP0264` | error | variante `'{0}'` espera {1} argumentos, fornecidos {2} |

### `return`

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0270` | error | `return` de `{0}` em função que retorna `{1}` |
| `LAP0271` | error | `return;` sem expressão em função que retorna `{0}` |
| `LAP0272` | error | nem todos os caminhos de execução retornam um valor |
| `LAP0273` | warning | código inalcançável após `return` |
| `LAP0274` | error | `return` fora de uma função |

### Operadores

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0280` | error | operador `{0}` não se aplica a `{1}` e `{2}` |
| `LAP0281` | error | funções não podem ser comparadas com `{0}` |

### Generics

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0290` | error | `{0}` espera {1} argumentos genéricos, fornecidos {2} |
| `LAP0291` | error | o parâmetro `{0}` é de tipo; um valor não serve como argumento |
| `LAP0292` | error | o parâmetro `{0}` é constante; um tipo não serve como argumento |
| `LAP0293` | error | `{0}` espera `{1}`, encontrado `{2}` |
| `LAP0294` | error | o argumento de `{0}` não é constante em tempo de compilação |
| `LAP0295` | error | `{0}` é genérico e requer argumentos de tipo |
| ~~`LAP0296`~~ | — | **aposentado** (Q7): nenhum argumento genérico é inferido |
| ~~`LAP0297`~~ | — | **aposentado** (Q7): idem |
| `LAP0298` | error | não foi possível determinar os argumentos genéricos de `{0}` |

`LAP0290` e `LAP0295` são o mesmo confronto de aridade visto de dois ângulos:
`LAP0295` quando **nenhum** argumento foi escrito (o caso comum, e o que merece a
mensagem "requer argumentos"), `LAP0290` quando a quantidade escrita está errada.

`LAP0298` cobre um caso específico e vale o código próprio: acessar a variante de
um enum genérico sem instanciá-lo (`Result.Ok(1)`). A nota diz a forma correta,
`Result<...>.Ok`.

---

## LAP03xx — Execução

Não são diagnósticos de compilação: são relatados na saída de execução com span.

| Código | Severidade | Mensagem |
|---|---|---|
| ~~`LAP0301`~~ | — | **aposentado** (Q9): a divisão inteira por zero produz o maior `Int`, então a operação é total |
| `LAP0302` | abort | profundidade de chamada excedida (limite {0}) |
| `LAP0303` | abort | limite de saltos excedido (limite {0}) |
| `LAP0304` | abort | span de {0} elementos excede o limite de {1} |

`LAP0303` chegou com o `goto` para trás (M6). Até o M4 todo programa terminava por
construção — sem recursão (Q8) e sem laços; `goto` para trás acaba com isso, e este
é o diagnóstico que troca um travamento por uma mensagem. O orçamento é do
**programa inteiro** (1.000.000 de saltos), não de cada laço: é o que torna "todo
programa termina ou reporta `LAP0303`" uma propriedade verificável.

`LAP0304` é o mesmo raciocínio aplicado à quantidade de `.[T; inicial; n]` (plano
24): um span pedido grande demais termina com diagnóstico em vez de travar a
máquina. O orçamento é de **execução**, e não de compilação — se o checker
rejeitasse a quantidade constante, o partial evaluator poderia transformar um
programa que compila num que não compila, só por dobrar a expressão do tamanho.

### `is` (plano 25 — proposta, M16)

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0730` | error | a ligação de `is` só vale em condição de `if` ou à esquerda de `&&` |
| `LAP0731` | error | a ligação de `is` não atravessa um salto |
| `LAP0732` | error | `'{0}'` não é variante de {1} |
| `LAP0733` | error | a variante `'{0}'` não carrega valor |
| `LAP0734` | error | a variante `'{0}'` carrega {1} valores; escreva um padrão de `match` |

`LAP0730` e `LAP0731` são a **regra de escopo** da ligação, e não limitações de
implementação. `e is Some(v)` produz um `Bool`, e um `Bool` vai a qualquer lugar —
a ligação, não. As duas posições permitidas são aquelas em que o desugar consegue
dar escopo a `v` traduzindo para `match`.

`LAP0731` em particular é a mesma regra do `var` declarado entre um `goto` e o seu
rótulo: o destino é alcançável sem passar pela ligação, então ela não pode existir
lá. Ver `tests/conformance/eval/mutation/scope_across_joins_with_jump.ls`.

`LAP0732` repete a mensagem de `LAP0251` de propósito — é o mesmo erro visto de
outro lugar. `LAP0734` é a fronteira com `match`: `is` liga **um** valor, e a forma
completa já tem casa.

---

## LAP052x — `goto` e `label` (implementado, M6)

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0520` | error | o rótulo `'{0}'` não existe |
| `LAP0521` | error | o rótulo `'{0}'` pertence a uma função externa |
| `LAP0522` | error | o rótulo `'{0}'` já foi declarado neste bloco |

`LAP0521` existe para não responder "não existe" a quem escreveu um rótulo que
existe: um `label` é local à função, como `return`, e a diferença entre "esse nome
não é rótulo de nada" e "esse rótulo é de outra função" é a diferença entre um erro
de digitação e um mal-entendido sobre escopo.

A condição de `goto ... if ...` usa `LAP0230`, o mesmo do `if` — é a mesma exigência.

---

## LAP04xx — Partial Evaluator

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0401` | error | `IndexUnchecked` não é permitido em código-fonte |
| `LAP0402` | warning | limite de {0} atingido; especialização interrompida |
| `LAP0403` | error | binding `'{0}'` de `--dynamic` não existe no programa |
| `LAP0404` | error | tipo inválido em `--dynamic`: `{0}` |

---

## LAP05xx — Macros e controle de fluxo

Introduzidos pela [spec de macros](../spec/lapislang-macros-0.1.md). Planos 16–20.

### Macros

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0501` | error | nenhum padrão de `@{0}` corresponde a esta invocação |
| `LAP0502` | error | padrões ambíguos para `@{0}`: {1} regras correspondem |
| `LAP0503` | error | *(mensagem do `throw` da constraint)* |
| `LAP0504` | error | categoria sintática `{0}` não existe |
| `LAP0505` | error | expansão de macro profunda demais (limite {0}) |
| `LAP0506` | error | `expand` produziu statements onde se esperava uma expressão |
| `LAP0507` | error | `throw` só é válido dentro de `constraint` |
| `LAP0508` | error | `throw` espera Str, encontrado `{0}` |
| `LAP0509` | error | macro `{0}` não existe |
| `LAP0510` | error | uma macro não é um valor e não pode ser usada em posição de expressão |

`LAP0503` é o único código cuja mensagem vem do programa: é o texto que a
`constraint` passou a `throw`. O span é sempre o da **invocação**, com nota
apontando o ponto dentro da macro.

`LAP0501`, `LAP0502`, `LAP0504`–`LAP0506`, `LAP0509`, `LAP0510` e `LAP0511`
estão **implementados** (M8); `LAP0503`, `LAP0507` e `LAP0508`, no M9.

`LAP0507` e `LAP0508` são do **checker**, não do expander: onde `throw` vale e o
que ele carrega são perguntas de tipo. `LAP0508` só aparece dentro de um
`constraint` — fora dele o problema é `LAP0507`, e somar os dois descreveria a
mesma linha duas vezes.

Uma `constraint` que não compila reporta **os erros dela**, e não `LAP0503`:
"esta construção é inválida" seria uma leitura errada de um erro de tipo dentro da
própria macro.

`LAP0511` (macro já declarada) não estava na proposta: apareceu ao implementar o
registro. Um nome de macro é único no arquivo, como um `def` no mesmo bloco.

### Controle de fluxo

`LAP0520`–`LAP0522` saíram da proposta: estão implementados (M6) e documentados
acima, na seção "LAP052x — `goto` e `label`".

`LAP0523` em diante estão **livres**: foram esboçados para a leitura de carga por
campo, que Q23 descartou por ser insegura. Como nunca chegaram a ser publicados —
não há implementação —, os números voltam ao pool. A construção que Q23 vier a
definir alocará os seus.

---

## LAP06xx — Reflection

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0601` | error | `reflect` espera um tipo, encontrado `{0}` |
| `LAP0602` | error | o tipo `{0}` não foi declarado neste ponto |

Os dois são do **checker**: `reflect` é um intrínseco checado especialmente, e a
pergunta "isto é um tipo?" é de tipo.

`LAP0602` é exclusivo de compile time. Dentro de um `constraint`, os tipos do
programa vêm da tabela de declarações sintáticas — o checker ainda não rodou sobre
o programa —, e a tabela respeita a ordem do arquivo (Q8): um tipo declarado
**abaixo** da invocação não está lá, e é isso que o código diz. Fora de um
`constraint`, um nome que não existe recebe o `LAP0201` de sempre.

Um número de argumentos errado em `reflect` usa `LAP0221`, o mesmo de qualquer
chamada: não é um erro diferente por ser intrínseco.

---

## LAP07xx — Type members

Introduzidos pela [spec de type members](../spec/lapislang-type-members-0.1.md).
Planos 21–23.

### Declaração e resolução (plano 21 — implementado, M13)

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0701` | error | o tipo `{0}` não possui o membro `'{1}'` |
| `LAP0702` | error | o membro `'{0}'` de `'{1}'` já foi declarado |
| `LAP0703` | error | `'{0}'` já é variante de `'{1}'` |
| `LAP0704` | error | `'{0}'` não é um tipo declarado |
| `LAP0705` | error | um membro de tipo não pode ser `var` |
| ~~`LAP0706`~~ | — | **reservado, nunca emitido** |
| `LAP0707` | error | `'{0}'` é um membro de {1} e não um campo da instância |

**Atribuição a campo reusa os códigos que já existem.** `mutavel.campo = e` é
válido quando o binding é `var`; os erros são `LAP0206` (o binding é `def`),
`LAP0210` (tipo errado) e `LAP0250` (campo inexistente). `LAP0707` é o único novo,
e existe porque a mensagem certa é específica: `u.hello = ...` falha não porque
`hello` não exista, mas porque ele é membro do **tipo**, não campo da instância.

`LAP0702` é mais específico que `LAP0202` de propósito: "'create' já foi definido
neste escopo" seria confuso, porque `create` sozinho não está definido em escopo
nenhum. Pelo mesmo motivo um membro **não** colide com um `def` comum homônimo —
são símbolos distintos.

`LAP0703` existe porque `Color.Red` e `Color.membro` ocupam a **mesma** sintaxe:
sem ele, declarar um membro com nome de variante seria resolvido por precedência
silenciosa, e quem escreveu o segundo nunca saberia.

`LAP0704` cobre "não é tipo" e "ainda não foi declarado" com a mesma mensagem: a
ordem do topo é sequencial (Q8), então um membro antes do `type` é o mesmo caso
que qualquer nome usado antes da declaração.

`LAP0706` está **reservado e nunca é emitido**: chegou a ser alocado para "nada
qualificado é atribuível", leitura que a decisão do autor corrigiu antes de virar
código. O número não é reciclado.

### Instância (plano 22 — implementado, M14)

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0710` | error | o membro `'{0}'` de {1} exige uma instância |
| `LAP0711` | error | o membro `'{0}'` de {1} é estático |
| `LAP0712` | error | o parâmetro `'{0}'` requer anotação de tipo |

`LAP0710` e `LAP0711` existem para que `User.hello` e `user.hello()` não sejam
dois caminhos para a mesma coisa. Sem eles, `User.hello` ficaria ambíguo entre "o
membro" e "a função não aplicada", e a segunda leitura tornaria
`User.hello(user)` equivalente à chamada por instância.

`LAP0712` reusa a **mensagem** de `LAP0104` — "requer anotação de tipo" — porque
é o mesmo problema visto de outro lugar: o parâmetro não tem tipo e não há de onde
tirá-lo. O código é outro porque quem reporta é o checker: só ele sabe se a função
é o valor de um `def T.m`, e o parser não pode decidir.

### Extensions genéricas (plano 23)

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0720` | error | `'{0}'` é declarado para {1} e para {2}, que se sobrepõem |
| `LAP0721` | error | `'?'` só é válido como argumento genérico do dono de um membro |
| `LAP0722` | error | o dono do membro tem {0} argumentos genéricos, e `'{1}'` espera {2} |

A decisão da **Q27** trocou `?` curinga por parâmetro nomeado, e `LAP0721` mudou
de sentido junto: ele não fala mais de "parâmetro que não aparece no dono" — não
há parâmetros no dono —, e sim da fronteira de onde `?` vale. `def x: Result<?, ?>`
não compila, porque `?` como tipo de valor seria um `Any` estrutural pela porta
dos fundos.

`LAP0720` fica mais fácil de defender com a decisão nova: com `?` escrito, os dois
padrões que se sobrepõem estão **na fonte**, e não são consequência sutil de duas
declarações parecidas.

---

## LAP09xx — CLI

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0901` | error | arquivo não encontrado: `{0}` |
| `LAP0902` | error | esperado um arquivo `.ls` |
| `LAP0903` | error | argumentos inválidos |

---

## Notas de implementação

- Mensagens em português, alinhadas com a spec. Se internacionalização entrar
  depois, os códigos já são a chave estável.
- Todo diagnóstico com "esperado X, encontrado Y" deve imprimir os tipos com o
  mesmo formatador (`LapisType.ToDisplayString()`), para que `Int[]` nunca
  apareça como `ArrayType(PrimitiveType(Int))`.
- Notas (`DiagnosticNote`) são usadas para: sugestão de nome próximo
  (`LAP0201`), local da definição anterior (`LAP0202`), lista de variantes
  faltantes (`LAP0262`) e local do `return` esperado (`LAP0272`).
- **Códigos aposentados nunca são reciclados.** `LAP0203`, `LAP0296`, `LAP0297` e
  `LAP0301` saíram por causa de decisões do apêndice C e seus números ficam
  permanentemente vagos. Aposentado significa **sem constante** em
  `DiagnosticCodes`: fica só o comentário com o número e o motivo, para que
  ninguém volte a emiti-lo por engano.
- **Todo código com constante precisa de um caso de conformidade** que o produza
  (`DiagnosticCoverageTests`, M5). A isenção existe, é nomeada e vem com motivo —
  hoje só `LAP0205` (reservado, ainda não emitido) e `LAP0302` (inalcançável sem
  recursão). Uma isenção que passou a ter caso quebra o teste e precisa sair.
- `LAP0290` passou a cobrir também "função genérica chamada sem argumentos
  genéricos explícitos" (Q7), com nota indicando os parâmetros a escrever.
- `LAP0202` é reportado pelo **desugar**, não pelo type checker (Q10).
- Diagnósticos sobre código que veio de um `expand` carregam **dois** spans: o da
  invocação (onde apontam) e o do ponto dentro da macro (em nota). Quem lê o erro
  escreveu a invocação, não a macro.
- `LAP0294` traz nota com o que *serve* como argumento const (Q18): literal, ou
  `def` ligado a literal. Um parâmetro const de um genérico envolvente também
  serve e não produz diagnóstico algum.

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
| `LAP0203` | error | variante `'{0}'` colide com um nome já definido |
| `LAP0204` | error | tipo `'{0}'` não existe |
| `LAP0205` | warning | `'{0}'` foi definido mas nunca usado |

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

### Arrays e indexação

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0240` | error | elementos de array devem ter o mesmo tipo: `{0}` e `{1}` |
| `LAP0241` | error | array vazio requer anotação de tipo |
| `LAP0242` | error | `{0}` não é indexável |
| `LAP0243` | error | índice deve ser `Int`, encontrado `{0}` |

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
| `LAP0291` | error | esperado um tipo na posição genérica {0}, encontrado um valor |
| `LAP0292` | error | esperado um valor constante na posição genérica {0}, encontrado um tipo |
| `LAP0293` | error | argumento const {0}: esperado `{1}`, encontrado `{2}` |
| `LAP0294` | error | argumento genérico deve ser constante em tempo de compilação |
| `LAP0295` | error | `{0}` é genérico e requer argumentos de tipo |
| `LAP0296` | error | argumentos const generic não são inferidos; forneça-os explicitamente |
| `LAP0297` | error | não foi possível inferir o parâmetro genérico `'{0}'` |

---

## LAP03xx — Execução

Não são diagnósticos de compilação: são relatados na saída de execução com span.

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0301` | abort | divisão inteira por zero |
| `LAP0302` | abort | profundidade de chamada excedida (limite {0}) |

---

## LAP04xx — Partial Evaluator

| Código | Severidade | Mensagem |
|---|---|---|
| `LAP0401` | error | `IndexUnchecked` não é permitido em código-fonte |
| `LAP0402` | warning | limite de {0} atingido; especialização interrompida |
| `LAP0403` | error | binding `'{0}'` de `--dynamic` não existe no programa |
| `LAP0404` | error | tipo inválido em `--dynamic`: `{0}` |

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

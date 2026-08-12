# Plano 23 — Extensions genéricas e especializadas

**Projeto:** `Lapis.Ast`, `Lapis.TypeChecker`
**Milestone:** M15
**Spec:** [`lapislang-type-members-0.1.md`](../spec/lapislang-type-members-0.1.md) §8.1
**Depende de:** 21 (type members), 22 (instância), 04 (generics)

---

## Objetivo

`def Result<T>.isOk` e `def Result<Int>.doubleOrZero` — membros sobre tipos
genéricos, e a regra que decide qual se aplica.

> **Este plano tem uma questão aberta que precisa de decisão antes de virar
> código.** Ver §23.2 e **Q27**. Ele está escrito para deixar a decisão preparada,
> não para ser executado como está.

## Escopo

**Entra:** `def T<P>.m`, `def T<Arg>.m`, casamento do receptor contra o padrão do
dono, regra de especificidade.

**Fica de fora:** *where clauses*, bounds, e qualquer forma de restrição sobre `P`
— a 0.2 não tem bounds em nenhum lugar, e introduzi-los aqui os traria para a
linguagem inteira pela porta dos fundos.

---

## O que será construído

### 23.1 As duas formas

```c
def Result<T>.isOk        = fn(self) Bool { ... };   // parâmetro: vale para todo Result
def Result<Int>.doubled   = fn(self) Int { ... };    // argumento: só para Result<Int, E>
```

A diferença é sintaticamente invisível — `T` e `Int` são os dois um
`NamedTypeSyntax` sem argumentos, exatamente a ambiguidade que a 0.2 já conhece de
`GenericArgumentSyntax` ("um identificador nu é os dois"). A regra que a resolve
tem de ser dita:

> Um nome no dono é **parâmetro** se aparecer na lista de parâmetros genéricos da
> própria declaração; caso contrário é um tipo.

O que exige declarar os parâmetros:

```c
def<T> Result<T>.isOk = fn(self) Bool { ... };
```

Feio, e honesto: é o mesmo princípio de Q7 — argumento genérico é sempre
explícito, e adivinhar qual identificador é parâmetro seria inferência sintática.
A alternativa (convenção de maiúscula, `T` é parâmetro e `Int` não) foi descartada
por transformar estilo em semântica.

### 23.2 A questão aberta — Q27

Casar `Result<Int, IndexError>` contra `Result<T, E>` **é unificação**, e Q7
estabelece que argumento genérico nunca é inferido. As duas coisas não convivem em
silêncio, e há três saídas:

| Saída | Custo |
|---|---|
| **A. Só extensions especializadas** — `Result<Int>.m`, nunca `Result<T>.m` | some a metade útil da feature: `isOk` teria de ser escrito por instanciação |
| **B. Unificação restrita ao dono** — casar só a cabeça `Result<...>` e ligar os parâmetros posicionalmente | é inferência, mas de forma **fechada**: sem bounds, sem recursão, sem falha parcial. Q7 continua valendo onde ela vale, que é chamada de função |
| **C. Exigir o argumento na invocação** — `result.isOk<Int>()` | coerente com Q7 ao pé da letra, e inutiliza a sintaxe: ninguém escreve isso |

**A recomendação é B**, com a justificativa registrada: o que Q7 recusa é deduzir
o argumento de uma chamada a partir dos valores passados. Aqui o argumento já
está **escrito no tipo do receptor** — `Result<Int, IndexError>` é o que o checker
já sabe —, e o casamento só o transporta para o corpo do membro. Não há busca, não
há escolha, não há falha ambígua.

Isso precisa de confirmação do autor antes da implementação.

### 23.3 Especificidade

Se B for adotada, `Result<Int>` casa com `Result<T>` **e** com `Result<Int>`. Com
membros de nomes diferentes não há problema; com o mesmo nome, há:

```c
def<T> Result<T>.descrever = fn(self) Str { ... };
def    Result<Int>.descrever = fn(self) Str { ... };
```

A spec §7 diz "sem overload", e isto **não é** overload — é a mesma assinatura para
dois donos que se sobrepõem. Duas leituras:

1. **Erro** (`LAP0720`): o espaço de nomes de membro é por tipo, e `Result<Int>`
   tem dois `descrever`. Falha ruidosamente, coerente com a postura da 0.2 em
   `a < b < c` e em `LAP0502`.
2. **Mais específico vence**: precisa de uma ordem de especificidade, que é uma
   regra nova no sistema de tipos e uma fonte clássica de surpresa.

**A recomendação é 1** — erro. Faz parte de Q27.

### 23.4 Diagnósticos

```text
LAP0720  '{0}' é declarado para '{1}' e para '{2}', que se sobrepõem
LAP0721  o parâmetro genérico '{0}' não aparece no dono do membro
LAP0722  o dono do membro tem {0} argumentos genéricos, e '{1}' espera {2}
```

---

## Decisões de design

### Por que não bounds

`def<T: Comparable> Array<T>.sort` seria a forma útil, e exige bounds — que a 0.2
não tem em lugar nenhum. Introduzi-los aqui os traria para a linguagem inteira sem
a discussão que eles merecem. Fica para uma spec própria.

### Por que o dono é `TypeSyntax` desde o plano 21

`Result<Int>` no lado esquerdo de um `def` só é representável se `Owner` for
`TypeSyntax`. Guardar `string` no plano 21 obrigaria a reabrir parser, desugar e
checker aqui — daí a forma larga entrar antes de ser usada.

---

## Testes necessários

Escritos para a saída **B + 1**; se a decisão de Q27 for outra, a tabela muda.

| Teste | Fonte | Esperado |
|---|---|---|
| `Generic_AppliesToEveryInstance` | `def<T> Result<T>.isOk` | vale para `Result<Int>` e `Result<Str>` |
| `Generic_BindsTheParameter` | corpo usa `T` | `T` é `Int` num `Result<Int>` |
| `Specialized_OnlyItsInstance` | `def Result<Int>.doubled` | `LAP0701` num `Result<Str>` |
| `Overlapping_IsError` | genérico + especializado, mesmo nome | `LAP0720` |
| `UnusedParameter_IsError` | `def<T> User.m` | `LAP0721` |
| `WrongArity_IsError` | `def<T> Result<T>.m` (Result tem 2) | `LAP0722` |
| `Generic_IsFoldedByPE` | receptor conhecido | especializa como chamada comum |

---

## Critérios de conclusão

- [ ] **Q27 decidida pelo autor** — sem isso o plano não começa.
- [ ] `def<T> Result<T>.m` aplicável a toda instanciação, com `T` ligado no corpo.
- [ ] Sobreposição entre genérico e especializado reportada, não resolvida em
      silêncio.
- [ ] Q7 preservada onde ela vale: nenhuma inferência em chamada de função.
- [ ] Zero alteração de expectativa em qualquer teste anterior.

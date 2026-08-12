# Plano 23 — Membros sobre tipos genéricos

**Projeto:** `Lapis.Ast`, `Lapis.Parser`, `Lapis.TypeChecker`
**Milestone:** M15
**Spec:** [`lapislang-type-members-0.1.md`](../spec/lapislang-type-members-0.1.md) §8.1
**Depende de:** 21 (type members), 22 (instância), 04 (generics)

---

## Objetivo

`def Result<?, ?>.isOk` e `def Result<Int, ?>.maiorQue` — membros sobre tipos
genéricos, com o alcance de cada um **escrito**, não deduzido.

## Escopo

**Entra:** `?` como argumento genérico curinga, membros com dono parcialmente
aplicado, `self` sobre dono genérico, sobreposição entre declarações.

**Fica de fora:** ligar o argumento do dono a um nome utilizável no corpo
(§23.6); *where clauses* e bounds — a 0.2 não tem bounds em lugar nenhum.

---

## O que será construído

### 23.1 A decisão do autor fecha a Q27 — e a dissolve

A pergunta da Q27 era: casar `Result<Int, Error>` contra `Result<T, E>` **é**
unificação, e a Q7 diz que argumento genérico nunca é inferido. As três saídas
examinadas eram ruins: sem extensions genéricas, com unificação restrita, ou
exigindo `result.isOk<Int>()` que ninguém escreve.

A decisão do autor **não escolhe entre as três**. Ela troca a pergunta:

```c
def Result<?, ?>.isOk       = fn(self) Bool { ... };          // qualquer Result
def Result<Int, ?>.maiorQue = fn(self, v: Int) Bool { ... };  // só Result<Int, ...>
def Result.ok = fn<T>(value: T) Result<T, Error> { ... };     // membro genérico
```

> **`<>` do lado esquerdo do `=` fala do dono. `<>` do lado direito fala do
> membro.**

E `?` não é um parâmetro: é **curinga**. Ele não liga nome nenhum, então não há o
que unificar e não há o que transportar para o corpo. A Q27 não é respondida —
ela deixa de existir.

Isso também apaga a §23.1 da versão anterior deste plano, que precisava de
`def<T> Result<T>.isOk` só para dizer quais identificadores no dono eram
parâmetros. Com `?`, **nenhum** é.

### 23.2 `?` é a mesma decisão de `[T;?]`

Não é coincidência de símbolo. A leitura é literalmente a mesma:

| Forma | `?` significa |
|---|---|
| `[Int;?]` | não se sabe o tamanho (Q29) |
| `Result<?, ?>` | não se diz quais argumentos |

E a regra de atribuibilidade é a mesma, na mesma direção: **esquecer o que se
sabia é seguro; afirmar o que não se sabe, não.** `Result<Int, Error>` cabe onde
se espera `Result<?, ?>`; o contrário não.

Essa é a **terceira** regra de subtipagem da linguagem, e as três têm a mesma
forma — `Never <: T` (Q13), `[T;N] <: [T;?]` (Q29), `T<A,B> <: T<?,?>` (Q27). Vale
dizer em voz alta porque é o tipo de coerência que se perde sem alguém notar.

### 23.3 O que entra no modelo de tipos

`GenericArgument` ganha um caso curinga. É o mínimo:

```csharp
public sealed record WildcardArgument : GenericArgument;
```

Um `NamedType(Result, [Wildcard, Wildcard])` é o tipo que `self` recebe em
`def Result<?, ?>.isOk`. `TypeRelations.IsAssignableTo` ganha uma cláusula:

```csharp
// Q27: T<A,B> cabe em T<?,?>. Posição a posição — `Result<Int, ?>` aceita
// `Result<Int, Error>` e recusa `Result<Bool, Error>`.
if (target is NamedType wanted && source is NamedType actual
    && wanted.Definition.Id == actual.Definition.Id)
{
    return wanted.Arguments.Zip(actual.Arguments)
        .All(p => p.First is WildcardArgument || p.First == p.Second);
}
```

Curinga é **só de posição**, nunca de definição: `Result<?, ?>` não aceita um
`Option<Int>`. O nome do dono continua exato.

### 23.4 A tabela de membros passa a ser por padrão, não por nome

Hoje `_members` é `Dictionary<TypeDefinitionId, Dictionary<nome, MemberInfo>>`. O
valor vira uma **lista**, porque o mesmo nome pode ter várias declarações com
donos diferentes:

```csharp
private readonly Dictionary<int, Dictionary<string, List<MemberInfo>>> _members;
```

`MemberInfo` ganha o padrão do dono (`ImmutableArray<GenericArgument>`, com
curingas). A resolução em `CheckField` filtra os candidatos pelo tipo do receptor
usando §23.3.

### 23.5 Sobreposição é erro, e agora é **visível**

```c
def Result<?, ?>.descrever   = fn(self) Str { ... };
def Result<Int, ?>.descrever = fn(self) Str { ... };   // LAP0720
```

A Q27 já recomendava erro em vez de regra de especificidade. A decisão do autor
melhora o argumento: com `?` escrito, a sobreposição está **na fonte**. Não é uma
consequência sutil de duas declarações que parecem diferentes — são dois padrões
que qualquer leitor vê que se cruzam.

A checagem é a que a §23.3 já dá: dois padrões se sobrepõem quando existe algum
tipo que casa com os dois, e com curinga isso é comparação posição a posição.

`LAP0720` é reportado na **segunda** declaração, com nota apontando a primeira —
mesma forma de `LAP0702`.

### 23.6 O que `?` **não** dá, e por quê

```c
def Result<?, ?>.unwrapOr = fn(self, fallback: ???) ??? { ... };
```

Não há nome para o argumento do dono, então não há como escrever o tipo de
`fallback`. Isso é limitação real, e é o preço de `?` ser curinga em vez de
binder — que é justamente o que dissolve a Q27.

**Recomendação: aceitar a limitação nesta milestone.** O que se escreve com
curinga é o que não olha para dentro: `isOk`, `isFail`, `descrever`. O que precisa
do argumento tem duas saídas, ambas adiáveis:

1. um membro **genérico** que recebe o receptor explicitamente
   (`def Result.unwrapOr = fn<T>(r: Result<T, Error>, fallback: T) T`), que já
   funciona hoje;
2. curinga **nomeado** numa milestone futura, se a forma (1) provar ser
   insuficiente na prática.

Adiar aqui é barato porque não fecha porta: `?` continua válido no dia em que um
nome for permitido ao lado dele.

### 23.7 `self` sobre dono genérico

O M14 recusa com `LAP0295` ("genérico ainda não aceita membro de instância").
Aqui a recusa some: `self` recebe `NamedType(dono, padrão)` — com curingas onde a
declaração escreveu `?`.

A consequência cai de graça da §23.3: o corpo só consegue fazer com `self` o que
não depende dos argumentos. `self.value` sobre `Result<?, ?>` não compila, e a
mensagem é a de campo desconhecido, que é a verdade.

### 23.8 Membro genérico — o lado direito

```c
def Result.ok = fn<T>(value: T) Result<T, Error> { ... };

var iRes = Result.ok<Int>(10);      // Result<Int, Error>
```

Isto **já funciona** hoje: o membro é um valor comum ligado a uma `fn` genérica, e
`Result.ok<Int>(10)` é `Call(Instantiate(Field(Result, ok), <Int>))` — a cadeia
pós-fixa do M4 cobre. O plano só precisa de teste e de exemplo, não de código.

Vale registrar porque é o meio da regra do autor: os dois lados da igualdade podem
ter `<>`, e eles falam de coisas diferentes.

### 23.9 Diagnósticos

```text
LAP0720  '{0}' é declarado para {1} e para {2}, que se sobrepõem
LAP0721  '?' só é válido como argumento genérico do dono de um membro
LAP0722  o dono do membro tem {0} argumentos genéricos, e '{1}' espera {2}
```

`LAP0721` marca a fronteira: `?` **não** é um tipo. `def x: Result<?, ?> = ...`
não compila, e nem `fn(r: Result<?, ?>)`. Ele existe só na posição de dono, onde
significa "esta declaração vale para qualquer coisa aqui" — e permiti-lo como tipo
de valor seria um `Any` estrutural pela porta dos fundos.

---

## Decisões de design

### Por que `?` e não um parâmetro nomeado

Um parâmetro nomeado (`def Result<T, E>.isOk`) obrigaria a distinguir "`T` é
parâmetro" de "`Int` é tipo" — que era o nó da versão anterior deste plano, e a
razão de ela propor `def<T>`. `?` não tem esse problema porque não nomeia nada.

O custo é a §23.6. A troca é boa: uma limitação declarada vale mais do que uma
regra de inferência que ninguém consegue prever.

### Por que a sobreposição não escolhe a mais específica

Especificidade é uma regra que o leitor precisa simular de cabeça para saber qual
membro roda. Erro é uma regra que ele não precisa saber. A 0.2 já faz a mesma
escolha em `a < b < c`, e por isso mesmo.

---

## Testes necessários

| Teste | Fonte | Esperado |
|---|---|---|
| `Wildcard_AppliesToAnyInstance` | `Result<?, ?>.isOk` em `Result<Int, Error>` | compila |
| `Wildcard_IsPositional` | `Result<Int, ?>.m` em `Result<Bool, Error>` | `LAP0701` |
| `Wildcard_DoesNotCrossDefinitions` | `Result<?, ?>.m` em `Option<Int>` | `LAP0701` |
| `Overlap_IsAnError` | `Result<?, ?>.d` e `Result<Int, ?>.d` | `LAP0720` |
| `Disjoint_PatternsCoexist` | `Result<Int, ?>.d` e `Result<Bool, ?>.d` | compila |
| `Wildcard_IsNotAType` | `def x: Result<?, ?> = y;` | `LAP0721` |
| `Wildcard_ArityIsChecked` | `Result<?>.m` | `LAP0722` |
| `Self_OnGenericOwner` | `fn(self)` em `Result<?, ?>` | `self: Result<?, ?>` |
| `Self_CannotReadArgumentDependentField` | `self.value` | erro de campo |
| `GenericMember_OnTheRight` | `Result.ok = fn<T>(...)` | já funciona; teste e exemplo |
| corpus | equivalência do PE | inalterada |

---

## Critérios de conclusão

- [ ] `?` como argumento genérico de dono, e **só** ali (`LAP0721`).
- [ ] `T<A,B> <: T<?,?>`, posição a posição, numa direção só.
- [ ] Tabela de membros por padrão, com resolução pelo tipo do receptor.
- [ ] Sobreposição virando `LAP0720`, sem regra de especificidade.
- [ ] `self` sobre dono genérico, sem o `LAP0295` do M14.
- [ ] Membro genérico (`fn<T>` do lado direito) coberto por teste e exemplo.
- [ ] Zero alteração de expectativa em qualquer teste anterior.

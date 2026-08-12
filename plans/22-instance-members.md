# Plano 22 — Métodos de instância e `self`

**Projeto:** `Lapis.TypeChecker`, `Lapis.Evaluator`, `Lapis.Ast`
**Milestone:** M14
**Spec:** [`lapislang-type-members-0.1.md`](../spec/lapislang-type-members-0.1.md) §3, §5
**Depende de:** 21 (type members)

---

## Objetivo

`fn(self, ...)` como membro de instância, e `receptor.m(args)` resolvido pelo
**tipo estático** do receptor.

## Escopo

**Entra:** a regra do `self`, resolução de chamada por instância, separação entre
acesso estático e por instância, e os erros que a separação produz.

**Fica de fora:** extensions genéricas (plano 23); mutação de campo (spec §9).

---

## O que será construído

### 22.1 A regra do `self`

> Numa função ligada por `def T.m`, um primeiro parâmetro chamado `self` e **sem
> anotação** recebe o tipo `T`.

```c
def User.hello  = fn(self) Void { ... };        // self: User — instância
def User.create = fn() User { ... };            // sem self  — estático
def User.of     = fn(self: Str) User { ... };   // anotado   — parâmetro comum, estático
```

Três coisas precisam ser ditas em voz alta:

**A ausência de anotação é o gatilho.** É a única exceção à exigência de anotar
parâmetro (spec §26), e ela é estreita de propósito: fora de um `def T.m`,
`fn(self)` continua sendo erro, porque não há de onde tirar o tipo.

**`self` não é palavra reservada.** `def self = 1;` continua válido, e um
parâmetro chamado `self` numa função comum é um parâmetro chamado `self`.
Reservá-la quebraria programa válido sem comprar nada — mesma decisão de `label`
ser contextual (M6).

**O nome é a assinatura.** Renomear o primeiro parâmetro para `this` muda o membro
de instância para estático. É frágil, e é o preço de não ter sintaxe de método;
o diagnóstico de acesso errado (`LAP0711`) é que torna o erro legível.

### 22.2 Resolução por instância

Em `CheckField`, quando o alvo é `NamedType`:

1. campo do struct (comportamento atual);
2. membro de instância do tipo;
3. `LAP0701`.

Em `CheckCall`, quando o callee é um `CoreField` resolvido como membro de
instância, o **receptor entra como primeiro argumento**:

```csharp
public sealed record MemberCallResolution(
    string SyntheticName,
    CoreExpr Receiver,
    FunctionType Instantiated) : Resolution;
```

A aridade e os tipos são checados com o receptor já na posição 0, então
`user.rename("x")` compara contra `fn(User, Str) Void` — e a mensagem de aridade
conta o receptor, senão "esperados 2 argumentos, fornecidos 1" seria mentira para
quem escreveu um argumento.

### 22.3 A separação estático × instância

| Escrita | Membro | Resultado |
|---|---|---|
| `user.hello()` | instância | ok |
| `User.hello()` | instância | `LAP0710` — exige uma instância |
| `User.create()` | estático | ok |
| `user.create()` | estático | `LAP0711` — é estático |

Sem essa separação, `User.hello` seria ambíguo entre "o membro" e "a função não
aplicada", e a segunda leitura tornaria `user.hello()` e `User.hello(user)` duas
formas do mesmo — dois caminhos para a mesma coisa, que é o que este projeto evita.

### 22.4 O receptor é avaliado uma vez

```c
proximo().hello()
```

O receptor é uma expressão qualquer, e pode ter efeito. A resolução **não** pode
duplicá-lo: o evaluator avalia o receptor uma vez e passa o valor. Vale um teste
próprio, porque é o erro clássico de desugaring de método.

### 22.5 Diagnósticos

```text
LAP0710  o membro '{0}' de '{1}' exige uma instância
LAP0711  o membro '{0}' de '{1}' é estático
LAP0712  'self' sem anotação só é válido no primeiro parâmetro de um membro
```

---

## Decisões de design

### Por que dispatch estático e nada mais

O tipo estático do receptor decide, sempre. Não há virtual dispatch porque não há
subtipagem além de `Never <: T` (Q13) — não existe "outro tipo que também é
`User`". A escolha não custa expressividade aqui; ela só a custaria com herança,
que a spec §2 recusa.

### Por que o receptor não vira açúcar sintático puro

Seria possível reescrever `user.hello()` para `User@hello(user)` na Surface AST,
antes do desugar. Não dá: a reescrita precisa do **tipo** de `user`, que só existe
depois do checker. É a mesma razão de member resolution ser type checking (plano
21 §21.1), e a razão de a proposta original ter proposto uma fase que não cabe.

---

## Testes necessários

### `self`

| Teste | Fonte | Esperado |
|---|---|---|
| `Self_GetsTheOwnerType` | `def User.hello = fn(self) Void` | `self: User` |
| `Self_Annotated_IsAPlainParameter` | `fn(self: Str)` | membro estático |
| `Self_NotFirst_IsAPlainParameter` | `fn(x: Int, self)` | `LAP0712` |
| `Self_OutsideMember_IsError` | `def f = fn(self) Void` | `LAP0712` |
| `Self_IsNotReserved` | `def self = 1;` | compila |

### Invocação

| Teste | Fonte | Esperado |
|---|---|---|
| `Instance_Call` | `user.hello()` | executa com `self = user` |
| `Instance_WithArguments` | `user.saudar("oi")` | receptor em 0 |
| `Instance_ArityCountsTheReceiver` | argumento a menos | mensagem conta o receptor |
| `Static_OnInstance_IsError` | `user.create()` | `LAP0711` |
| `Instance_OnType_IsError` | `User.hello()` | `LAP0710` |
| `Receiver_IsEvaluatedOnce` | receptor com efeito | efeito uma vez só |
| `Receiver_IsAnyExpression` | `arr[0].hello()` | resolve pelo tipo do elemento |

### Interação

| Teste | Asserção |
|---|---|
| `Field_StillWins_OverMember` | campo e membro homônimos: campo primeiro, e a colisão é reportada |
| `Instance_OnPreludeType` | `def Result.orDefault` chamado por instância |
| `Instance_IsFoldedByPE` | `user.hello()` sobre `user` conhecido especializa como chamada comum |

---

## Critérios de conclusão

- [x] `fn(self)` recebendo o tipo dono, com `self` **não** reservada.
- [x] `receptor.m(args)` resolvido pelo tipo estático, receptor no argumento 0.
- [x] `LAP0710`/`LAP0711` separando as duas formas de acesso.
- [x] Receptor avaliado exatamente uma vez.
- [x] Zero alteração de expectativa em qualquer teste anterior.

---

## O que a implementação mudou no plano

### O receptor não entra na árvore, entra na resolução

§22.2 propunha uma `MemberCallResolution` com o receptor dentro. Ficou como um
campo opcional da `CallResolution` que já existia, e a diferença importa: uma
resolução só para chamada por instância significaria duas resoluções possíveis
num `CoreCall`, e o evaluator teria de perguntar as duas. Assim ele lê uma, e o
receptor é `null` no caso comum.

O que **não** mudou é a razão de fundo: o receptor não pode ser reescrito para
dentro de `CoreCall.Arguments`, porque a reescrita precisaria do tipo dele — que
só existe no checker. É a mesma razão de member resolution ser type checking, e é
o que mantém o receptor avaliado uma vez.

### `CoreParameter.Type` teve de ficar anulável

Não estava previsto. `self` é o único parâmetro sem anotação, e representá-lo com
um tipo sintético (`?`) faria o checker distinguir "não anotado" de "anotado com
um nome inválido" por comparação de string. Anulável diz a mesma coisa no tipo,
e os três printers ganharam o caso — que é justamente o que faz um membro de
instância sobreviver ao round-trip.

### A tabela de membros vazou para o `FreeVariables`

Terceira vez que o mesmo defeito aparece, e a última posição em que ele cabia.

Em `u.saudar()` o dono é o **tipo de `u`**, e a árvore não carrega isso: o
`CoreField` tem `Name = "saudar"` e um alvo que é a variável `u`. A leitura
sintática do M13 somava `u#saudar`, que não é binding de ninguém, e o `Let` do
membro morria.

`FreeVariables` passou a receber a `TypedProgram` e a ler a `MemberResolution`,
que diz o nome exato. Sem a tabela — o PE pode rodar sobre o próprio residual sem
re-checar — a resposta é conservadora: um nome de membro nunca é dado como
livre-de-uso. Errar para o lado de manter o binding custa uma linha no residual;
errar para o outro apaga código vivo.

### Dono genérico ainda não aceita `self`

`def Caixa<T>.ler = fn(self)` exigiria dizer quais argumentos `self` carrega, e é
exatamente o que o plano 23 resolve. Até lá, `LAP0295` — o mesmo código de
"genérico precisa de argumentos" —, porque a causa é a mesma.

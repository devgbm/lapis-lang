# LapisLang — Type Members e Extension Methods

**Versão:** 0.1 (proposta) · **Requer:** [`lapislang-0.2.md`](lapislang-0.2.md) ≥ 0.2.3

> Membros associados a tipos: métodos de instância, métodos estáticos, valores
> associados e extensions — inclusive sobre tipos genéricos e sobre tipos que o
> programa não declarou.
>
> A feature **não** introduz orientação a objetos. `user.hello()` é
> `hello(user)` com outra sintaxe, resolvido estaticamente. Não há classes,
> herança, `this`, virtual dispatch, vtable nem overload.

---

## Changelog em relação à proposta original

Esta spec nasceu de um documento escrito fora do contexto do projeto — mesma
situação da spec de macros. As mudanças abaixo são adaptações **necessárias**, não
preferências de estilo: cada uma corrige um conflito real com a 0.2 já
implementada.

| # | Mudança | Motivo |
|---|---|---|
| 1 | `fn(User self)` / `fn(self, Str name)` → **`fn(self)` / `fn(self, name: Str)`** | a 0.2 anota parâmetro com `nome: Tipo`, nunca `Tipo nome` (§26) |
| 2 | `User { name: "x" }` → **`.User { name: "x" }`** | construção leva ponto inicial (Q2) |
| 3 | `Result<Int>.Fail` → **`Result<Int>.Err`** | a variante do prelude se chama `Err` |
| 4 | Braços de `match` com `{ }` → **`=>`** | a 0.2 usa `padrão => expressão` (§22) |
| 5 | `self.name = name;` **removido do escopo** | a 0.2 não tem mutação de campo; ver §9 |
| 6 | Pipeline do §24 da proposta **substituído** | member resolution é type checking, não uma fase antes dele; ver §10 |
| 7 | Resolução `Type.membro` ganha regra de precedência com variantes de enum | `Color.Red` já é acesso a membro sobre `MetaType` |
| 8 | Extensions genéricas separadas em fase própria | casar `Result<Int>` com `Result<T>` é inferência, e Q7 diz que argumento genérico é sempre explícito |

---

# 1. Princípio

```text
Type + Member → Function/Value
```

Um membro é uma função ou um valor **associado a um tipo em tempo de
compilação**. Nada é guardado na instância, nada é despachado em runtime.

```c
user.hello()
```

é semanticamente idêntico a chamar a função que `def User.hello` declarou,
passando `user` como primeiro argumento. A diferença é sintática e de resolução,
não de execução.

---

# 2. Declaração

```text
def <Tipo>.<nome> = <expressão>;
```

```c
def User.defaultAge = 30;                              // valor associado

def User.create = fn() User { ... };                   // membro estático

def User.hello = fn(self) Void {                       // membro de instância
    print("Olá " + self.name);
};
```

O lado direito é uma expressão comum. Não há sintaxe de método: o que distingue
um membro de instância é o **primeiro parâmetro chamar-se `self`**.

Vale a ordem de declaração (Q8): `def User.hello` exige `User` já declarado.

---

# 3. `self`

`self` não é palavra reservada nem tipo especial. A regra é estreita:

> Numa função ligada por `def T.m`, um primeiro parâmetro chamado `self` e **sem
> anotação** recebe o tipo `T`.

```c
def User.hello = fn(self) Void { ... };        // self: User — membro de instância
def User.create = fn() User { ... };           // sem self  — membro estático
def User.of = fn(self: Str) User { ... };      // anotado   — parâmetro comum, membro estático
```

A ausência de anotação é o que dispara a regra, e é a **única** exceção à
exigência de anotar parâmetros (§26). Fora de um `def T.m`, `fn(self)` continua
sendo erro: não há de onde tirar o tipo.

---

# 4. Membro estático e valor associado

Qualquer membro sem `self` é estático. Um valor associado é o caso em que a
expressão não é função nenhuma — não há diferença de tratamento.

```c
def User.defaultAge = 30;
def User.create = fn() User { ... };

var idade = User.defaultAge;
var novo = User.create();
```

---

# 5. Invocação

| Forma | Resolve por |
|---|---|
| `receptor.m(args)` | tipo **estático** de `receptor` |
| `Tipo.m(args)` | o tipo nomeado |
| `Tipo.m` | idem, sem chamar |

O receptor vira o primeiro argumento. `Tipo.m` acessa o símbolo do membro
diretamente.

**Um membro de instância não é acessível sem receptor** (`User.hello` é erro), e
**um membro estático não é acessível por instância** (`user.create()` é erro). Sem
essa separação, `Tipo.m` seria ambíguo entre "o membro estático" e "a função de
instância não aplicada".

---

# 6. Conflito com variantes de enum

`Color.Red` já existe: acesso a membro sobre um `MetaType`. A regra é que o espaço
é **único**:

```c
def Color = enum { Red, Green };

def Color.Red = 1;      // erro: `Red` já é variante de `Color`
```

Variante e membro não convivem sob o mesmo nome. Resolver por precedência
silenciosa esconderia a colisão de quem escreveu o segundo.

---

# 7. Sem overload

Um tipo tem **um** membro por nome, qualquer que seja a assinatura. Duas
declarações de `User.create` são erro, mesmo com parâmetros diferentes — e é o que
torna a resolução determinística sem regra de escolha.

Um membro também não colide com um `def` comum de mesmo nome: `def hello` e
`def User.hello` são símbolos distintos, porque o segundo só é alcançável através
de um tipo.

---

# 8. Extensions

Um membro não precisa estar junto da declaração do tipo, e o tipo não precisa ser
do programa:

```c
def Result.orDefault = fn(self, fallback: Int) Int { ... };
```

Como não há sistema de módulos, não há problema de *orphan rule*: um arquivo é a
unidade, e todos os membros de um tipo são visíveis no arquivo inteiro.

## 8.1 Extensions genéricas e especializadas

```c
def Result<T>.isOk = fn(self) Bool { ... };            // vale para todo Result<T, E>
def Result<Int>.doubleOrZero = fn(self) Int { ... };   // só para Result<Int, E>
```

Casar `Result<Int>` contra `Result<T>` é inferência, e Q7 estabelece que argumento
genérico é sempre **explícito**. A tensão é real e está registrada como **Q27** —
esta é a parte da proposta que exige decisão antes de virar código.

---

# 9. O que a feature **não** traz

**Mutação de campo.** A proposta original mostrava:

```c
def User.rename = fn(self, name: Str) Void {
    self.name = name;      // NÃO — a 0.2 não tem mutação de campo
};
```

`var` reatribui um **binding**, nunca um campo, e a restrição que fez a mutação
caber (Q25) foi justamente um `var` não atravessar fronteira de função. Mutar
campo reintroduz aliasing, que o partial evaluator teria de modelar antes de
especializar qualquer coisa — o oposto do que Q25 comprou.

A forma que a linguagem suporta devolve um valor novo:

```c
def User.renamed = fn(self, name: Str) User {
    return .User { name: name, age: self.age };
};
```

**Herança, virtual dispatch, vtable, `this`, overload** — nenhum deles, por §2 da
proposta e porque nada aqui precisa.

---

# 10. Onde a resolução acontece

A proposta original pedia uma fase `Member Resolution` entre a resolução de tipos
e o desugar. Isso exigiria uma **segunda** análise de tipos antes do checker, e a
0.2 é explicitamente uma travessia única dirigida por sintaxe (§47).

A resolução de membro **é** type checking:

```text
.ls → Lexer → Parser → Macros → Desugar → Core AST → TypeChecker → Typed Core → Evaluator
                                                          │
                                                   member resolution
```

`user.hello` já é `CoreField(user, "hello")` na Core, e `user.hello()` já é
`CoreCall(CoreField(...), [])`. O checker resolve o membro no mesmo lugar onde já
resolve campo de struct e variante de enum, e registra uma `MemberResolution` —
como `VariantResolution` e `ReflectResolution` já fazem.

**A Core não ganha nó nenhum, e não há fase de lowering.** É o que torna a feature
barata: o evaluator lê a resolução e chama a função.

---

# 11. Reflection

`TypeInfo` ganha os membros:

```c
def MemberInfo = type {
    name: Str;
    kind: MemberKind;      // InstanceMethod | StaticMethod | Value
    typeName: Str;
};
```

Reflection **lê**; não declara nem remove membro. Uma `constraint` pode usar isso
para validar que um tipo tem o membro que o framework espera.

---

# 12. Macros

Uma macro pode gerar `def T.m = ...`: é sintaxe de superfície, e a expansão é
anterior a tudo isso. O que precisa de cuidado é a higiene — o nome do membro vem
do tipo e do nome escrito, não do escopo léxico da macro.

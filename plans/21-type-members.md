# Plano 21 — Type members: declaração, membros estáticos e valores

**Projeto:** `Lapis.Ast`, `Lapis.Parser`, `Lapis.Desugar`, `Lapis.TypeChecker`, `Lapis.Evaluator`
**Milestone:** M15
**Spec:** [`lapislang-type-members-0.1.md`](../spec/lapislang-type-members-0.1.md) §2, §4, §5, §6, §7
**Depende de:** 04 (parser), 05 (desugar), 06 (checker), 08 (evaluator), 17 (macros)

---

## Objetivo

`def T.m = e;` e o acesso `T.m` — membros estáticos e valores associados a um
tipo, resolvidos em tempo de compilação, sem nó novo na Core.

## Escopo

**Entra:** declaração de membro, membros estáticos, valores associados, acesso e
chamada por `Tipo.membro`, colisão com variante de enum, ausência de overload.

**Fica de fora:** `self` e métodos de instância (plano 22); extensions genéricas
(plano 23); reflection sobre membros (plano 24); mutação de campo (spec §9 — não
entra em plano nenhum).

---

## O que será construído

### 21.1 A decisão que torna a feature barata

> **Member resolution é type checking, não uma fase antes dele.**

A proposta original pedia `Name Resolution → Type Resolution → Member Resolution →
Member Desugaring → Other Desugaring → Core AST → Type Checking`. Isso exigiria uma
**segunda** análise de tipos antes do checker — e a 0.2 é, por escolha explícita,
uma travessia única dirigida por sintaxe (spec §47, plano 06).

O caminho barato já está construído:

| O que o programa escreve | O que a Core já tem |
|---|---|
| `User.defaultAge` | `CoreField(CoreVariable "User", "defaultAge")` |
| `User.create()` | `CoreCall(CoreField(...), [])` |

`TypeChecker.CheckField` já despacha em `NamedType` (campo de struct) e `MetaType`
(variante de enum). Membro é o terceiro caso, **no mesmo lugar**, e o resultado é
uma `Resolution` — exatamente como `VariantResolution`, `FieldResolution` e
`ReflectResolution` já funcionam.

**Consequência: nenhum nó novo na Core, nenhuma fase de lowering.** O evaluator lê
a resolução e avalia a função associada.

### 21.2 Surface AST

O `def` passa a aceitar um dono:

```csharp
public sealed record DefStatement(string Name, TypeSyntax? Annotation, Expression Value) : Statement
{
    /// <summary>O tipo dono, quando a declaração é `def T.m = ...`.</summary>
    public TypeSyntax? Owner { get; init; }

    public SourceSpan? OwnerSpan { get; init; }
}
```

`Owner` é `TypeSyntax` e não `string` porque o plano 23 precisará de
`Result<T>.isOk`, e mudar a forma depois custaria reabrir parser, desugar e
checker. Neste plano só `NamedTypeSyntax` sem argumentos é aceito; o resto é
`LAP0704`.

### 21.3 Parser

`ParseDefStatement` passa a aceitar `IDENT '.' IDENT` depois de `def`. A
desambiguação é de um token: depois do primeiro identificador, um `.` significa
membro; um `=` ou `:` significa `def` comum.

`var T.m = ...` é **erro** (`LAP0705`): um membro é definitivo, e um membro
mutável exigiria decidir onde vive o slot — pergunta que Q25 fechou para bindings
e que não vale reabrir aqui.

### 21.3b Atribuição a campo — a mutabilidade segue o binding

**Decisão do autor:** `mutavel.campo = e;` é **válido** quando `mutavel` é um
`var`, e inválido quando é um `def`. A mutabilidade é do binding, não da forma do
alvo.

Hoje isso nem chega ao checker — o parser responde a coisa errada:

```text
u.name = "b";
LAP0102: esperado ';' ao final da declaração
```

Uma reclamação de pontuação para um problema semântico, e a primeira coisa que
alguém escreve. `AtAssignment()` reconhece só `IDENT '='`; passa a reconhecer um
**caminho**: `IDENT ('.' IDENT)* '='`.

#### A semântica que torna isso barato

`u.name = e` **não** muda o struct no lugar. Reconstrói o valor e reatribui o
slot — atualização funcional:

```c
u = .User { name: e, /* demais campos copiados de u */ };
```

Com isso, nada do que Q25 comprou se perde: todo `Value` continua imutável, a
única coisa mutável continua sendo o slot do ambiente, `LAP0207` continua
impedindo que um `var` atravesse fronteira de função, e **não há aliasing** para o
partial evaluator modelar.

O preço é semântica de valor, e é observável — `def b = a; a.name = "y";` deixa
`b` com o valor antigo. Está na spec §9.1 porque é comportamento, não detalhe.

#### Na Core

`CoreAssign` ganha um caminho, e mais nada:

```csharp
CoreAssign(string Name, ImmutableArray<string> Path, CoreExpr Value)
```

O receptor é um **nome**, nunca expressão qualquer: `proximo().name = x` mutaria
um temporário que ninguém mais vê. O evaluator resolve o caminho, reconstrói com
`ImmutableArray.SetItem` e chama o `TryAssign` que já existe. Aninhado
(`u.endereco.rua = e`) sai de graça da mesma forma.

#### O que cada erro responde

A maior parte já tem código:

| Escrita | Código |
|---|---|
| `fixa.campo = e;` com `def fixa` | `LAP0206` — já existe |
| `mutavel.campo = <tipo errado>;` | `LAP0210` — já existe |
| `mutavel.naoExiste = e;` | `LAP0250` — já existe |
| `mutavel.membro = e;` | **`LAP0707`** — novo |

`LAP0707` existe porque a mensagem certa é específica: `hello` **existe** em
`User`, só não é campo da instância. "Campo desconhecido" seria mentira.

**`arr[i] = v` fica de fora** — ver Q28. Não é esquecimento: a atribuição é
statement e não tem como devolver `Result` fora dos limites, e Q9 eliminou os
caminhos de aborto.

### 21.4 Core: nada de novo

O desugar de `def T.m = e;` produz um `CoreLet` com um **nome sintético**
derivado do símbolo do membro, não de concatenação textual (spec §22 da proposta):

```csharp
// `MemberNames.Of(owner, name)` — determinístico e não escrevível em fonte
"User.hello"   →   "User@hello"
```

O `@` é o mesmo separador que a higiene de macro usa, e pela mesma razão: não é
lexável no meio de um identificador, então `def User_hello` escrito pelo usuário
**não** colide (spec §23 da proposta).

O desugar não resolve nada — ele só nomeia. Quem liga `User.hello` ao nome
sintético é o checker.

### 21.5 Type checker

Uma tabela de membros por definição de tipo, preenchida ao checar cada
`def T.m`:

```csharp
sealed record MemberInfo(string Name, MemberKind Kind, LapisType Type, string SyntheticName, SourceSpan Span);

enum MemberKind { Value, StaticMethod, InstanceMethod }   // InstanceMethod só no plano 22
```

Em `CheckField`, quando o alvo é `MetaType`:

1. variante de enum (comportamento atual);
2. membro do tipo;
3. `LAP0701` — o tipo não tem esse membro.

E a colisão de §6: registrar um membro cujo nome já é variante do enum é
`LAP0703`.

**A tabela é indexada pela `TypeDefinition`**, não pelo nome, para que sombrear
`Result` no programa não redirecione membros do `Result` do prelude — mesma razão
de `PreludeScope` guardar a `TypeDefinition`.

### 21.6 Evaluator

`CoreField` com `MemberResolution` avalia o nome sintético no ambiente. Como o
desugar já emitiu o `CoreLet`, o valor está lá — não há caminho especial.

### 21.7 Diagnósticos

```text
LAP0701  o tipo '{0}' não possui o membro '{1}'
LAP0702  o membro '{0}' de '{1}' já foi declarado
LAP0703  '{0}' já é variante de '{1}'
LAP0704  o dono de um membro deve ser um tipo declarado
LAP0705  um membro não pode ser 'var'
LAP0707  '{0}' é um membro de {1} e não um campo da instância
```

---

## Decisões de design

### Por que `def T.m` e não um bloco `impl`

Um bloco agruparia os membros e pouparia repetir o nome do tipo. Mas ele
introduziria um escopo novo, com perguntas próprias — o que é visível lá dentro,
`self` implícito existe, um `def` comum pode aparecer — e nada disso é necessário.
`def T.m` reaproveita o `def` que já existe e mantém a regra de que **toda
declaração é um `def`**.

### Por que o nome sintético não é concatenação textual

`User_hello` colidiria com um `def User_hello` do usuário, que é código válido
hoje. O `@` resolve, é o mesmo mecanismo da higiene de macro, e mantém o nome fora
do alcance de `FindSuggestion`.

### Por que membro não colide com `def` comum

`def hello` e `def User.hello` são símbolos distintos porque o segundo só é
alcançável através de um tipo. Fundi-los criaria uma pergunta de precedência sem
ganho.

---

## Testes necessários

### Declaração

| Teste | Fonte | Esperado |
|---|---|---|
| `Member_Value` | `def User.defaultAge = 30;` | `User.defaultAge` ⇒ 30 |
| `Member_StaticMethod` | `def User.create = fn() User {...}` | `User.create()` constrói |
| `Member_BeforeType_IsError` | membro antes do `type` | `LAP0201` (Q8) |
| `Member_OnNonType_IsError` | `def x.m = 1;` com `x: Int` | `LAP0704` |
| `Member_Var_IsError` | `var User.m = 1;` | `LAP0705` |

### Atribuição a campo

| Teste | Fonte | Esperado |
|---|---|---|
| `FieldAssign_OnVar_Works` | `var u`; `u.name = "b";` | muda |
| `FieldAssign_OnDef_IsError` | `def u`; `u.name = "b";` | `LAP0206`, não `LAP0102` |
| `FieldAssign_WrongType` | `u.name = 12;` | `LAP0210` |
| `FieldAssign_UnknownField` | `u.naoExiste = 1;` | `LAP0250` |
| `FieldAssign_ToMember` | `u.hello = fn() Void {};` | `LAP0707` |
| `FieldAssign_Nested` | `u.endereco.rua = "B";` | muda o campo aninhado |
| `FieldAssign_OnCall_IsError` | `proximo().name = "b";` | erro, não muta temporário |
| `FieldAssign_IsValueSemantics` | `def b = a; a.name = "y";` | `b.name` continua `"x"` |
| `FieldAssign_DoesNotAlias` | array de structs copiado | cópia não muda |
| `Assign_ToVar_StillWorks` | `x = 1;` com `var x` | compila |
| `Parser_NeverThrows` | `u.name =`, `u. = 1;`, `.x = 1;` | não lança |

### Resolução

| Teste | Fonte | Esperado |
|---|---|---|
| `Member_Unknown` | `User.naoExiste` | `LAP0701` |
| `Member_Duplicate` | dois `def User.create` | `LAP0702` |
| `Member_ShadowsVariant` | `def Color.Red = 1;` | `LAP0703` |
| `Member_DoesNotCollideWithPlainDef` | `def hello` + `def User.hello` | os dois valem |
| `Member_SyntheticNameIsNotWritable` | `def User_hello` + `def User.hello` | os dois valem |

### Semântica

| Teste | Asserção |
|---|---|
| `Member_IsAnOrdinaryValue` | `User.create` sem chamar é uma função |
| `Member_NoNewCoreNode` | a Core de um programa com membros não tem nó novo |
| `Member_IsFoldedByPE` | `User.defaultAge` residualiza como `30` |
| `Prelude_CanHaveMembers` | um membro declarado no prelude vale no programa |

---

## Critérios de conclusão

- [ ] `def T.m = e;` para valor e para função sem `self`.
- [ ] `T.m` e `T.m(...)` resolvidos pelo checker, com `LAP0701`–`LAP0705`.
- [ ] **Zero nós novos na Core** e nenhuma fase de lowering.
- [ ] Variante de enum e membro convivendo sem precedência silenciosa.
- [ ] `mutavel.campo = e;` funcionando por atualização funcional, com semântica de
      valor testada (`def b = a` não vê a mudança).
- [ ] `def fixa.campo = e;` respondendo `LAP0206` em vez de `LAP0102`.
- [ ] Zero alteração de expectativa em qualquer teste anterior.

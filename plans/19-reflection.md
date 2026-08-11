# Plano 19 — Reflection

**Projeto:** `Lapis.Runtime`, `Lapis.TypeChecker`, `Lapis.Macros`
**Milestone:** M10
**Spec:** [`lapislang-macros-0.1.md` §11](../spec/lapislang-macros-0.1.md)
**Depende de:** 18 (compile time), 06 (type checker), 09 (prelude)

---

## Objetivo

Expor metadados do programa — nomes, campos, variantes — como **valores comuns e
somente leitura**, disponíveis tanto em compile time quanto em runtime.

## Escopo

**Entra:** `TypeInfo` e companhia no prelude, o intrínseco `reflect`, as duas
fontes de metadados (sintática e resolvida).

**Fica de fora:** reflection sobre funções (`FunctionInfo`) e sobre AST — a
primeira versão prioriza `type` e `enum`.

---

## O que será construído

### 19.1 Os tipos, escritos na própria linguagem

No `prelude.ls`, não em C#:

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

Princípio §58.2 aplicado: nada de sistema de metadados paralelo. Um `TypeInfo` é um
struct comum e, portanto, **imutável por construção** — a 0.2 não tem mutação (§8).

> A exigência de que "valores de reflection são constantes não modificáveis" não
> precisa de regra própria: ela já vale para todo valor da linguagem. Reflection
> herda a imutabilidade em vez de pedir uma exceção.

**Um `TypeInfo` só, para struct e enum.** `kind` diz qual é; `fields` fica vazio em
enums e `variants` em structs. Dois tipos separados obrigariam `reflect` a ter
resultado dependente do argumento, complicando o checker sem ganho real.

`typeName` é `Str` e não um `TypeInfo` aninhado: tipos podem ser recursivos, e um
`TypeInfo` aninhado não teria fim. Resolver o nome é responsabilidade de quem
consome.

### 19.2 `reflect` é intrínseco do checker

```c
reflect(User)      // TypeInfo do struct
reflect(Color)     // TypeInfo do enum, com `variants` preenchido
```

O argumento tem de ser um tipo — um `MetaType`. Isso **não é expressável** na
gramática de tipos: não há como escrever a assinatura de `reflect` em LapisLang.
Ela é, portanto, um intrínseco checado especialmente, do mesmo modo que a indexação
produz `Result<T, IndexError>` sem que exista um `fn(T[], Int) Result<T, ...>`
escrito em lugar nenhum (§21).

```csharp
// TypeChecker.CheckCall, caso especial
if (IsReflectIntrinsic(node.Callee))
{
    if (argumentTypes[0] is not MetaType meta) → LAP0601
    _resolutions[node.NodeId] = new ReflectResolution(meta.Definition);
    return prelude.TypeInfoType;
}
```

Um `MetaType` **genérico não instanciado** é aceito: `reflect(Result)` descreve a
declaração, com `typeParameterNames = ["T", "E"]`. É o que uma `constraint` precisa —
nomes e aridade de variantes, não os argumentos de uma instância.

### 19.3 As duas fontes de metadados

| Fase | Fonte | Fidelidade |
|---|---|---|
| Compile time (`constraint`) | tabela de declarações da Surface AST | **sintática**: `typeName` é o tipo *como escrito* |
| Runtime | `TypeDefinition` do checker | **resolvida**: tipos já checados |

A diferença é inevitável e precisa ser dita em voz alta: durante a expansão o type
checker ainda não rodou. `reflect(Box)` numa `constraint` devolve
`typeName: "T"` para o campo `value`; em runtime, depois de `Box<Int>`, devolve
`"Int"`.

Para o que uma `constraint` precisa — **nomes de variantes, campos e aridade** — a
informação sintática basta.

**Tabela de declarações**, construída antes da expansão:

```csharp
sealed class DeclarationTable
{
    bool TryLookup(string name, out DeclaredType type);
}

sealed record DeclaredType(
    string Name,
    TypeDefinitionKind Kind,
    ImmutableArray<string> TypeParameterNames,
    ImmutableArray<FieldSyntax> Fields,
    ImmutableArray<VariantSyntax> Variants,
    SourceSpan Span);
```

Uma varredura dos `DefStatement` de topo cujo valor é `TypeExpression` ou
`EnumExpression`. É a `declarations` do `CompileContext` que a proposta §22 pedia,
agora com forma concreta.

### 19.4 Construção do valor

Em runtime, `reflect` é avaliado a partir da `ReflectResolution` que o checker
deixou:

```csharp
private Completion EvaluateReflect(CoreCall node, ...)
{
    var definition = _program.ResolutionOf<ReflectResolution>(node)!.Definition;
    return Completion.Normal(_prelude.MakeTypeInfo(definition));
}
```

`PreludeScope.MakeTypeInfo` monta o `StructValue` a partir da `TypeDefinition`, do
mesmo jeito que `MakeOk` já monta um `Result`. Nada de reflexão de C#.

### 19.5 Ganho para o partial evaluator

`reflect(User)` sobre um tipo conhecido é **inteiramente estático**: a definição não
depende de valor de runtime nenhum. O partial evaluator (plano 12, já pronto no M7)
dobra a chamada inteira em um `StructValue` literal, e o programa residual não paga
nada por usar reflection.

Isto é material de pesquisa, não bônus: é um caso limpo em que uma feature
aparentemente cara é gratuita depois da especialização, e vale medir no plano 15.

---

## Decisões de design

### Por que runtime também

A proposta original dizia "reflection não precisa existir no runtime". O autor
decidiu o contrário, e a decisão simplifica: sendo `TypeInfo` um tipo comum,
reflection em runtime **não custa nada além do que já existe** — não há caminho
especial, é um struct construído por uma nativa. Restringi-la a compile time exigiria
uma regra a mais no checker, não a menos.

### Por que somente leitura

Não há escrita a proibir: a linguagem não tem mutação. "Somente leitura" aqui
significa que reflection não *registra* símbolos nem *altera* declarações — ela lê
uma tabela que outra fase construiu. Quem transforma é macro.

### Por que não `FunctionInfo` já

Não há consumidor ainda, e funções trazem perguntas que tipos não trazem: reflectir uma
closure expõe o ambiente capturado? Uma função genérica reflete a assinatura
genérica ou a instanciada? Perguntas boas, sem consumidor ainda.

---

## Testes necessários

### Metadados de tipo

| Teste | Fonte | Esperado |
|---|---|---|
| `Reflect_Struct_Name` | `reflect(User).name` | `"User"` |
| `Reflect_Struct_Fields` | campos de `User` | nomes e tipos na ordem declarada |
| `Reflect_Struct_HasNoVariants` | `reflect(User).variants` | vazio |
| `Reflect_Enum_Variants` | `reflect(Color).variants` | `Red`, `Green`, `Blue` |
| `Reflect_Enum_VariantArity` | `Result` | `Ok` e `Err` com aridade 1 |
| `Reflect_Enum_PayloadTypes` | `Result` | `Ok.payloadTypeNames == ["T"]` |
| `Reflect_Generic_TypeParameterNames` | `reflect(Result)` | `["T", "E"]` |
| `Reflect_Kind` | struct vs. enum | `TypeKind.Struct` / `TypeKind.Enum` |

### Checagem

| Teste | Fonte | Esperado |
|---|---|---|
| `Reflect_NonType_IsError` | `reflect(42)` | `LAP0601` |
| `Reflect_Variable_IsError` | `reflect(x)` com `x: Int` | `LAP0601` |
| `Reflect_ResultType` | `reflect(User)` | tipa como `TypeInfo` |
| `Reflect_FieldAccess` | `reflect(User).fields[0]` | `Result<FieldInfo, IndexError>` |

O último confirma que reflection não escapa das regras da linguagem: indexar um
array de metadados devolve `Result` como qualquer outro (§21).

### Runtime

| Teste | Fonte | Saída |
|---|---|---|
| `Reflect_AtRuntime_Prints` | `print(reflect(Color).name);` | `Color` |
| `Reflect_IsImmutable` | valor não modificável | herdado de §8 |
| `Reflect_TwoCalls_AreEqual` | `reflect(User) == reflect(User)` | `true` |

### Compile time

| Teste | Asserção |
|---|---|
| `Reflect_InConstraint_SeesDeclarations` | `constraint` enxerga `Color` |
| `Reflect_InConstraint_IsSyntactic` | `typeName` é o tipo como escrito |
| `Reflect_UndeclaredType_InConstraint` | `LAP0602` |
| `Reflect_DeclaredLater_IsNotVisible` | ordem de declaração respeitada (Q8) |

### Partial evaluation (M6+)

| Teste | Asserção |
|---|---|
| `Reflect_IsFoldedByPE` | `reflect(User).name` residualiza como `"User"` |

---

## Critérios de conclusão

- [x] `TypeInfo` e companhia declarados em `prelude.ls`, não em C#.
- [x] `reflect` funcionando nas duas fases, com a diferença de fidelidade testada.
- [x] `reflect` de não-tipo rejeitado com código e span.
- [x] `Reflect_IsFoldedByPE` verde quando o M6 existir.
- [x] Nenhuma API de mutação exposta.

---

## O que a implementação mudou no plano

### 1. `reflect(Box<Int>)` descreve a instância

O §19.3 promete que em runtime, "depois de `Box<Int>`", o campo `value` tem
`typeName: "Int"`. Isso não sai de graça: a `TypeDefinition` guarda a
**declaração**, onde o campo tem tipo `T`. Foi preciso aplicar os argumentos
genéricos escritos aos tipos dos campos e das variantes.

A substituição já existia — `TypeSubstitution`, do M4 —, mas morava em
`Lapis.TypeChecker`. Ela é uma operação **pura sobre o modelo de tipos**, e o
checker foi só quem precisou dela primeiro; mudou para `Lapis.Ast.Types`, onde o
runtime também a alcança. Nenhum comportamento mudou com a mudança de casa.

O resultado é que as duas leituras convivem, e as duas são legítimas:
`reflect(Box)` descreve a declaração, `reflect(Box<Int>)` a instância.

### 2. Uma captura de `Identifier` resolve para o tipo que ela nomeia

Não estava no plano, e sem isso reflection em compile time seria curiosidade:

```c
macro exige_variantes
    match Identifier:nome
    constraint { ... reflect(nome) ... }
```

Sem a resolução, `reflect(nome)` procuraria um tipo literalmente chamado `nome`.
A macro que valida **o tipo que recebeu** — o uso inteiro de reflection numa
constraint — não teria como ser escrita.

Não é reflection sobre AST, que continua fora de escopo: a captura é um nome, e
`reflect` já opera sobre nomes. O que se faz é resolvê-lo.

### 3. O `IConstraintRunner` recebe o span da invocação

Q8 exige que uma macro só enxergue os tipos declarados **acima** dela, e decidir
isso pede saber onde ela foi invocada. O span entrou na assinatura do runner (M9),
e o ambiente de compile time passou a ser montado por invocação em vez de uma vez
por compilação.

### 4. O PE ganhou "conhecido, mas sem forma sintática"

O §19.5 diz que o partial evaluator "dobra a chamada inteira em um `StructValue`
literal". Isso contradiz uma decisão já tomada no M7: struct e enum **não** são
residualizáveis, porque a expressão que os reconstrói cita um nome de tipo que
pode estar sombreado no ponto de emissão.

As duas coisas se conciliam sem abrir mão de nenhuma: `DynamicResult` ganhou um
`Opaque`, o valor que o PE conhece mas não sabe escrever. O residual continua
sendo `reflect(User)`; a projeção `reflect(User).name` dobra para `"User"`, que é
um `Str` e portanto escrevível. O critério `Reflect_IsFoldedByPE` fica verde sem
que um literal de struct seja emitido em lugar nenhum.

O mecanismo é geral, não específico de reflection: qualquer valor conhecido e
opaco passa a poder ser projetado. `[10,20,30][1]` continua não dobrando — falta
o outro lado, que é o plano 14.

---

## O que ficou de fora

**`reflect` sobre expressões e sobre a AST.** Uma captura `Expression:e` continua
invisível na constraint. Ler uma árvore como valor exige um modelo de metadados de
AST, que é uma superfície inteira, e não há consumidor.

**`FunctionInfo`.** Como o plano já dizia: sem consumidor, e com perguntas por
responder — reflectir uma closure expõe o ambiente capturado? Uma função genérica
reflete a assinatura genérica ou a instanciada?

**Contar elementos de um array.** `reflect(T).variants` é um array, e a linguagem
ainda não tem como perguntar o tamanho de um: `array_length` está previsto no
plano 09 mas nunca foi implementado, e não é deste plano implementá-lo. Uma
constraint que queira "tem pelo menos uma variante" escreve
`match info.variants[0] { Result.Ok(_) => ..., Result.Err(_) => ... }`, que é o
que o caso de conformidade faz.

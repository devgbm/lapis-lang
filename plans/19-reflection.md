# Plano 19 — Reflection

**Projeto:** `Lapis.Runtime`, `Lapis.TypeChecker`, `Lapis.Macros`
**Milestone:** M13
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
primeira versão prioriza `type` e `enum`, que é o que `@match` precisa.

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
declaração, com `typeParameterNames = ["T", "E"]`. É o que `@match` precisa —
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

Para o que a expansão precisa — **nomes de variantes e aridade de carga** — a
informação sintática basta, e é exatamente por isso que `@match` funciona
(plano 20).

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
depende de valor de runtime nenhum. O partial evaluator (plano 12) dobra a chamada
inteira em um `StructValue` literal, e o programa residual não paga nada por usar
reflection.

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

`@match` não precisa, e funções trazem perguntas que tipos não trazem: reflectir uma
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

- [ ] `TypeInfo` e companhia declarados em `prelude.ls`, não em C#.
- [ ] `reflect` funcionando nas duas fases, com a diferença de fidelidade testada.
- [ ] `reflect` de não-tipo rejeitado com código e span.
- [ ] `Reflect_IsFoldedByPE` verde quando o M6 existir.
- [ ] Nenhuma API de mutação exposta.

# 05 — Desugar: Surface AST → Core AST

**Milestone:** M1 → M3 · **Depende de:** 02, 04 · **Projeto:** `Lapis.Desugar`

Corresponde à Etapa 6 da spec (§48), antecipada para M1 (ver justificativa em
`README.md` → "Ordem de execução").

---

## Objetivo

Reduzir a linguagem de superfície à Core AST de 16 nós do plano 02, de forma
**puramente sintática**: sem resolver nomes, sem consultar tipos, sem executar
nada (spec §36).

---

## Escopo

**Entra:** as transformações listadas em §5.2, geração de nomes frescos,
preservação de spans.

**Fica de fora:** resolução de nomes (plano 06), otimização de qualquer tipo — o
desugar **não** faz constant folding, mesmo quando é óbvio; isso é trabalho do
partial evaluator (spec §58: "otimizações devem ser consequência da análise").

Única exceção permitida, e ela é normalização e não otimização: `Unary(Negate,
IntLiteral(n))` → `Literal(-n)`, para que `-10` seja um literal na Core (spec §6
descreve `-10` como literal). Documentada e testada como tal.

---

## O que será construído

### 5.1 API

```csharp
public sealed class Desugarer
{
    public static CoreProgram Desugar(SourceFile file, DiagnosticBag diagnostics);
}
```

Emite diagnósticos apenas para construções sintaticamente válidas mas
semanticamente impossíveis de traduzir (hoje: nenhuma). A assinatura já aceita o
bag para não quebrar depois.

### 5.2 Regras de tradução

Notação: `D[e]` = desugar de `e`.

#### Programa e sequenciamento

O arquivo inteiro vira **uma** expressão Core (spec §3: statements top-level
avaliados em ordem):

```text
D[ def x = e; rest... ]      = Let(x, D[e], D[rest...])
D[ e; rest... ]              = Let(_k, D[e], D[rest...])       // IsSynthetic = true
D[ <fim do arquivo> ]        = Literal(Unit)
```

`_k` é um nome fresco garantidamente não colidível (contador global, prefixo
`$tmp`, que o lexer não consegue produzir — ver §5.4).

#### Blocos

```text
D[ { s1; s2; tail } ]        = mesma recursão acima, com a cauda no lugar do `Literal(Unit)` final
D[ { s1; s2; } ]             = ... Let($tmpN, D[s2], Literal(Unit))
```

#### Funções

```text
D[ fn<G>(p: T, ...) R { body } ] = Lambda(G, [(p,T),...], R ?? Void, D[body])
```

O corpo **não** ganha `return` implícito (spec §12). O corpo é apenas a
expressão do bloco; o valor dessa expressão é descartado — quem produz o
resultado da função é `Return`. Para funções `Void` que caem no fim do corpo, o
evaluator devolve `Void` (plano 08).

#### `return`

```text
D[ return e; ]               = Return(D[e])
D[ return; ]                 = Return(null)
```

#### `if`

`Else` sempre presente na Core (elimina um caso de `null` em todo consumidor):

```text
D[ if c { t } ]              = If(D[c], D[t], Literal(Unit))
D[ if c { t } else { e } ]   = If(D[c], D[t], D[e])
D[ if c { t } else if ... ]  = If(D[c], D[t], D[if ...])
```

#### Operadores lógicos (curto-circuito) — Q4

```text
D[ a && b ]                  = If(D[a], D[b], Literal(false))
D[ a || b ]                  = If(D[a], Literal(true), D[b])
```

Isso mantém `&&`/`||` fora da Core **e** preserva o curto-circuito — que é
semanticamente relevante para o PE (não se pode reduzir `b` se `a` é falso).

#### Arrays, índice, campo, construção

Traduções 1-para-1: `Array`, `Index`, `Field`, `Construct`.

`Index` **não** é expandido aqui para uma checagem explícita de limites. A
checagem é semântica do nó `Index` (spec §41) e vive no evaluator; expandi-la no
desugar exigiria referenciar `Result` antes da resolução de nomes e tornaria o
BCE do plano 14 uma análise sobre `If` em vez de uma decisão sobre `Index`.

#### `match`

Permanece na Core como `CoreMatch` (a spec §24 o lista; §22 diz que *poderia* ser
desugared). Os padrões são normalizados:

```text
D[ p => body ]               = Arm(N[p], D[body])
N[ _ ]                       = WildcardPat
N[ x ]                       = IdentPat(x)             // binding ou variante — decide o checker
N[ A.B(p1, p2) ]             = VariantPat(["A","B"], [N[p1], N[p2]])
N[ literal ]                 = LiteralPat(const)
```

Motivo de manter `Match`: desugará-lo exigiria primitivas `enum_tag` e
`enum_payload`, que aumentariam o runtime — contra a spec §58 ("runtime mínimo").
Um plano futuro pode inverter essa decisão sem afetar nada acima do desugar.

#### `type` e `enum`

1-para-1 para `CoreTypeDef` / `CoreEnumDef`, preservando parâmetros genéricos.

#### Normalização de literais negativos

```text
D[ -(IntLiteral n) ]         = Literal(Int(-n))
D[ -(FloatLiteral f) ]       = Literal(Float(-f))
D[ -e ]                      = Unary(Negate, D[e])
```

### 5.3 Preservação de spans

Todo nó Core recebe o span do nó Surface que o originou. Nós **introduzidos**
(o `Let` sintético de sequenciamento, o `Literal(Unit)` do `else` implícito, o
`Literal(false)` de `&&`) recebem o span da construção-pai, nunca
`SourceSpan.Synthetic` — senão diagnósticos do checker sobre um `else` implícito
apontariam para lugar nenhum.

### 5.4 Nomes frescos

```csharp
sealed class FreshNameGenerator { public string Next(string hint = "tmp"); }  // "$tmp0", "$tmp1", ...
```

O prefixo `$` não é produzível pelo lexer (§7 da spec: identificadores começam
com letra ou `_`), então um nome fresco nunca captura nem é capturado por um nome
do usuário. O mesmo gerador é reutilizado pelo partial evaluator (plano 13) ao
especializar funções.

---

## Decisões de design

**Desugar antes do type checker.** O checker opera sobre a Core (spec §26 e §36:
`Core AST → Typed Core AST`). Isso significa que os diagnósticos do checker
precisam apontar para spans da Core — daí a regra §5.3 ser rígida.

**Desugar é total.** Toda `SourceFile` sintaticamente válida tem tradução. Não há
caminho "não suportado".

**Desugar é determinístico e idempotente na numeração.** Para o mesmo arquivo, os
nomes frescos e `NodeId`s são os mesmos em toda execução — snapshots estáveis.

---

## Testes necessários

Snapshots via `CoreSExprPrinter`.

### Sequenciamento e `Let`

| Teste | Fonte | Esperado |
|---|---|---|
| `Desugar_TopLevel_Defs_NestAsLets` | `def a=1; def b=2;` | `Let(a,1,Let(b,2,Unit))` |
| `Desugar_TopLevel_Expr_UsesSyntheticLet` | `f();` | `Let($tmp0, call, Unit)` com `IsSynthetic` |
| `Desugar_EmptyFile` | `` | `Literal(Unit)` |
| `Desugar_Block_WithTail` | `{ def x=1; x }` | `Let(x,1,Var(x))` |
| `Desugar_Block_WithoutTail` | `{ f(); }` | termina em `Literal(Unit)` |
| `Desugar_Block_Empty` | `{}` | `Literal(Unit)` |
| `Desugar_NestedBlocks_PreserveScoping` | bloco dentro de bloco | aninhamento correto |
| `Desugar_FreshNames_DoNotCollide` | `def $tmp0 = 1;` é **inválido** no lexer | teste garante o prefixo inutilizável |
| `Desugar_FreshNames_AreDeterministic` | desugar 2x ⇒ S-expressions idênticas |

### Funções e `return`

| Teste | Fonte | Esperado |
|---|---|---|
| `Desugar_Fn_NoImplicitReturn` | `fn() Int { 1 }` | corpo é `Literal(1)`, **sem** `Return` |
| `Desugar_Return_Value` | `return a+b;` | `Return(Binary(+,a,b))` |
| `Desugar_Return_Empty` | `return;` | `Return(null)` |
| `Desugar_Fn_MissingReturnType_IsVoid` | `fn(){ }` | `ReturnType == Void` |
| `Desugar_Fn_EarlyReturn_InIf` | spec §12 `abs` | snapshot |
| `Desugar_NestedFn_ReturnBindsToInner` | `fn(){ return fn(){ return 1; }; }` | dois `Return` em lambdas distintas |

### Condicionais e lógicos

| Teste | Fonte | Esperado |
|---|---|---|
| `Desugar_If_NoElse_GetsUnitElse` | `if c { }` | `If(c, ..., Unit)` |
| `Desugar_ElseIf_Chains` | `if a {} else if b {} else {}` | `If(a,_,If(b,_,_))` |
| `Desugar_AndAlso_BecomesIf` | `a && b` | `If(a, b, false)` |
| `Desugar_OrElse_BecomesIf` | `a \|\| b` | `If(a, true, b)` |
| `Desugar_LogicalOps_ShortCircuit_Preserved` | teste de integração com efeito colateral (`print`) confirma que o lado direito não é avaliado |
| `Desugar_NoAndAlsoOrElse_InCore` | property sobre `examples/`: nenhum `CoreBinary` com `AndAlso`/`OrElse` |

### Literais e negação

| Teste | Fonte | Esperado |
|---|---|---|
| `Desugar_NegativeInt_IsLiteral` | `-10` | `Literal(Int(-10))` |
| `Desugar_NegativeFloat_IsLiteral` | `-0.5` | `Literal(Float(-0.5))` |
| `Desugar_NegateVariable_StaysUnary` | `-x` | `Unary(Negate, Var(x))` |
| `Desugar_DoubleNegation_NotFolded` | `- -x` | dois `Unary` (não é otimização) |
| `Desugar_NoConstantFolding` | `1+2` | `Binary(+,1,2)`, **não** `Literal(3)` |

### Arrays, índice, membro, construção

| Teste | Asserção |
|---|---|
| `Desugar_Array_OneToOne` | `[1,2]` ⇒ `Array([1,2])` |
| `Desugar_Index_OneToOne` | `a[0]` ⇒ `Index(a,0)` |
| `Desugar_Index_NoBoundsCheckExpansion` | nenhum `If` gerado |
| `Desugar_Member_OneToOne` | `a.b` ⇒ `Field(a,"b")` |
| `Desugar_Construct_OneToOne` | snapshot |

### `match`, `type`, `enum`

| Teste | Asserção |
|---|---|
| `Desugar_Match_PreservesArmOrder` | ordem dos braços mantida |
| `Desugar_Match_PatternNormalization` | `Result.Ok(v)` ⇒ `VariantPat(["Result","Ok"],[IdentPat v])` |
| `Desugar_Match_WildcardPattern` | `_` ⇒ `WildcardPat` |
| `Desugar_Match_ReturnInArm` | `Ok(v) => return v` ⇒ `Arm(_, Return(v))` |
| `Desugar_TypeDef_PreservesFields` | snapshot |
| `Desugar_EnumDef_PreservesVariants` | snapshot |
| `Desugar_GenericParams_Preserved` | `fn<T, N: Int>` ⇒ 2 params na Core |

### Spans e invariantes globais

| Teste | Asserção |
|---|---|
| `Desugar_PreservesSpans` | property: todo `CoreExpr` tem span dentro dos limites do arquivo |
| `Desugar_ImplicitElse_HasIfSpan` | o `Literal(Unit)` do else implícito aponta para o `if` |
| `Desugar_Total` | property: toda `SourceFile` sem erros de parse desugara sem lançar |
| `Desugar_Deterministic` | property: duas execuções ⇒ mesma S-expression |
| `Desugar_CoreHasNoSurfaceOnlyNodes` | property: a Core resultante não contém `AndAlso`/`OrElse` |

### Snapshots de programas completos

`examples/hello.ls`, `functions.ls`, `arrays.ls`, `result.ls` e os exemplos das
seções §11, §12, §22 e §34 da spec, cada um com um `.verified.txt`.

---

## Critérios de conclusão

- [ ] Todas as regras de §5.2 implementadas e cobertas por teste.
- [ ] Core resultante contém apenas os 16 nós do plano 02.
- [ ] Property `Desugar_Total` e `Desugar_Deterministic` verdes.
- [ ] Snapshots dos 4 exemplos estáveis.
- [ ] `Lapis.Desugar` não referencia `Runtime` nem `Evaluator`.

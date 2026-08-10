# 08 — Evaluator

**Milestone:** M1 → M3 · **Depende de:** 02, 06, 07 · **Projeto:** `Lapis.Evaluator`

Corresponde à Etapa 4 da spec (§46) e a §28, §30, §36.

---

## Objetivo

`TypedProgram × Environment → Value`. Esta é a **implementação de referência da
semântica** da LapisLang (spec §36, §58). Prioridade absoluta: ser simples e
óbvio. Performance é irrelevante aqui — se houver conflito entre clareza e
velocidade, clareza vence, e a justificativa fica registrada.

---

## Escopo faseado (spec §46)

| Fase | Nós | Milestone |
|---|---|---|
| A | `Literal`, `Variable`, `Let`, `Binary`, `Unary`, `If` | M1 |
| B | `Lambda`, `Call`, `Return` (fluxo de controle) | M1 |
| C | `Array`, `Index` (+ `Result` do prelude) | M2 |
| D | `EnumDef`, `TypeDef`, `Field`, `Construct`, `Match` | M3 |

---

## O que será construído

### 8.1 API

```csharp
public sealed class Evaluator
{
    public static EvaluationResult Run(TypedProgram program, RuntimeContext context);
    public Value Evaluate(CoreExpr expr, Environment env);      // reentrante, usado pelo PE
}

public sealed record EvaluationResult(Value Value, ExecutionStatus Status, string? Message, SourceSpan? Span);
public enum ExecutionStatus { Completed, Aborted }   // Aborted: divisão por zero etc.
```

### 8.2 `return` como fluxo de controle (spec §28)

Sem exceções C#. `Evaluate` interno devolve um **completion record**:

```csharp
readonly record struct Completion(CompletionKind Kind, Value Value);
enum CompletionKind { Normal, Return, Abort }
```

Regras:

```text
eval(Return(e))                = Completion(Return, eval(e))       ou Void se e == null
eval(Let(x, v, body)):
    c = eval(v);  if c.Kind != Normal → propaga c
    eval(body, env.Extend(x, c.Value))
eval(If(c,t,e)):
    cc = eval(c); if cc.Kind != Normal → propaga
    eval(cc.Value ? t : e)
eval(Binary(op,l,r)):
    cl = eval(l); propaga se != Normal
    cr = eval(r); propaga se != Normal
    Completion(Normal, Primitives.Apply(op, cl.Value, cr.Value))
```

Ou seja, **todo** nó propaga um completion não-`Normal` sem avaliar o resto. A
única fronteira que converte `Return` de volta em `Normal` é a chamada de função:

```text
apply(closure, args):
    env' = closure.Captured.ExtendAll(params ↦ args)
    c = eval(closure.Lambda.Body, env')
    match c.Kind:
        Return → Completion(Normal, c.Value)
        Normal → Completion(Normal, Void)      // função Void que caiu no fim (spec §12)
        Abort  → propaga
```

O braço `Normal → Void` só é alcançável em funções `Void`: o checker garante
(`LAP0272`) que uma função não-`Void` retorna em todos os caminhos. Se acontecer
em função não-`Void`, é `InternalCompilerException` — bug do checker.

**`Abort`** existe para erros de execução da linguagem que não são `Result`.
Depois de Q9 (divisão total), sobrou apenas o limite de profundidade de chamada —
que, sem recursão (Q8), também é inalcançável hoje. O mecanismo fica como rede de
proteção para quando a recursão entrar.

**Por que completion record e não exceção C#?** Três razões: (1) o custo de
exceção em .NET tornaria funções recursivas absurdamente lentas; (2) o partial
evaluator precisa avaliar sub-expressões e inspecionar o completion sem
`try/catch`; (3) o completion torna a regra de propagação explícita no código —
o que importa numa implementação de referência.

### 8.3 Ordem de avaliação

Fixada e testável: **esquerda para direita, de dentro para fora**, sem exceções.

- `Binary(op, l, r)`: `l` antes de `r`, sempre — mesmo em `*` e `==`.
- `Call(f, args)`: o callee é avaliado antes dos argumentos; argumentos da
  esquerda para a direita.
- `Array([e1..en])`: `e1` … `en` em ordem.
- `Construct`: campos na ordem em que aparecem no **código**, não na ordem de
  declaração do `type`.
- `&&`/`||`: já viraram `If` no desugar ⇒ curto-circuito garantido pela regra do
  `If`.

Isso é observável via `print` e está coberto por testes — e é obrigação do
partial evaluator preservar (plano 12).

### 8.4 `Index` e a produção de `Result` (spec §21, §30, §41)

```text
eval(Index(target, index)):
    a = eval(target); i = eval(index)
    match Primitives.ArrayGet(a, i):
        InBounds(v)  → Prelude.MakeOk(v)
        OutOfBounds  → Prelude.MakeErr(Prelude.IndexErrorOutOfBounds)
```

`Prelude.MakeOk`/`MakeErr` constroem `EnumValue` usando a `TypeDefinition` de
`Result` **resolvida do prelude** (plano 09). O evaluator não tem `Result`
hardcoded: se o prelude não o define, é erro interno.

Fora de limites **nunca** lança e **nunca** aborta (spec §30).

### 8.5 `Match`

Avalia o escrutinado uma única vez; testa os braços em ordem; o primeiro que casa
vence. Casamento de padrão:

```text
match(WildcardPat, v)          = sucesso, sem bindings
match(IdentPat(x), v)          = se x resolveu para variante nulária (via Resolutions):
                                     sucesso sse v é aquela variante
                                 senão: sucesso, binding x ↦ v
match(VariantPat(path, ps), v) = v é EnumValue da variante indicada
                                 e todos os sub-padrões casam com a carga
match(LiteralPat(c), v)        = v == c (igualdade estrutural)
```

O checker garante exaustividade (`LAP0262`), então "nenhum braço casou" é
`InternalCompilerException`.

### 8.6 Chamadas e generics

`ClosureValue` guarda os argumentos genéricos resolvidos pelo checker
(`CallResolution`). Na v0.2 o evaluator **não** monomorfiza: o corpo genérico é
avaliado como está, e os argumentos genéricos só importam para (a) construir
valores de enum/struct com os tipos certos, e (b) alimentar `Format`. Const
generics ficam disponíveis como bindings normais no ambiente da closure — é isso
que os torna acessíveis dentro do corpo.

### 8.7 Sem recursão em v0.2

Como decidido no plano 06 §6.2, `Let` não é recursivo ⇒ nenhum programa v0.2
pode recorrer ⇒ o evaluator não precisa de proteção contra stack overflow. Ainda
assim, um contador de profundidade de chamada com limite (10.000) produz um
`Abort` legível, para o dia em que a recursão entrar. Custo desprezível, evita
crash não-capturável.

---

## Testes necessários

### Fase A — núcleo (M1)

| Teste | Fonte | Esperado |
|---|---|---|
| `Eval_IntLiteral` | `1` | `IntValue(1)` |
| `Eval_AllLiterals` | float, bool, str, unit | valores correspondentes |
| `Eval_Arithmetic` | `10 + 20` | `30` — exemplo da spec §28 |
| `Eval_Precedence` | `1 + 2 * 3` | `7` |
| `Eval_IntDivision_Truncates` | `7 / 2` | `3` |
| `Eval_IntDivision_ByZero_IsMaxValue` | `1 / 0` | `9223372036854775807` (Q9) |
| `Eval_FloatDivision_ByZero` | `1.0 / 0.0` | `Infinity` |
| `Eval_StrConcat` | `"a" + "b"` | `"ab"` |
| `Eval_Comparisons` | todos os 6 operadores | `Bool` correto |
| `Eval_Not`, `Eval_Negate` | | |
| `Eval_Let_Sequential` | `def x=1; def y=x+1; y` | `2` |
| `Eval_Shadowing` | escopo interno | valor interno |
| `Eval_Block_Value_IsTail` | `{ def x=1; x+1 }` | `2` (spec §9) |
| `Eval_Block_NoTail_IsVoid` | `{ print(1); }` | `Void` |
| `Eval_If_TrueBranch` / `Eval_If_FalseBranch` | | |
| `Eval_If_NoElse_IsVoid` | `if false { 1 }` | `Void` |
| `Eval_AndAlso_ShortCircuits` | `false && print_and_true()` | lado direito **não** executa |
| `Eval_OrElse_ShortCircuits` | `true \|\| ...` | idem |

### Fase B — funções, closures, `return`

| Teste | Fonte | Esperado |
|---|---|---|
| `Eval_Call_Simple` | spec §34 `add(10,20)` | `30` |
| `Eval_Call_ArgOrder_LeftToRight` | `f(print1(), print2())` | saída `1\n2\n` |
| `Eval_Closure_CapturesEnv` | spec §32 `multiply` | `x * multiplier` |
| `Eval_Closure_CapturesAtDefinitionTime` | shadowing posterior não afeta | valor original |
| `Eval_HigherOrder_ReturnsFn` | `fn() fn(Int) Int { ... }` | chamável |
| `Eval_Fn_InArray` | spec §11 | `values[0]` chamável |
| `Eval_Return_Value` | `fn() Int { return 1; }()` | `1` |
| `Eval_Return_Early_SkipsRest` | `fn() Int { return 1; print(2); return 3; }` | `1`, **nada impresso** |
| `Eval_Return_FromInsideIf` | spec §12 `abs(-5)` | `5` |
| `Eval_Return_FromInsideNestedBlock` | `{ { return 1; } }` | `1` |
| `Eval_Return_FromInsideMatchArm` | spec §22 `unwrapOr` | valor correto |
| `Eval_Return_StopsAtNearestFunction` | lambda interna retorna, externa continua | spec §12, último bullet |
| `Eval_VoidFn_FallsOffEnd` | `fn() Void { print(1); }` | `Void` |
| `Eval_VoidFn_BareReturn` | `fn() Void { return; }` | `Void` |
| `Eval_Return_InArgumentPosition` | `f(return 1)` dentro de função | função externa retorna 1 |
| `Eval_Return_InBinaryOperand` | `1 + (return 2)` | retorna 2, `+` não executa |

### Fase C — arrays e indexação (M2)

| Teste | Fonte | Esperado |
|---|---|---|
| `Eval_Array_Literal` | `[1,2,3]` | `ArrayValue` de 3 |
| `Eval_Array_ElementOrder` | `[p(1), p(2)]` | saída `1\n2\n` |
| `Eval_Index_First` | `[1,2,3][0]` | `Result.Ok(1)` — spec §50 |
| `Eval_Index_Last` | `[1,2,3][2]` | `Result.Ok(3)` — spec §50 |
| `Eval_Index_OutOfBounds` | `[1,2,3][3]` | `Result.Err(IndexError.OutOfBounds)` — spec §50 |
| `Eval_Index_Negative` | `[1,2,3][-1]` | `Result.Err(...)` |
| `Eval_Index_EmptyArray` | `[][0]` | `Result.Err(...)` |
| `Eval_Index_NeverThrows` | property: índice arbitrário ⇒ sempre `Result`, nunca exceção (spec §30) |
| `Eval_Index_ResultIsEnumValue` | o valor é `EnumValue` da `TypeDefinition` do prelude, não um tipo especial |
| `Eval_Index_Nested` | `[[1,2]][0]` ⇒ `Ok([1,2])` |
| `Eval_ArrayLength` | `array_length([1,2,3])` ⇒ `3` |

### Fase D — enums, structs, match (M3)

| Teste | Asserção |
|---|---|
| `Eval_EnumDef_ProducesTypeValue` | `def C = enum{Red};` ⇒ `TypeValue` |
| `Eval_EnumVariant_Nullary` | `C.Red` ⇒ `EnumValue(idx 0, [])` |
| `Eval_EnumVariant_WithPayload` | `Ok(10)` ⇒ carga `[IntValue(10)]` |
| `Eval_EnumVariant_RequiresQualification` | `Result.Ok(10)`; a forma nua nem compila (Q3) |
| `Eval_Struct_Construct` | `.User { id: 1, name: "g" }` ⇒ `StructValue` (Q2) |
| `Eval_Struct_FieldAccess` | `u.id` ⇒ `1` |
| `Eval_Struct_ConstructFieldOrder` | ordem de avaliação segue o código |
| `Eval_Match_FirstMatchingArm` | ordem respeitada |
| `Eval_Match_BindsPayload` | `Ok(v) => v` ⇒ valor da carga |
| `Eval_Match_Wildcard` | `_` casa qualquer coisa |
| `Eval_Match_NestedPattern` | `Ok(Ok(v))` |
| `Eval_Match_LiteralPattern` | `1 => "one"` |
| `Eval_Match_ScrutineeEvaluatedOnce` | `match p() { ... }` com efeito ⇒ uma impressão |
| `Eval_Match_ArmWithReturn` | retorna da função envolvente |
| `Eval_Match_NoArmMatches_IsInternalError` | construção artificial ⇒ `InternalCompilerException` |

### Integração ponta a ponta (âncoras da spec)

| Teste | Fonte | Saída |
|---|---|---|
| `Eval_SpecExample_Section34` | `examples/hello.ls` | `30\n` — **teste-âncora do M1** |
| `Eval_SpecExample_Section3` | §3 | `30\n` |
| `Eval_SpecExample_Section5` | §5 | `Ok(2)\n` |
| `Eval_SpecExample_Section22` | `unwrapOr` | valor correto nos dois braços |
| `Eval_SpecExample_Section42` | `values[1]` ⇒ `Ok(20)` |

### Invariantes

| Teste | Asserção |
|---|---|
| `Eval_NeverThrows_OnWellTypedProgram` | property: programas gerados que passam no checker nunca lançam |
| `Eval_Deterministic` | property: mesma entrada ⇒ mesmo valor e mesma saída |
| `Eval_NoConsoleWrites` | toda saída passa pelo `IOutput` injetado |
| `Eval_CallDepthLimit_Aborts` | construção artificial de 20k chamadas ⇒ `Abort`, não `StackOverflow` |

---

## Critérios de conclusão

- [ ] `lapis examples/hello.ls` imprime `30` (spec §60) — **fecha o M1**.
- [ ] `return` implementado com completion record, com todos os testes de §12.
- [ ] Os três casos de indexação da spec §50 verdes.
- [ ] Nenhuma exceção C# escapa para um programa bem-tipado.
- [ ] Ordem de avaliação documentada e testada por observação de efeitos.

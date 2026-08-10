# 13 — Partial Evaluator: Beta Reduction e Especialização

**Milestone:** M7 · **Depende de:** 12 · **Projeto:** `Lapis.PartialEvaluator`

Corresponde à spec §37 e ao segundo bloco de §52 ("beta reduction, closure
specialization, function specialization").

---

## Objetivo

Fazer o PE atravessar chamadas de função. É aqui que o exemplo central da
spec §37 passa a funcionar:

```c
def add = fn(a: Int, b: Int) Int { return a + b; };
add(10, x);
```

com `x` dinâmico, deve residualizar para algo equivalente a:

```c
fn(x: Int) Int { return 10 + x; }
```

---

## O que será construído

### 13.1 `PEClosure`

Uma closure em tempo de PE tem ambiente **estático**, não de runtime:

```csharp
sealed record PEClosure(
    CoreLambda Lambda,
    StaticEnvironment Captured,
    ImmutableArray<SemanticGenericArg> GenericArguments,
    string? OriginName)                  // para nomear a especialização
    : PEValue;
```

Uma closure é `Known` mesmo quando seu ambiente capturado contém `Unknown` — o
que a torna **parcialmente estática**. É essa distinção que dá poder ao PE.

### 13.2 Especialização de `Call`

```text
P⟦Call(f, args)⟧σ:
    fc = P⟦f⟧σ
    ac = [P⟦a⟧σ for a in args]           // esquerda→direita, ordem preservada

    caso fc seja Known(PEClosure c):
        se todos os ac são Static  e  o corpo é puro e "pequeno"
            → unfold: avalia o corpo com σ' = c.Captured[params ↦ Known(...)]
              (constant folding completo — é literalmente o evaluator)
        se alguns ac são Dynamic
            → beta reduction parcial: especializa o corpo com
              σ' = c.Captured[params ↦ mistura de Known/Unknown]
              e residualiza o resultado
        se o corpo não é seguro para desdobrar (tamanho/efeitos/limite)
            → especializa a função (§13.4) e emite chamada à versão especializada

    caso fc seja Known(NativeFunction):
        se o nativo é puro (array_length) e args estáticos → executa
        se o nativo é efetivo (print)                      → residualiza sempre

    caso fc seja Dynamic:
        → Dynamic(Call(residual(fc), residuais(ac)))
```

### 13.3 Disciplina de duplicação (o risco real)

Beta reduction ingênua duplica trabalho e efeitos:

```c
def twice = fn(v: Int) Int { return v + v; };
twice(f())        // f() executaria duas vezes se `v` fosse substituído textualmente
```

Regra, herdada de §12.4: um argumento é **substituído** apenas se for
`Static` **ou** uma expressão dinâmica trivial e pura (variável/literal). Caso
contrário, o PE emite um `Let` residual e substitui pelo nome:

```text
Let($a0, f(), <corpo com v ↦ $a0>)
```

Isso preserva número e ordem de efeitos, que é o que o teste
`PE_Effects_NotDuplicated` (plano 12) verifica.

### 13.4 Especialização de funções (polivariante)

Quando desdobrar não é seguro ou não vale a pena, o PE **cria uma nova função**
especializada para o padrão de tempo de ligação daquele call site:

```c
def add = fn(a: Int, b: Int) Int { return a + b; };
add(10, x);
```

⇒

```c
def add$1 = fn(b: Int) Int { return 10 + b; };
add$1(x);
```

Componentes:

```csharp
sealed record SpecializationKey(
    CoreLambda Lambda,                                  // identidade da origem
    ImmutableArray<PEValue> ArgumentBindingTimes,       // Known(v) exato ou Unknown(T)
    ImmutableArray<SemanticGenericArg> GenericArguments,
    StaticEnvironmentFingerprint CapturedFingerprint);  // parte estática do ambiente capturado

sealed class SpecializationCache
{
    bool TryGet(SpecializationKey key, out SpecializedFunction fn);
    SpecializedFunction Add(SpecializationKey key, ...);
}
```

**Polivariante**: a mesma função origina especializações distintas para padrões
de argumento distintos. `add(10, x)` e `add(20, x)` geram `add$1` e `add$2`.
O cache é indexado pela chave completa, então `add(10, y)` reusa `add$1`.

O `CapturedFingerprint` é necessário porque duas closures da mesma lambda podem
ter capturado ambientes diferentes (spec §32) — especializar sem considerar isso
seria incorreto.

As funções especializadas são emitidas como `Let`s no topo do residual, na ordem
de criação (determinismo).

### 13.5 Especialização de closures

Caso especial e importante para a pesquisa:

```c
def makeAdder = fn(n: Int) fn(Int) Int {
    return fn(x: Int) Int { return x + n; };
};
def add10 = makeAdder(10);
add10(y);
```

Com `n = 10` estático, o PE deve produzir `y + 10` — ou seja, propagar através de
duas camadas de closure. Isso funciona "de graça" se `PEClosure` guarda o
`StaticEnvironment` capturado, e é exatamente o teste que prova que a captura foi
implementada certo.

### 13.6 Especialização por generics (spec §13)

Argumentos genéricos são, por construção, conhecidos em tempo de PE — inclusive
os **const generics**. Portanto:

- `identity<Int>(x)` especializa para uma versão monomórfica;
- `SomeType<"value", 1, true, Int, fn() Int { return 1; }>` tem todos os
  argumentos disponíveis como `Known` no ambiente estático da especialização;
- um const generic usado no corpo (`return N * 2;` com `N = 3`) dobra para `6`.

Esta é a ponte entre o sistema de tipos e o partial evaluator, e é um dos
resultados de pesquisa mais interessantes do projeto: **const generics são
partial evaluation declarada pelo programador**.

### 13.7 Terminação e limites

Em v0.2 não há recursão (plano 06 §6.2), então desdobramento sempre termina.
Ainda assim, os limites são implementados agora, porque (a) inlining pode
explodir o tamanho e (b) recursão entra em v0.3:

| Limite | Padrão | Efeito ao estourar |
|---|---|---|
| `MaxUnfoldDepth` | 8 | para de desdobrar, residualiza a chamada |
| `MaxResidualGrowthFactor` | 4 | reverte a última especialização e residualiza |
| `MaxSpecializationsPerFunction` | 16 | reusa a versão mais genérica |
| `MaxFixpointIterations` | 5 | encerra a iteração |

Todo limite atingido é registrado em `PEStatistics` e aparece no `--trace`
(plano 15) — silêncio ao desistir de otimizar é o pior comportamento possível
numa ferramenta de pesquisa.

### 13.8 Iteração até ponto fixo

Inlining cria novas oportunidades de dobra, que criam novas oportunidades de
inlining. O PE passa a iterar: aplica uma passada completa, compara a S-expression
com a anterior, repete até estabilizar ou atingir `MaxFixpointIterations`.

Isso torna `PE(PE(P)) == PE(P)` (teste do plano 12) um requisito ainda mais forte
— e ele continua sendo obrigatório.

---

## Testes necessários

Mesmo protocolo do plano 12 (residual esperado + equivalência + idempotência).

### Beta reduction

| Teste | Entrada | Residual |
|---|---|---|
| `PE_Call_AllStatic_FullyEvaluated` | `add(10,20)` | `30` |
| `PE_Call_PartiallyStatic` | `add(10,x)` | `10 + x` — **exemplo da spec §37** |
| `PE_Call_AllDynamic` | `add(x,y)` | chamada preservada ou `x+y` |
| `PE_Call_NestedCalls` | `add(add(1,2), x)` | `3 + x` |
| `PE_Call_ArgumentOrderPreserved` | efeitos nos argumentos | ordem mantida |
| `PE_Call_ImpureArg_NotDuplicated` | `twice(f())` | um `Let`, um `f()` |
| `PE_Call_PureTrivialArg_Substituted` | `twice(y)` | `y + y` |
| `PE_Call_UnknownCallee_Residualized` | callee dinâmico | chamada preservada |
| `PE_Call_NativePure_Folded` | `array_length([1,2,3])` | `3` |
| `PE_Call_NativeEffectful_NotFolded` | `print(1)` | preservado |

### `return` dentro de funções desdobradas

| Teste | Asserção |
|---|---|
| `PE_Unfold_ReturnValue_BecomesExpression` | `fn(){return 1;}()` ⇒ `1` (o `return` some ao desdobrar) |
| `PE_Unfold_EarlyReturn_Static` | `abs(-5)` ⇒ `5` |
| `PE_Unfold_EarlyReturn_Dynamic` | `abs(x)` ⇒ função especializada com os dois `return` |
| `PE_Unfold_DoesNotLeakReturn` | um `return` desdobrado **não** retorna da função externa |
| `PE_Unfold_VoidFunction` | função `Void` desdobrada não produz valor espúrio |

### Closures

| Teste | Entrada | Residual |
|---|---|---|
| `PE_Closure_CapturedStatic` | spec §32 `multiply(x)` com `multiplier=10` | `x * 10` |
| `PE_Closure_CapturedDynamic` | capturado desconhecido | closure preservada |
| `PE_Closure_TwoLevels` | `makeAdder(10)(y)` | `y + 10` |
| `PE_Closure_SameLambda_DifferentCaptures` | duas closures da mesma lambda | especializações distintas |
| `PE_Closure_InArray` | array de closures indexado estaticamente | closure certa selecionada |
| `PE_Closure_ReturnedAndCalledLater` | | correto |

### Especialização de funções

| Teste | Asserção |
|---|---|
| `PE_Specialize_EmitsNewFunction` | `add$1` presente no residual |
| `PE_Specialize_ReusesForSameKey` | `add(10,x)` e `add(10,y)` ⇒ **uma** especialização |
| `PE_Specialize_DistinctForDifferentKey` | `add(10,x)` e `add(20,x)` ⇒ duas |
| `PE_Specialize_RespectsCapturedEnv` | mesma lambda, capturas diferentes ⇒ chaves diferentes |
| `PE_Specialize_Deterministic` | duas execuções ⇒ mesmos nomes e mesma ordem |
| `PE_Specialize_UnusedOriginal_Eliminated` | se todas as chamadas foram especializadas e a original é pura e não referenciada, ela some |
| `PE_Specialize_OriginalKept_IfEscapes` | função usada como valor sobrevive |

### Generics e const generics

| Teste | Asserção |
|---|---|
| `PE_Generic_Monomorphized` | `identity<Int>(x)` ⇒ versão `Int` |
| `PE_Generic_TwoInstantiations` | `identity<Int>` e `identity<Str>` ⇒ duas especializações |
| `PE_ConstGeneric_FoldsInBody` | `N=3` no corpo `return N*2;` ⇒ `6` |
| `PE_ConstGeneric_FunctionArg_Inlined` | `fn() Int { return 1; }` como arg genérico ⇒ chamada dobrada para `1` |
| `PE_ConstGeneric_AffectsSpecializationKey` | `F<Int,3>` e `F<Int,4>` ⇒ especializações distintas |
| `PE_SpecExample_Section13` | o exemplo completo de const generics reduz |

### Limites e terminação

| Teste | Asserção |
|---|---|
| `PE_UnfoldDepth_Respected` | cadeia de 20 chamadas com limite 8 ⇒ para e residualiza |
| `PE_GrowthFactor_Respected` | residual não excede 4× o original |
| `PE_MaxSpecializations_Respected` | 100 call sites distintos ⇒ no máximo 16 versões |
| `PE_Fixpoint_Converges` | corpus inteiro converge em ≤ 5 iterações |
| `PE_Fixpoint_LimitReached_IsRecorded` | estatística e trace registram |
| `PE_Terminates_OnAllConformanceCases` | com timeout |

### Equivalência (spec §40, §54)

| Teste | Asserção |
|---|---|
| `PE_Equivalence_WithInlining_OverCorpus` | corpus inteiro, `Inlining=true` |
| `PE_Equivalence_PerFlagCombination` | as 4 combinações de flags dão o mesmo **resultado observável** |
| `PE_Residual_TypeChecks_WithSpecializations` | residual com funções especializadas re-tipável |
| `PE_Idempotent_WithInlining` | |
| `PE_EffectCount_Preserved` | property: nº de `print` executados idêntico entre original e residual |

---

## Critérios de conclusão

- [ ] `add(10, x)` residualiza para `10 + x` (spec §37) — **critério central do M7**.
- [ ] Especialização polivariante com cache determinístico.
- [ ] Closures parcialmente estáticas propagando em dois níveis.
- [ ] Const generics dobrando no corpo especializado.
- [ ] Equivalência verde sobre todo o corpus com inlining ligado.
- [ ] Nenhum efeito duplicado, eliminado ou reordenado (property test).

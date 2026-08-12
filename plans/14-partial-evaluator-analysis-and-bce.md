# 14 — Partial Evaluator: Análise Estática e Bounds-Check Elimination

**Milestone:** M13/M14 · **Depende de:** 13 · **Projeto:** `Lapis.PartialEvaluator`

Corresponde ao terceiro bloco da spec §52 ("conditional specialization, dead
branch elimination, array specialization, bounds-check elimination"), a §41–§42,
e aos testes de §54–§55.

---

> **Laços, agora via `loop`/`break`/`continue` (plano 26, Q32).** Até o M6 a
> linguagem terminava por construção; o `goto` para trás trazia laços (Q24), e o
> M16 trocou o mecanismo por controle estruturado sem mudar essa consequência —
> um `loop` com progresso ainda não termina por construção. Especializar um laço
> não é como especializar um `If`: exige *widening* ou combustível, sob pena de o
> próprio partial evaluator não terminar. "Provar `i < n` para um `i` derivado de
> laço" (a análise que este plano quer) passa a ser sobre um `var` mutado dentro
> de um `CoreLoop` — um escopo léxico só, mais fácil de seguir do que o grafo de
> joins que `goto`/`label` produziam. Este plano precisa ser revisitado com isso
> em mãos antes de começar.


## Objetivo

Passar de "sei o valor exato" para "sei **algo** sobre o valor". É a diferença
entre partial evaluation e análise estática — e é onde a pergunta científica da
spec §61 começa a ter respostas quantitativas.

O resultado emblemático (spec §42):

```c
def values = [10, 20, 30];
def x = values[1];
```

⇒ o PE prova `0 <= 1 < 3` e residualiza `def x = Result.Ok(20);` sem checagem de limites.

---

## O que será construído

### 14.1 Domínios abstratos

`Unknown(Type)` do plano 12 passa a carregar um fato abstrato:

```csharp
sealed record Unknown(LapisType Type, AbstractValue Facts) : PEValue;

abstract record AbstractValue;
  sealed record TopValue                                  : AbstractValue;  // nada se sabe
  sealed record IntRange(long Min, long Max)              : AbstractValue;  // [min, max]
  sealed record BoolFact(bool? KnownValue)                : AbstractValue;
  sealed record ArrayFact(int? KnownLength, AbstractValue ElementFacts) : AbstractValue;
  sealed record EnumFact(ImmutableArray<int> PossibleVariants)          : AbstractValue;
```

Cada domínio implementa a interface de rede (lattice):

```csharp
interface ILattice<T> { T Top { get; } T Bottom { get; } T Join(T a, T b); T Meet(T a, T b); bool LessOrEqual(T a, T b); }
```

**Análise de ranges** (spec §1: "análise de ranges") sobre `Int`:

| Operação | Regra |
|---|---|
| `Literal n` | `[n, n]` |
| `a + b` | `[a.min+b.min, a.max+b.max]` com saturação em overflow ⇒ `Top` |
| `a - b`, `a * b` | análogo (multiplicação: min/max dos 4 produtos) |
| `a / b` | `Top` se `0 ∈ b`; senão min/max dos 4 quocientes |
| `-a` | `[-a.max, -a.min]` |
| `array_length(a)` | `[0, ∞)`, ou `[n,n]` se o comprimento é conhecido |

**Overflow é saturante e conservador**: qualquer operação que possa transbordar
`long` produz `Top`. Um range errado por overflow produziria BCE incorreta — o
tipo exato de bug que destrói a confiança na equivalência.

### 14.2 Refinamento por condição (conditional specialization)

Ao especializar `If(c, t, e)` com `c` dinâmico, os ramos são especializados sob
ambientes **refinados**:

```text
c = (i < n)   ⇒ no ramo `then`:  i ∈ [i.min, min(i.max, n.max-1)]
                 no ramo `else`: i ∈ [max(i.min, n.min), i.max]
```

Condições suportadas no M8: comparações de `Int` entre variável e constante, e
entre duas variáveis; `!c`; `&&`/`||` (que já viraram `If` no desugar, então o
refinamento acontece naturalmente aninhado).

Este é o mecanismo que faz o caso realista de BCE funcionar:

```c
def get = fn(a: Int[], i: Int) Result<Int, IndexError> {
    if i < array_length(a) {
        if 0 <= i {
            return a[i];        // aqui o PE sabe 0 <= i < length(a)
        }
    }
    return Err(IndexError.OutOfBounds);
};
```

### 14.3 Especialização de arrays

| Situação | Ação |
|---|---|
| array estático, índice estático | executa o acesso (já em M6) |
| array estático, índice dinâmico com range | se `range ⊆ [0, len)` ⇒ elimina o check |
| array dinâmico com comprimento conhecido | idem, usando `ArrayFact.KnownLength` |
| array dinâmico sem comprimento | mantém o check |

`ArrayFact.KnownLength` é propagado por: literais de array (comprimento exato),
`Let` de array literal, parâmetros especializados a partir de argumentos com
comprimento conhecido.

### 14.4 Bounds-check elimination (spec §41, §42)

O nó `Index` na Core **não** distingue "checado" de "não checado", e não deve —
a semântica é sempre checada (spec §41). A eliminação é representada assim:

- se o PE prova `0 <= i < len`, ele **residualiza o resultado direto**:
  `Result.Ok(<acesso>)`, usando um nó `CoreIndexUnchecked` interno ao PE, que:
  - só existe na Core produzida pelo PE;
  - é rejeitado pelo type checker se aparecer num programa de entrada;
  - é avaliado pelo evaluator **sem** o check.

Alternativa considerada e rejeitada: reescrever `a[i]` como
`if 0 <= i && i < len { Ok(...) } else { Err(...) }` e deixar o dead-branch
elimination cuidar. Rejeitada porque exigiria um nó de acesso não-checado de
qualquer forma, e porque perde a informação de que houve uma **prova** — que é
justamente o que o `--trace` precisa mostrar.

O evaluator ganha o caso `CoreIndexUnchecked` com uma asserção em build de debug
(`Debug.Assert(0 <= i < len)`), que em release é elidida. Se a asserção falhar
num teste, o PE é incorreto.

**Teste de segurança obrigatório:** para todo caso onde o PE eliminou um check,
executar o programa original **e** o residual com a mesma entrada e comparar; e,
adicionalmente, rodar a suíte inteira com as asserções de debug ligadas.

### 14.5 Simplificação de `Ok` (spec §42, último parágrafo)

A spec sugere que, num passo posterior, o `Ok` também poderia ser simplificado.
Isso só é válido quando o `Result` é **consumido imediatamente** por um `match`
estático:

```c
match values[1] { Result.Ok(v) => v, Result.Err(e) => 0 }
```

⇒ `20`, sem construir o `EnumValue`.

Implementado como uma regra de reescrita local: `Match(Construct-de-variante, arms)`
⇒ seleciona o braço e substitui os bindings. É "case-of-known-constructor",
clássico. **Não** é aplicado quando o `Result` escapa (é armazenado, retornado ou
passado adiante) — aí o valor precisa existir.

### 14.6 Eliminação de código morto avançada

Com os fatos abstratos disponíveis:

- ramos com condição provadamente constante são eliminados mesmo sem valor exato
  (`if x < 0` com `x ∈ [1, 10]` ⇒ ramo `else`);
- braços de `match` cujas variantes são impossíveis (`EnumFact`) são removidos;
- se sobra um único braço, o `match` inteiro desaparece.

### 14.7 Testes de equivalência com geração de programas (spec §54, §55)

Esta é a peça de M9 e o critério de qualidade mais forte do projeto.

```csharp
// Gerador FsCheck de programas LapisLang bem-tipados
public static class ProgramGenerator
{
    public static Gen<GeneratedProgram> WellTyped(GeneratorOptions options);
}

public sealed record GeneratedProgram(string Source, CoreProgram Core, ImmutableArray<string> DynamicNames);
```

O gerador constrói **por tipo** (type-directed generation): escolhe um tipo alvo
e gera uma expressão daquele tipo, garantindo que o programa passa no checker por
construção. Cobre: literais, aritmética, comparações, `if`, `let`, funções não
recursivas de até 3 parâmetros, closures, arrays, indexação, enums e `match`.

Propriedade principal (spec §55):

```text
∀ P, ∀ S ⊆ bindings(P):
    R = PE(P, S)
    eval(P, S) ≡ eval(R, S)          -- mesmo valor
    stdout(P, S) = stdout(R, S)      -- mesma sequência de efeitos
    status(P, S) = status(R, S)      -- mesmo aborto ou ausência dele
```

Propriedades secundárias:

| Propriedade | Enunciado |
|---|---|
| Idempotência | `PE(PE(P,S), ∅) ≡ PE(P,S)` |
| Monotonicidade da informação | se `S ⊆ S'` então `|PE(P,S')| ≤ |PE(P,S)|` em nº de nós (heurística, com contra-exemplos documentados quando falhar) |
| Preservação de tipos | `typeof(PE(P,S)) == typeof(P)` |
| Conservatividade | `PE(P, ∅)` sobre programa sem constantes ≡ `P` |
| Segurança de BCE | nenhum `IndexUnchecked` do residual acessa fora de limites em nenhuma execução |

**Redução (shrinking)** é obrigatória no gerador: um contra-exemplo de 200 linhas
é inútil. O shrinker deve reduzir preservando boa tipagem.

Contra-exemplos encontrados viram casos fixos em
`tests/conformance/regressions/`.

---

## Testes necessários

### Domínios abstratos (unitários, sem programa)

| Teste | Asserção |
|---|---|
| `Range_Add`, `Range_Sub`, `Range_Mul`, `Range_Div` | limites corretos, inclusive com negativos |
| `Range_Mul_NegativeBounds` | `[-2,3]*[-4,5]` ⇒ `[-12,15]` |
| `Range_Div_ContainingZero_IsTop` | |
| `Range_Overflow_SaturatesToTop` | `[long.Max-1, long.Max] + [1,1]` ⇒ `Top` |
| `Range_Join_IsUpperBound` | `join([1,2],[5,6]) == [1,6]` |
| `Range_Meet_Empty_IsBottom` | |
| `Lattice_Laws` | property: `join` comutativo, associativo, idempotente; `a ≤ join(a,b)` |
| `ArrayFact_LengthPropagation` | |
| `EnumFact_JoinUnionsVariants` | |

### Refinamento por condição

| Teste | Entrada | Asserção |
|---|---|---|
| `Refine_LessThanConstant` | `if i < 10 {}` | no `then`, `i.max <= 9` |
| `Refine_GreaterOrEqual` | `if i >= 0 {}` | no `then`, `i.min >= 0` |
| `Refine_ElseBranch` | | complementar |
| `Refine_TwoVariables` | `if i < n {}` | relaciona os ranges |
| `Refine_Negation` | `if !(i < 0) {}` | |
| `Refine_Conjunction` | `if 0 <= i && i < n {}` | ambos os fatos no `then` |
| `Refine_ProvesBranchDead` | `x ∈ [1,10]`, `if x < 0 {}` | ramo `then` eliminado |
| `Refine_DoesNotOverRefine` | property: o range refinado sempre contém o valor real em execuções concretas |

### Especialização de arrays e BCE (spec §42)

| Teste | Entrada | Residual |
|---|---|---|
| `BCE_StaticArray_StaticIndex` | `[10,20,30][1]` | `Result.Ok(20)` — **spec §42** |
| `BCE_StaticArray_RefinedIndex` | índice provado em `[0,2]` | acesso sem check |
| `BCE_StaticArray_UnprovableIndex` | índice `Top` | check preservado |
| `BCE_PartiallyProvable_LowerOnly` | prova `i >= 0` mas não `i < len` | check preservado |
| `BCE_DynamicArray_KnownLength` | comprimento conhecido, índice refinado | check eliminado |
| `BCE_GuardedAccess_Realistic` | exemplo de §14.2 | check interno eliminado |
| `BCE_OutOfBoundsProven` | índice provado `>= len` | residualiza `Result.Err(...)` direto |
| `BCE_NeverEliminatesUnsafely` | property: para toda eliminação, executar o original em 1000 entradas confirma que nenhum acesso era inválido |
| `BCE_Unchecked_RejectedInSource` | `IndexUnchecked` num programa de entrada ⇒ diagnóstico |
| `BCE_DebugAssertions_Enabled_InTestSuite` | a suíte roda com asserções ligadas |

### Case-of-known-constructor (spec §42, §14.5)

| Teste | Entrada | Residual |
|---|---|---|
| `MatchOnKnownOk_SelectsArm` | `match Result.Ok(20) { Result.Ok(v)=>v, Result.Err(_)=>0 }` | `20` |
| `MatchOnKnownErr_SelectsArm` | | `0` |
| `MatchOnStaticIndex_Simplifies` | `match values[1] { ... }` | `20` |
| `MatchOnEscapingResult_NotSimplified` | `Result` retornado | `EnumValue` preservado |
| `MatchArms_ImpossibleVariant_Removed` | `EnumFact` restringe | braço some |
| `MatchWithSingleRemainingArm_Collapses` | | `match` desaparece |

### Dead branch elimination

| Teste | Asserção |
|---|---|
| `Dead_IfWithProvenCondition_Eliminated` | ramo impossível some |
| `Dead_EffectsInDeadBranch_AlsoRemoved` | correto: o ramo nunca executaria |
| `Dead_LiveBranch_EffectsPreserved` | |
| `Dead_NestedConditions` | dois níveis |

### Equivalência gerada (spec §54, §55) — M9

| Teste | Asserção |
|---|---|
| `Property_Equivalence_ValueAndOutput` | 10.000 programas gerados; valor, stdout e status idênticos |
| `Property_Equivalence_AcrossStaticEnvs` | mesmo programa com vários subconjuntos estáticos |
| `Property_Residual_TypeChecks` | |
| `Property_Residual_Reparseable` | |
| `Property_Idempotence` | |
| `Property_BCE_Safety` | nenhum acesso não-checado inválido |
| `Property_Termination` | timeout por programa |
| `Generator_ProducesWellTypedPrograms` | 100% dos gerados passam no checker |
| `Generator_CoversAllCoreNodes` | métrica de cobertura por tipo de nó ≥ 1 ocorrência |
| `Shrinker_PreservesWellTypedness` | property |
| `Shrinker_ReducesSize` | contra-exemplo reduzido é menor |

### Métricas (alimenta o plano 15 e a spec §61)

| Teste | Asserção |
|---|---|
| `Statistics_CountsFoldedNodes` | |
| `Statistics_CountsEliminatedBranches` | |
| `Statistics_CountsEliminatedChecks` | |
| `Statistics_ResidualSizeRatio` | razão nós residual/original |
| `Statistics_AreDeterministic` | |

---

## Critérios de conclusão

- [ ] Exemplo da spec §42 residualizando para `Ok(20)` sem check.
- [ ] Análise de ranges com overflow saturante e refinamento por condição.
- [ ] Case-of-known-constructor implementado, com a regra de escape respeitada.
- [ ] Gerador de programas bem-tipados com shrinking.
- [ ] Property de equivalência verde em 10.000 casos.
- [ ] Property de segurança de BCE verde.
- [ ] Nenhum contra-exemplo aberto; todos convertidos em regressões.

# 12 — Partial Evaluator: Núcleo

**Milestone:** M7 · **Depende de:** M6 (`goto`/`label`) e M5 completo · **Projeto:** `Lapis.PartialEvaluator`

Corresponde à spec §37–§40, §52 (primeiro bloco) e §53.

> **Pré-requisito duro:** não começar antes do M5. A spec §52 é explícita
> ("somente depois que o evaluator estiver estável") e §58.3 define o evaluator
> como a referência semântica. Um PE construído sobre um evaluator instável
> testa contra um alvo móvel.

---

> **Este plano é o "PE básico" de que as macros dependem.** Uma `constraint`
> (plano 18) recebe capturas sintáticas e precisa reduzi-las a valores para decidir —
> dobrar `"route." + path`, avaliar `arrayLength(faltando) > 0`. Isso é exatamente o
> folding e a propagação desta fatia, e é por isso que o M7 vem antes do M8 mesmo
> com a decisão de tratar macros antes do partial evaluator.
>
> A fatia que as macros exigem é **só** folding e propagação. Especialização (13) e
> análise (14) continuam depois, onde já enfrentam laços (Q24).


## Objetivo

`CoreProgram × StaticEnvironment → CoreProgram residual`, preservando o
comportamento observável (spec §40):

```text
evaluate(P, S) ≡ evaluate(PE(P, S), S)
```

Primeiras transformações (spec §52): **literal reduction, constant folding,
variable propagation** — e a infraestrutura de residualização sobre a qual os
planos 13 e 14 se apoiam.

---

## O que será construído

### 12.1 Valores com tempo de ligação (spec §39)

```csharp
abstract record PEValue;
  sealed record Known(Value Value)      : PEValue;   // conhecido em tempo de PE
  sealed record Unknown(LapisType Type) : PEValue;   // dinâmico, mas com tipo
```

`Unknown` carrega o tipo porque o residual precisa ser tipável de novo — e porque
o plano 14 vai refinar `Unknown` com domínios abstratos (intervalos).

```csharp
public sealed class StaticEnvironment
{
    public StaticEnvironment? Parent { get; }
    public bool TryLookup(string name, out PEBinding binding);
    public StaticEnvironment Extend(string name, PEBinding binding);
}

sealed record PEBinding(PEValue Value, CoreExpr? ResidualReference);
```

`ResidualReference` é o nome (ou expressão) pelo qual esse binding é acessível no
programa residual. Para um binding dinâmico que virou um `Let` residual, é
`CoreVariable("x_1")`. É o que evita capturar nomes quando o PE renomeia.

### 12.2 Resultado da especialização

O PE precisa de mais que "valor ou expressão", porque `return` é fluxo de
controle e pode ser estático **ou** dinâmico:

```csharp
abstract record PEResult;
  sealed record StaticResult(Value Value)                     : PEResult;
  sealed record DynamicResult(CoreExpr Residual, LapisType Type) : PEResult;

readonly record struct PECompletion(PECompletionKind Kind, PEResult Result);
enum PECompletionKind {
    Normal,          // segue o fluxo
    Returned,        // retornou estaticamente com um valor conhecido
    MayReturn        // o residual contém um `return` cuja execução é dinâmica
}
```

`MayReturn` é a peça que a maioria dos partial evaluators de brinquedo esquece:

```c
def f = fn(x: Int) Int {
    if x < 0 { return 0; }      // condição dinâmica
    return 10 + 20;             // corpo estático
};
```

O `if` não pode ser reduzido, então o `return 0;` **precisa** sobreviver no
residual, e o PE não pode assumir que o fluxo depois dele é alcançável de forma
estática. A regra: quando um ramo produz `Returned`/`MayReturn` e o outro não, o
resultado da junção é `MayReturn` e o código subsequente é residualizado, não
executado.

### 12.3 Regras de especialização

Notação: `P⟦e⟧σ` = especialização de `e` no ambiente estático `σ`.

```text
P⟦Literal c⟧σ         = Static(c)

P⟦Var x⟧σ             = σ(x) = Known(v)      → Static(v)
                        σ(x) = Unknown(T)    → Dynamic(ref(x), T)
                        x ∉ σ                → erro interno (checker já validou)

P⟦Let(x,v,body)⟧σ     = c = P⟦v⟧σ
                        se c é Static(val) e val é "duplicável"   → P⟦body⟧σ[x↦Known(val)]
                        se c é Static(val) e não duplicável        → residualiza Let com literal
                        se c é Dynamic(r,T):
                            se r é puro e trivial (var/literal)    → P⟦body⟧σ[x↦Unknown(T), ref=r]
                            senão                                   → Let(x', r, P⟦body⟧σ[x↦Unknown(T), ref=x'])

P⟦Binary(op,l,r)⟧σ    = ambos Static  → Static(Primitives.Apply(op, ...))     [constant folding]
                        senão          → Dynamic(Binary(op, residual(l), residual(r)))

P⟦Unary(op,e)⟧σ       = análogo

P⟦If(c,t,e)⟧σ         = P⟦c⟧σ = Static(true)  → P⟦t⟧σ        [dead branch elimination]
                        P⟦c⟧σ = Static(false) → P⟦e⟧σ
                        dinâmico              → Dynamic(If(rc, residualize(P⟦t⟧σ), residualize(P⟦e⟧σ)))
                                                com junção de completions

P⟦Return(e)⟧σ         = P⟦e⟧σ = Static(v) → completion Returned com v
                        dinâmico          → Dynamic(Return(r)), completion MayReturn

P⟦Array(es)⟧σ         = todos Static → Static(ArrayValue)
                        senão         → Dynamic(Array(residuais))

P⟦Index(a,i)⟧σ        = ambos Static → executa ArrayGet e constrói Ok/Err   [plano 14 refina]
                        senão         → Dynamic(Index(...))

P⟦Call(f,args)⟧σ      = plano 13 (beta reduction / especialização)
P⟦Match(s,arms)⟧σ     = escrutinado Static → seleciona o braço e continua
                        senão              → residualiza todos os braços
```

`residualize(PEResult)` converte `Static(v)` de volta em `CoreExpr`:
`Literal` para primitivos, `Array` de literais para arrays, e — para closures,
enums e structs — a reconstrução do nó correspondente. Valores que **não** têm
forma sintática (uma closure que capturou um ambiente dinâmico) não podem ser
residualizados; nesses casos o PE mantém a expressão original. Essa é a
"lift"/"reification" clássica, e é o único lugar onde o PE pode falhar em
progredir — nunca em correção.

### 12.4 Efeitos: a regra que preserva a equivalência

Esta seção é a diferença entre um PE correto e um que "otimiza" quebrando o
programa.

```csharp
public static class Effects
{
    public static bool IsPure(CoreExpr expr);   // conservador
}
```

Regras:

1. **`print` nunca é executado em tempo de PE.** Mesmo com argumento conhecido,
   `print(30)` é residualizado como `print(30)`. Executá-lo moveria a saída do
   programa para o tempo de compilação — violação direta de §40.
2. **Chamada a função desconhecida é impura**, por conservadorismo.
3. ~~`/` com denominador não comprovadamente diferente de zero é impura.~~
   **Não se aplica mais:** com Q9 a divisão é total (por zero produz o maior
   `Int`), então toda aritmética é pura e dobrável sem análise de efeito. Este é
   um ganho direto da decisão sobre divisão.
4. Nós puros: literais, variáveis, aritmética sobre operandos puros,
   `Array`/`Index`/`Field`/`Construct` sobre filhos puros, `Lambda` (criar uma
   closure não tem efeito).
5. **Nunca duplicar** uma expressão impura (substituição só de valores conhecidos
   ou de referências triviais — §12.3 no caso `Let`).
6. **Nunca eliminar** uma expressão impura, mesmo que seu resultado não seja
   usado. Um `Let` sintético cujo valor é impuro sobrevive no residual.
7. **Nunca reordenar** avaliações: o PE processa os filhos na mesma ordem que o
   evaluator (plano 08 §8.3).

### 12.5 Eliminação de código morto

Só sobre `Let` **puro** e não utilizado:

```text
Let(x, v, body) com x ∉ freeVars(body) e IsPure(v)  →  body
```

Requer análise de variáveis livres:

```csharp
public static class FreeVariables { public static ImmutableHashSet<string> Of(CoreExpr expr); }
```

### 12.6 Renomeação e captura

O PE cria bindings novos (ao residualizar um `Let` com nome já usado). Usa o
mesmo `FreshNameGenerator` do plano 05 (prefixo `$`, não produzível pelo lexer),
o que torna captura de nome estruturalmente impossível.

Teste dedicado: um programa com shadowing agressivo antes e depois do PE produz o
mesmo resultado.

### 12.7 API

```csharp
public sealed class PartialEvaluator
{
    public static PartialEvaluationResult Specialize(
        CoreProgram program,
        StaticEnvironment staticEnv,
        PEOptions options,
        PETrace? trace = null);
}

public sealed record PartialEvaluationResult(CoreProgram Residual, PEStatistics Statistics);

public sealed record PEOptions(
    bool ConstantFolding = true,
    bool DeadCodeElimination = true,
    bool Inlining = false,              // plano 13
    bool BoundsCheckElimination = false,// plano 14
    int MaxUnfoldDepth = 8,
    int MaxResidualGrowthFactor = 4);
```

Cada transformação atrás de uma flag: os testes de equivalência podem isolar qual
transformação quebrou uma propriedade.

`PEStatistics` (nós antes/depois, dobras realizadas, ramos eliminados, checks
eliminados) alimenta a pergunta científica da spec §61 e o plano 15.

### 12.8 Integração com o CLI

`lapis pe <arquivo>` imprime o programa residual usando o `CoreSourcePrinter`
(plano 02). Ambiente estático inicial: vazio (todo top-level é estático por
construção, já que não há entrada externa em v0.2) — o que faz `lapis pe` de um
programa fechado tender ao resultado totalmente avaliado. Para exercitar o
interessante, o CLI aceita `--dynamic x:Int,y:Str`, declarando bindings
top-level como desconhecidos. Detalhes no plano 15.

---

## Decisões de design

**PE sobre a Core AST, não sobre a Typed Core AST.** A spec §59 mostra o PE
partindo da Core. O PE consulta a tabela de tipos quando existe (para popular
`Unknown(T)`), mas não a exige — o que permite rodar o PE sobre um residual já
produzido por ele mesmo (idempotência) sem re-checar.

**Reusar `Primitives` do runtime para dobrar constantes** (spec §38: "quando o
evaluator consegue determinar o resultado, `evaluate` pode ser usado durante
partial evaluation"). Nada de reimplementar aritmética no PE — reimplementar
seria a forma mais provável de introduzir divergência semântica.

**Uma passada, sem ponto fixo, no M6.** Iterar até ponto fixo entra no plano 13,
quando inlining cria novas oportunidades. No M6 uma passada basta e mantém a
terminação trivial.

---

## Testes necessários

Todo teste de PE segue o protocolo da spec §53: comparar o residual esperado
**e** executar os dois programas.

```csharp
void AssertPE(string source, StaticEnvironment env, string expectedResidual)
{
    // 1. residual == esperado (comparação por S-expression)
    // 2. evaluate(original) == evaluate(residual)      (spec §40)
    // 3. stdout(original) == stdout(residual)          (efeitos preservados)
    // 4. PE(residual) == residual                      (idempotência)
}
```

### Redução de literais e constant folding (spec §38)

| Teste | Entrada | Residual |
|---|---|---|
| `PE_Literal_Unchanged` | `1` | `1` |
| `PE_Add_Constants` | `10 + 20` | `30` — exemplo da spec §38 |
| `PE_Nested_Constants` | `(1+2)*(3+4)` | `21` |
| `PE_Dynamic_Left` | `x + 20` com `x` dinâmico | `x + 20` — spec §38 |
| `PE_Dynamic_Both` | `x + y` | inalterado |
| `PE_Partial_Fold` | `(1+2) + x` | `3 + x` |
| `PE_Comparison_Folds` | `1 < 2` | `true` |
| `PE_StrConcat_Folds` | `"a" + "b"` | `"ab"` |
| `PE_Float_Folds_Exactly` | `0.1 + 0.2` | mesmo bit pattern do evaluator |
| `PE_DivByZero_Folds` | `1 / 0` | `9223372036854775807` (Q9: divisão é total) |
| `PE_DivByKnownNonZero_Folds` | `10 / 2` | `5` |
| `PE_Not_Folds` | `!true` | `false` |

### Propagação de variáveis

| Teste | Entrada | Residual |
|---|---|---|
| `PE_Let_KnownValue_Propagates` | `def x=10; x+5` | `15` |
| `PE_Let_Unused_Pure_Eliminated` | `def x=10; 1` | `1` |
| `PE_Let_Unused_Impure_Kept` | `def x=print(1); 2` | mantém o `print` |
| `PE_Let_DynamicValue_Kept` | `def x=f(); x+1` com `f` dinâmico | `Let` preservado |
| `PE_Let_DynamicValue_NotDuplicated` | `def x=f(); x+x` | **um** `f()`, não dois |
| `PE_Let_TrivialDynamic_Inlined` | `def x=y; x+1` | `y+1` |
| `PE_Shadowing_Preserved` | shadowing em escopos aninhados | semântica idêntica |
| `PE_NameCapture_Avoided` | binding residual colidindo com nome do usuário | nomes `$`-prefixados |

### Condicionais

| Teste | Entrada | Residual |
|---|---|---|
| `PE_If_StaticTrue` | `if true { 1 } else { 2 }` | `1` |
| `PE_If_StaticFalse` | | `2` |
| `PE_If_Dynamic_BothBranchesSpecialized` | `if x { 1+1 } else { 2+2 }` | `if x { 2 } else { 4 }` |
| `PE_If_DeadBranch_NotEvaluated` | `if false { 1/0 } else { 1 }` | `1`, sem abortar |
| `PE_If_DeadBranch_WithEffect_Removed` | `if false { print(1); } else {}` | `print` some (era inalcançável) |
| `PE_ShortCircuit_Preserved` | `false && print_true()` | `print` **não** executa no PE nem no residual |

### `return` e completions

| Teste | Entrada | Esperado |
|---|---|---|
| `PE_Return_Static` | `fn() Int { return 10+20; }` | corpo vira `return 30;` |
| `PE_Return_AfterStaticIf` | `if true { return 1; } return 2;` | `return 1;` |
| `PE_Return_UnderDynamicIf_Preserved` | `if x { return 0; } return 30;` | **os dois** `return` sobrevivem |
| `PE_CodeAfterStaticReturn_Eliminated` | `return 1; print(2);` | `print` some (inalcançável) |
| `PE_CodeAfterDynamicReturn_Preserved` | `if x { return 1; } print(2);` | `print` sobrevive |
| `PE_Return_MayReturn_Join` | um ramo retorna, outro não | completion `MayReturn` |

### Efeitos (§12.4) — a bateria mais importante

| Teste | Asserção |
|---|---|
| `PE_Print_NeverExecutedAtPeTime` | `print(30)` ⇒ nenhum stdout durante o PE |
| `PE_Print_ResidualizedWithFoldedArg` | `print(10+20)` ⇒ `print(30)` |
| `PE_Effects_NotDuplicated` | property: contagem de `print` no residual == contagem executada pelo original |
| `PE_Effects_NotReordered` | `print(1); print(2);` mantém ordem |
| `PE_Effects_NotEliminated` | valor descartado, efeito preservado |
| `PE_UnknownCall_TreatedAsImpure` | não é dobrado nem duplicado |
| `PE_NoArithmeticIsImpure` | property: nenhuma expressão aritmética é classificada como impura |

### Arrays e indexação

| Teste | Entrada | Residual |
|---|---|---|
| `PE_Array_AllStatic_Folds` | `[1,2,3]` | valor de array |
| `PE_Array_PartlyDynamic` | `[1, x]` | `[1, x]` |
| `PE_Index_Static_InBounds` | `[10,20,30][1]` | `Result.Ok(20)` — spec §42 |
| `PE_Index_Static_OutOfBounds` | `[1,2,3][5]` | `Result.Err(IndexError.OutOfBounds)` |
| `PE_Index_DynamicIndex_Kept` | `[1,2,3][i]` | inalterado |

### Equivalência e invariantes (spec §40, §54)

| Teste | Asserção |
|---|---|
| `PE_Equivalence_OverConformanceCorpus` | para **todo** caso de `tests/conformance/eval/**`: `eval(P) == eval(PE(P))` e stdout idêntico |
| `PE_Idempotent` | `PE(PE(P)) == PE(P)` (S-expression) |
| `PE_EmptyStaticEnv_IsIdentityOnDynamicProgram` | programa 100% dinâmico ⇒ residual estruturalmente igual |
| `PE_Residual_TypeChecks` | property: o residual passa no type checker sem erros |
| `PE_Residual_IsReparseable` | `parse(print(residual))` ⇒ mesma Core |
| `PE_Terminates` | property com timeout sobre o corpus |
| `PE_NeverThrows` | property: nunca lança em programa bem-tipado |
| `PE_Statistics_AreConsistent` | `nós depois <= nós antes * MaxResidualGrowthFactor` |

---

## Critérios de conclusão

- [x] `10 + 20` → `30` e `x + 20` → `x + 20` (spec §38).
- [x] Todas as regras de efeito de §12.4 implementadas e testadas.
- [x] `PE_Equivalence_OverConformanceCorpus` verde sobre todo o corpus.
- [x] `PE(PE(P)) == PE(P)`.
- [x] Residual sempre re-tipável e re-parseável.
- [x] `lapis pe examples/hello.ls` produz residual legível.

---

## O que a implementação mudou no plano

### Indexação estática não dobra — foi para o plano 14

§12.3 previa `[10,20,30][1]` → `Result.Ok(20)`. O resultado de uma indexação é um
**enum construído**, e um enum construído não volta a ser expressão sem citar o
nome do enum (`Result.Ok`) — nome que pode estar sombreado no ponto onde o
residual é emitido. Um PE que captura nome deixa de preservar o comportamento, e
preservar o comportamento é a única coisa que ele não pode negociar (spec §40).

A regra que ficou: **um valor só é `Known` se voltar a ser expressão sem depender
de nome nenhum** — primitivos e arrays não vazios deles. O resto atravessa o PE
como expressão. Isso empurra a dobra de indexação para o plano 14, que é onde ela
deixa de ser detalhe: lá, junto com a eliminação de bounds check, construir o
`Result` é o ponto do trabalho, não um efeito colateral.

### Um array vazio não é residualizável

Descoberto pela equivalência sobre o corpus. `def a: Int[] = []; print(a);`
propagava o valor e emitia `print([]);` — que **não compila** (`LAP0241`: array
vazio requer anotação). O valor existe, mas a forma sintática que o representa
perdeu a anotação que a tornava válida. Array vazio saiu de `CanResidualize`.

### `FreeVariables` precisa ver nomes que não são `CoreVariable`

Também descoberto pela equivalência. `.Flag { on: true }` referencia o tipo por
**string**, não por nó de variável — então `def Flag = type { ... };` era
eliminado como código morto e o residual quebrava. O mesmo vale para anotações de
tipo (`def r: Result<Int, E> = ...`), para o nome do enum num padrão de variante
(`Result.Ok(v)`) e para argumentos genéricos nomeados.

### As duas flags precisam ser independentes de verdade

§12.7 diz que cada transformação fica atrás de uma flag "para que os testes de
equivalência possam isolar qual transformação quebrou uma propriedade". Isso só
funciona se `DeadCodeElimination: false` realmente mantiver o binding — inclusive
quando o valor é conhecido e foi propagado. Propagar o valor e **apagar** o
binding são duas transformações, e agora são duas decisões.

### `--dynamic` precisa forçar, não só declarar

Declarar `n` como desconhecido na raiz não tem efeito nenhum: o próprio
`def n = 2;` do programa sombreia a declaração. Sem entrada externa na v0.2, todo
top-level é estático por construção — então `--dynamic` marca o nome e o
especializador **recusa** conhecê-lo ao processar aquele `Let` top-level. É o que
dá ao comando um programa interessante para especializar.

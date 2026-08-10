# 15 — Ferramentas de Pesquisa e Tracing

**Milestone:** M13 · **Depende de:** 12, 13, 14 · **Projeto:** `Lapis.Cli` + `Lapis.PartialEvaluator`

Corresponde à spec §56 e à pergunta científica de §61.

---

## Objetivo

Transformar o partial evaluator em um **instrumento de medição**. A spec §61
pergunta:

> Quanto de um programa pode ser executado antecipadamente quando parte de seus
> valores é conhecida?

Responder isso exige ver o que o PE fez, por que fez, e quanto sobrou.

---

## O que será construído

### 15.1 `lapis pe`

```bash
lapis pe program.ls
lapis pe --dynamic x:Int,ys:Int[] program.ls
lapis pe --trace program.ls
lapis pe --stats program.ls
lapis pe --json program.ls
lapis pe --steps program.ls
lapis pe --disable inlining,bce program.ls
```

| Flag | Efeito |
|---|---|
| `--dynamic <n>:<T>,...` | declara bindings top-level como desconhecidos do tipo `T`. Sem isso, um programa fechado reduz a quase nada e o experimento fica sem graça |
| `--trace` | log de decisões (§15.2) |
| `--stats` | tabela de métricas (§15.3) |
| `--json` | tudo em JSON, para análise em script/notebook |
| `--steps` | imprime o residual após **cada** iteração de ponto fixo |
| `--disable <lista>` | desliga transformações individualmente (usa `PEOptions` do plano 12) |

Saída padrão: o programa residual via `CoreSourcePrinter` — código `.ls` válido,
re-executável com `lapis`.

`--dynamic` recebe o tipo porque `Unknown` carrega tipo (plano 12 §12.1); um tipo
inválido é diagnóstico de uso (exit 64).

### 15.2 Trace

Formato exatamente no espírito da spec §56:

```text
[known]       x = 10                                   program.ls(1,5)
[known]       add = <closure fn(Int,Int) Int>          program.ls(3,5)
[reduce]      10 + 20 -> 30                            program.ls(7,13)
[residualize] x + y                                    program.ls(8,13)
[unfold]      add(10, y)                               program.ls(9,1)
[specialize]  add -> add$1 (a=10, b=dynamic)           program.ls(9,1)
[branch]      if false -> else                         program.ls(11,1)
[eliminate]   dead branch (then)                       program.ls(11,8)
[prove]       0 <= 1 < 3                               program.ls(14,9)
[eliminate]   bounds check                             program.ls(14,9)
[keep]        print(30) (efeito)                       program.ls(15,1)
[limit]       unfold depth 8 atingido                  program.ls(20,1)
```

Modelo:

```csharp
public sealed record TraceEvent(
    TraceKind Kind, string Description, SourceSpan Span, int Iteration, int Depth);

public enum TraceKind {
    Known, Residualize, Reduce, Unfold, Specialize, Branch, Eliminate, Prove, Keep, Limit
}

public sealed class PETrace
{
    public void Record(TraceEvent e);
    public ImmutableArray<TraceEvent> Events { get; }
}
```

O `PETrace` é opcional (`null` desliga tudo, custo zero) e é passado por todo o
PE. **Toda** decisão relevante emite um evento — em especial `Keep` (por que
*não* otimizou) e `Limit` (onde desistiu), que são as informações que faltam na
maioria das ferramentas.

Cada evento carrega o span de origem — é por isso que o plano 05 §5.3 exige
propagação rigorosa de spans pelo desugar.

### 15.3 Métricas (`--stats`)

```text
programa:            program.ls
bindings dinâmicos:  x: Int

nós (core)           antes: 142    depois: 37     redução: 73.9%
iterações                   3      (limite 5)
constant folds             21
propagações                14
beta reductions             6
especializações             2      (add$1, mul$2)
ramos eliminados            4
braços de match eliminados  2
bounds checks               antes: 5      eliminados: 3    (60.0%)
chamadas residuais         antes: 9      depois: 2
efeitos (print)            antes: 3      depois: 3         (invariante)
limites atingidos          0
tempo                      12.4 ms
```

A linha de efeitos é uma **verificação embutida**: se `antes != depois`, o PE
violou a regra de efeitos do plano 12 §12.4 e o comando sai com erro.

`PEStatistics` já existe desde o plano 12; aqui ele ganha renderização e o modo
JSON.

### 15.4 Modo experimento (`lapis pe --experiment`)

Para responder a §61 de forma sistemática, e não anedótica:

```bash
lapis pe --experiment tests/conformance/eval/**/*.ls --out results.csv
```

Para cada programa e para cada configuração de "quanto se sabe" (0%, 25%, 50%,
75%, 100% dos bindings top-level marcados como estáticos), roda o PE e escreve
uma linha de CSV:

```csv
program,static_fraction,nodes_before,nodes_after,reduction,folds,specializations,checks_removed,ms
```

Isso é o dado bruto da primeira pergunta experimental do projeto. O CSV é
consumível por qualquer notebook.

Amostragem de subconjuntos estáticos: determinística por semente
(`--seed`), para que os experimentos sejam reproduzíveis.

### 15.5 Comparação de residuais (`lapis pe --diff`)

```bash
lapis pe --diff program.ls
```

Imprime original e residual lado a lado (ou em diff unificado), ambos via
`CoreSourcePrinter`, para inspeção manual. Útil em revisão de mudanças no PE:
uma mudança que altera o residual de um caso conhecido fica visível.

### 15.6 Verificação embutida (`lapis pe --verify`)

Executa original e residual no mesmo ambiente e compara valor, stdout e status.
É o teste de equivalência da spec §40 disponível na linha de comando, e o modo
que se usa ao investigar um contra-exemplo do gerador do plano 14.

Sai com código 1 e um relatório se divergirem.

---

## Testes necessários

### CLI

| Teste | Asserção |
|---|---|
| `Pe_Default_PrintsResidualSource` | saída é `.ls` válido |
| `Pe_Residual_IsExecutable` | `lapis pe f.ls > r.ls && lapis r.ls` ⇒ mesma saída de `lapis f.ls` |
| `Pe_Dynamic_Flag_MarksBindingUnknown` | binding não é dobrado |
| `Pe_Dynamic_MultipleBindings` | |
| `Pe_Dynamic_UnknownName_Exit64` | |
| `Pe_Dynamic_InvalidType_Exit64` | |
| `Pe_Disable_Flag_TurnsOffTransformation` | `--disable inlining` ⇒ nenhuma beta reduction no trace |
| `Pe_Steps_PrintsEachIteration` | nº de blocos == nº de iterações |
| `Pe_Json_SchemaStable` | snapshot do schema |

### Trace

| Teste | Asserção |
|---|---|
| `Trace_Reduce_EmittedForFold` | `10+20` ⇒ evento `Reduce` |
| `Trace_Residualize_EmittedForDynamic` | `x+20` ⇒ evento `Residualize` |
| `Trace_Unfold_And_Specialize` | `add(10,x)` ⇒ ambos os eventos |
| `Trace_Eliminate_DeadBranch` | |
| `Trace_Prove_And_Eliminate_BoundsCheck` | exemplo da spec §42 ⇒ `Prove` + `Eliminate` |
| `Trace_Keep_ForEffectfulCall` | `print(30)` ⇒ evento `Keep` com motivo "efeito" |
| `Trace_Limit_WhenDepthExceeded` | ⇒ evento `Limit` |
| `Trace_EventsHaveValidSpans` | property: todo span dentro do arquivo |
| `Trace_Disabled_HasNoOverhead` | `PETrace == null` ⇒ nenhuma alocação (teste de alocação) |
| `Trace_Deterministic` | duas execuções ⇒ mesma sequência |
| `Trace_Snapshot_SpecExample56` | saída de exemplo da spec §56 |

### Estatísticas

| Teste | Asserção |
|---|---|
| `Stats_NodeCounts_Match_ActualAst` | contagens conferem com travessia independente |
| `Stats_EffectCount_Invariant` | `print` antes == depois |
| `Stats_EffectMismatch_FailsCommand` | injetar bug ⇒ exit != 0 |
| `Stats_BoundsChecks_Counted` | |
| `Stats_Json_RoundTrips` | |
| `Stats_Deterministic` | |

### Experimento

| Teste | Asserção |
|---|---|
| `Experiment_ProducesCsvHeader` | colunas exatas |
| `Experiment_OneRowPerProgramPerFraction` | contagem correta |
| `Experiment_Seed_IsReproducible` | mesma semente ⇒ mesmo CSV |
| `Experiment_HandlesFailingProgram` | programa com erro não derruba o lote; linha marcada |
| `Experiment_ReductionIsMonotoneOnCorpus` | métrica reportada, não asseverada (documenta contra-exemplos) |

### Verificação e diff

| Teste | Asserção |
|---|---|
| `Verify_Equivalent_Exit0` | |
| `Verify_Divergent_Exit1_WithReport` | injetar PE incorreto |
| `Verify_ComparesStdout` | divergência só em efeitos é detectada |
| `Verify_ComparesAbortStatus` | |
| `Diff_ShowsBothPrograms` | |
| `Diff_NoChange_ReportsIdentical` | |

---

## Critérios de conclusão

- [ ] `lapis pe --trace` produzindo a saída da spec §56.
- [ ] Residual sempre executável (`lapis pe f.ls | lapis -` equivalente).
- [ ] `--stats` com verificação embutida de invariância de efeitos.
- [ ] `--experiment` gerando CSV reprodutível sobre todo o corpus.
- [ ] `--verify` disponível como ferramenta de investigação.
- [ ] Primeiro conjunto de dados de §61 coletado e registrado em
      `docs/experiments/`.

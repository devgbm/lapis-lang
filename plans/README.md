# LapisLang — Planos de Implementação (v0.2)

Este diretório contém o plano completo de implementação da LapisLang 0.2, do zero.
A especificação de referência está em [`../spec/lapislang-0.2.md`](../spec/lapislang-0.2.md).

Todo o código anterior (v0.1, baseado em `Binder`/`Symbol`) foi removido. A nova
implementação segue a arquitetura de pipeline descrita na spec §35–§36 e §59:

```
.ls → Lexer → Parser → Surface AST → Desugar → Core AST → TypeChecker → Typed Core AST → Evaluator → Value
                                                    ↓
                                          PartialEvaluator → Residual Core AST
```

---

## Como ler estes planos

Cada plano é auto-contido e segue a mesma estrutura:

| Seção | Conteúdo |
|---|---|
| **Objetivo** | o que este componente resolve |
| **Escopo** | o que entra e o que fica de fora |
| **O que será construído** | arquivos, tipos e assinaturas C# concretas |
| **Decisões de design** | escolhas feitas e por quê |
| **Testes necessários** | lista de casos, organizados por categoria |
| **Critérios de conclusão** | checklist verificável de "pronto" |

Nenhum plano deve ser iniciado antes que suas dependências estejam com os
critérios de conclusão satisfeitos.

---

## Índice

### Fundação

| # | Plano | Projeto | Milestone |
|---|---|---|---|
| 00 | [Arquitetura e convenções](00-architecture-and-conventions.md) | — | M0 ✅ |
| 01 | [Esqueleto da solução e CI](01-solution-skeleton.md) | `LapisLang.slnx` | M0 ✅ |
| 02 | [AST (Surface + Core + Types)](02-ast.md) | `Lapis.Ast` | M1–M4 ✅ |
| 03 | [Lexer](03-lexer.md) | `Lapis.Lexer` | M1 ✅ |
| 04 | [Parser](04-parser.md) | `Lapis.Parser` | M1–M4 ✅ |
| 05 | [Desugar → Core AST](05-desugar.md) | `Lapis.Desugar` | M1–M3 ✅ |
| 06 | [Type Checker](06-typechecker.md) | `Lapis.TypeChecker` | M1–M4 ✅ |
| 07 | [Runtime (valores, ambiente, primitivas)](07-runtime.md) | `Lapis.Runtime` | M1–M2 ✅ |
| 08 | [Evaluator](08-evaluator.md) | `Lapis.Evaluator` | M1–M4 ✅ |
| 09 | [Prelude (`Result`, `IndexError`, `print`)](09-prelude.md) | `Lapis.Runtime` + `prelude.ls` | M2 ✅ |
| 10 | [CLI `lapis`](10-cli.md) | `Lapis.Cli` | M1 ✅ · M5 ⬜ |
| 11 | [Testes de integração e conformidade](11-integration-and-conformance-tests.md) | `tests/` | M1–M5 |

### Pesquisa (Partial Evaluation)

| # | Plano | Projeto | Milestone |
|---|---|---|---|
| 12 | [Partial Evaluator — núcleo](12-partial-evaluator-core.md) | `Lapis.PartialEvaluator` | M6 |
| 13 | [Partial Evaluator — especialização](13-partial-evaluator-specialization.md) | `Lapis.PartialEvaluator` | M7 |
| 14 | [Partial Evaluator — análise e BCE](14-partial-evaluator-analysis-and-bce.md) | `Lapis.PartialEvaluator` | M8 |
| 15 | [Ferramentas de pesquisa e tracing](15-research-tooling-and-tracing.md) | `Lapis.Cli` | M8 |

### Metaprogramação (proposta)

Especificação: [`../spec/lapislang-macros-0.1.md`](../spec/lapislang-macros-0.1.md).
Q19–Q24 decididas; segue em proposta apenas **Q25** (o mecanismo que garante a
variante no acesso à carga) no [Apêndice C](appendix-c-decisions.md).

| # | Plano | Projeto | Milestone |
|---|---|---|---|
| 16 | [`goto` e `label`](16-goto-and-labels.md) | `Lapis.Ast` … `Lapis.Evaluator` | M6 |
| 17 | [Macro engine](17-macro-engine.md) | `Lapis.Macros` | M8 |
| 18 | [`constraint`, `throw` e contexto](18-compile-time-evaluation.md) | `Lapis.Macros` + `Lapis.Cli` | M9 |
| 19 | [Reflection](19-reflection.md) | `Lapis.Runtime` + `Lapis.TypeChecker` | M10 |
| 20 | [Macros de controle e retirada de `If`/`Match`](20-macro-prelude.md) | `prelude.ls` | M11 |

### Apêndices normativos

| Apêndice | Conteúdo |
|---|---|
| [A — Gramática](appendix-a-grammar.md) | EBNF completa da linguagem de superfície + regras de desambiguação |
| [B — Diagnósticos](appendix-b-diagnostics.md) | catálogo de códigos de erro `LAP####` |
| [C — Decisões e questões abertas](appendix-c-decisions.md) | ADRs e lacunas da spec que precisam de confirmação |
| [D — Estratégia de testes](appendix-d-test-strategy.md) | camadas de teste, ferramentas, snapshots, golden files |

---

## Milestones

| ID | Nome | Entrega observável | Planos |
|---|---|---|---|
| **M0** ✅ | Skeleton | `dotnet build` + `dotnet test` verdes numa solução vazia com CI | 00, 01 |
| **M1** ✅ | Walking skeleton | `lapis hello.ls` imprime `30` (spec §60) — cadeia completa | 02, 03, 04, 05, 06, 07, 08, 10 |
| **M2** ✅ | Arrays & Result | `[10,20,30][1] → Ok(20)`, `[..][3] → Err(OutOfBounds)` | 04, 06, 07, 08, 09 |
| **M3** ✅ | Enums, match, tipos | `enum`, `match` exaustivo, `type` + construção + acesso a campo | 02, 04, 05, 06, 08 |
| **M4** ✅ | Generics | `fn<T>` escritos pelo programador, sempre com argumentos explícitos (Q7) + const generics | 02, 04, 06, 08 |
| **M5** | Conformidade | suíte golden `tests/conformance/**/*.ls`, `examples/` executando | 10, 11 |
| **M6** | `goto`/`label` | controle de fluxo explícito; `GotoForm_EquivalentToIf` verde | 16 |
| **M7** | PE núcleo | constant folding, propagação, dead code, `lapis pe` | 12 |
| **M8** | Macro engine | `@unless`, `@square` expandindo; `lapis expand` | 17 |
| **M9** | Compile time | `@post` com deduplicação de rota; `throw` e contexto | 18 |
| **M10** | Reflection | `reflect(Color).variants` nas duas fases | 19 |
| **M11** | Macros de controle | `@if`/`@while`/`@match` no prelude; retirada **condicional** de `Match` | 20 |
| **M12** | PE especialização | beta reduction, inlining, especialização de funções | 13 |
| **M13** | PE análise | range analysis, bounds-check elimination, `lapis pe --trace` | 14, 15 |
| **M14** | Equivalência | property-based: `eval(P,S) ≡ eval(PE(P,S),S)` | 11, 14 |

**Por que as macros vêm antes do partial evaluator** (decisão do autor), e por que
o **M7 é a exceção**:

Uma `constraint` recebe capturas sintáticas e precisa reduzi-las a valores para
decidir — dobrar `"route." + path`, avaliar `arrayLength(faltando) > 0`. Isso é
exatamente constant folding e propagação, ou seja, o **núcleo do plano 12**.

Por isso o M7 antecipa a *fatia mínima* do PE — folding e propagação —, e só ela.
Especialização (13) e análise (14) continuam depois das macros, onde já enfrentam
**laços**, que o `goto` para trás do M6 trouxe (Q24).

O M6 vir primeiro também paga uma dívida: o plano 14 (bounds-check elimination)
**quer** um CFG, e agora o encontra pronto em vez de precisar inventá-lo.

**M1 é o marco crítico.** Nada de partial evaluation antes de M1 estar estável
(spec §58, §60).

---

## Ordem de execução recomendada

```
00 → 01 → 02 → 03 → 04 → 05 → 07 → 06 → 08 → 10   (M1)
              ↓
             09 → (arrays/index em 04,06,07,08)     (M2)
              ↓
        enums/match/type em 02,04,05,06,08          (M3)
              ↓
        generics em 02,04,06,08                     (M4)
              ↓
             11                                     (M5)
              ↓
             16                                     (M6)
              ↓
             12  (fatia mínima: folding + propagação) (M7)
              ↓
        17 → 18 → 19 → 20                           (M8–M11)
              ↓
        13 → 14 → 15                                (M12–M14)
```

Observação sobre a ordem da spec: a spec §43–§52 sugere construir o evaluator
(Etapa 4) **antes** do type checker (Etapa 5) e do desugar (Etapa 6). Estes planos
invertem essa ordem: o desugar entra já em M1, e o evaluator trabalha desde o
primeiro dia sobre a **Core AST**. Motivo: a spec §36 define o evaluator como
`Typed Core AST → Value`; construir um evaluator sobre a Surface AST criaria
trabalho descartável e duas semânticas concorrentes. O escopo de cada etapa
continua o mesmo — apenas a Core AST nasce menor e cresce junto.

---

## Princípios inegociáveis

Derivados da spec §58, valem para todos os planos:

1. **Semântica antes de otimização.** O evaluator é a referência. Se o PE e o
   evaluator discordam, o PE está errado — por definição.
2. **Runtime mínimo.** Se dá para expressar na própria linguagem (`Result`,
   `IndexError`, `Option`), expressa-se na linguagem, no `prelude.ls`.
3. **Erro de runtime ≠ `Result` da linguagem.** `numbers[100]` produz
   `Err(IndexError.OutOfBounds)`, nunca uma exceção C#. Exceção C# significa bug
   da implementação (spec §30).
4. **Nenhum projeto de fase depende de projeto de fase posterior.** `Lapis.Ast`
   não conhece o evaluator; `Lapis.Parser` não executa código.
5. **Localização de origem em tudo.** Todo token e todo nó de AST carrega
   `SourceSpan` desde o primeiro commit (spec §44).

---

## Lacunas da spec — decididas

Estas lacunas foram levantadas durante o planejamento e **decididas pelo autor da
spec**. O registro completo, com justificativa e consequências, está no
[Apêndice C](appendix-c-decisions.md).

| # | Lacuna | Decisão |
|---|---|---|
| Q1 | §13/§14 usam a forma de *uso* na *declaração* de generics | Declaração com parâmetros nomeados: `type<T, N: Int>`; uso passa valores: `FixedArray<Int, 3>` |
| Q2 | §24 tem `Construct` na Core AST, mas nenhuma sintaxe de construção é definida | **Ponto inicial**: `.User { id: 1, name: "x" }`. Elimina a ambiguidade com `if`/`match` sem regra contextual |
| Q3 | §15/§16 usam `Ok(10)` (nu) e `IndexError.OutOfBounds` (qualificado) | **Qualificação completa obrigatória**: `Result.Ok(1)`. Sem injeção de variantes no escopo |
| Q4 | §44 não lista `!`, `&&`, `\|\|` | Adicionados, com curto-circuito |
| Q5 | `identity<Int>(10)` conflita com `a < b` | Backtracking limitado: é generic se o `>` for seguido de `(`, de `.`, ou de algo que não inicia expressão |
| Q6 | §22 não define exaustividade de `match` | Exaustividade obrigatória; `_` permitido |
| Q7 | inferência de argumentos genéricos | **Sempre explícitos**, inclusive em variantes: `Option<Int>.Some(20)`. `print` deixa de ser genérico (`fn(Any) Void`) para não obrigar `print<Int>(x)` |
| Q8 | a spec não menciona recursão | **Sem recursão na v0.2** |
| Q9 | divisão inteira por zero | Produz o maior `Int`. A divisão vira total — sem caminho de aborto, e o PE ganha aritmética sempre pura |
| Q16 | statements que terminam em bloco exigem `;`? | Não exigem — é o que o `abs` da própria spec §12 pressupõe |
| Q17 | função literal como argumento genérico colide com tipo de função | Só escrevível em posição de expressão; em posição de tipo, `fn(Int) Int` é um tipo |
| Q18 | o que pode ser argumento const? | O que for resolvível em compilação. Um parâmetro const **é** constante e pode ser repassado, como constante simbólica |

**Nenhuma lacuna da 0.2 segue aguardando decisão.** A spec foi atualizada de acordo —
hoje está na **0.2.3**, com o changelog de cada decisão no topo do arquivo.

A proposta de metaprogramação abriu sete novas. **Seis decididas:**

| # | Lacuna | Decisão |
|---|---|---|
| Q19 | como se declara uma macro | `macro <nome> match ... expand { };` — macro não é first-class citizen |
| Q20 | `@match` desestrutura carga? | Não: compara variantes. Dissolve a necessidade de `enumTag`/`enumPayload` |
| Q21 | que linguagem roda em `constraint` | a própria, no mesmo evaluator |
| Q22 | `if`/`while`/`match` viram prelude? | Sim. `match` deixa de ser keyword; `if` sobrevive só em `goto ... if ...` |
| Q23 | como a carga é lida | **campo do escrutinado**: `result.value`. Cai na máquina de campos do M3 |
| Q24 | `goto` salta para trás? | Sim, sem sair do escopo. Traz `@while` — e o fim da terminação por construção |

**Em proposta, uma:** **Q25** — o mecanismo que garante a variante no ponto do
acesso. A spec adota análise de dominância sobre o grafo de `Labeled`, que é a mesma
que o plano 14 constrói para bounds-check elimination. Enquanto ela não existir,
`Match` permanece na Core para os casos com carga.

---

## Pré-requisito de ambiente

.NET SDK **10.0**:

```bash
apt-get install -y dotnet-sdk-10.0     # Ubuntu 24.04
# ou
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
```

Neste container o instalador oficial está bloqueado pela política de egress; o
SDK 10.0.110 foi instalado pelo repositório do Ubuntu. Detalhes no
[plano 01](01-solution-skeleton.md#nota-de-ambiente).

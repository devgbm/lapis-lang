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
| 02 | [AST (Surface + Core + Types)](02-ast.md) | `Lapis.Ast` | M1 ✅ |
| 03 | [Lexer](03-lexer.md) | `Lapis.Lexer` | M1 ✅ |
| 04 | [Parser](04-parser.md) | `Lapis.Parser` | M1 ✅ · M2–M3 ⬜ |
| 05 | [Desugar → Core AST](05-desugar.md) | `Lapis.Desugar` | M1 ✅ · M2–M3 ⬜ |
| 06 | [Type Checker](06-typechecker.md) | `Lapis.TypeChecker` | M1 ✅ · M2–M4 ⬜ |
| 07 | [Runtime (valores, ambiente, primitivas)](07-runtime.md) | `Lapis.Runtime` | M1 ✅ · M2 ⬜ |
| 08 | [Evaluator](08-evaluator.md) | `Lapis.Evaluator` | M1 ✅ · M2–M3 ⬜ |
| 09 | [Prelude (`Result`, `IndexError`, `print`)](09-prelude.md) | `Lapis.Runtime` + `prelude.ls` | M2 |
| 10 | [CLI `lapis`](10-cli.md) | `Lapis.Cli` | M1 ✅ · M5 ⬜ |
| 11 | [Testes de integração e conformidade](11-integration-and-conformance-tests.md) | `tests/` | M1–M5 |

### Pesquisa (Partial Evaluation)

| # | Plano | Projeto | Milestone |
|---|---|---|---|
| 12 | [Partial Evaluator — núcleo](12-partial-evaluator-core.md) | `Lapis.PartialEvaluator` | M6 |
| 13 | [Partial Evaluator — especialização](13-partial-evaluator-specialization.md) | `Lapis.PartialEvaluator` | M7 |
| 14 | [Partial Evaluator — análise e BCE](14-partial-evaluator-analysis-and-bce.md) | `Lapis.PartialEvaluator` | M8 |
| 15 | [Ferramentas de pesquisa e tracing](15-research-tooling-and-tracing.md) | `Lapis.Cli` | M8 |

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
| **M2** | Arrays & Result | `[10,20,30][1] → Ok(20)`, `[..][3] → Err(OutOfBounds)` | 04, 06, 07, 08, 09 |
| **M3** | Enums, match, tipos | `enum`, `match` exaustivo, `type` + construção + acesso a campo | 02, 04, 05, 06, 08 |
| **M4** | Generics | generics de tipo com inferência de 1ª ordem + const generics | 06 |
| **M5** | Conformidade | suíte golden `tests/conformance/**/*.ls`, `examples/` executando | 10, 11 |
| **M6** | PE núcleo | constant folding, propagação, dead code, `lapis pe` | 12 |
| **M7** | PE especialização | beta reduction, inlining, especialização de funções | 13 |
| **M8** | PE análise | range analysis, bounds-check elimination, `lapis pe --trace` | 14, 15 |
| **M9** | Equivalência | property-based: `eval(P,S) ≡ eval(PE(P,S),S)` | 11, 14 |

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
        generics em 06                              (M4)
              ↓
             11                                     (M5)
              ↓
        12 → 13 → 14 → 15                           (M6–M9)
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

## Questões abertas na spec

Estas lacunas foram resolvidas provisoriamente para destravar a implementação.
Cada uma está registrada em [Apêndice C](appendix-c-decisions.md) com a decisão
tomada, e **deve ser confirmada pelo autor da spec**:

| # | Lacuna | Decisão provisória |
|---|---|---|
| Q1 | §13/§14 usam a forma de *uso* (`type<"value", 1, true, Int, ...>`) na *declaração* de generics | Declaração usa parâmetros nomeados: `type<T, N: Int>`; uso passa valores: `FixedArray<Int, 3>` |
| Q2 | §24 tem `Construct` na Core AST, mas nenhuma sintaxe de construção de `type` é definida | `User { id: 1, name: "x" }` + acesso `user.id`, com restrição de literal-em-condição |
| Q3 | §15/§16 usam `Ok(10)` (nu) e `IndexError.OutOfBounds` (qualificado) | Ambos válidos: `def` de enum injeta variantes no escopo, com diagnóstico em colisão |
| Q4 | §44 não lista `!`, `&&`, `\|\|` | Adicionados como extensão sinalizada (necessários para `Bool` ser útil) |
| Q5 | `identity<Int>(10)` conflita com `a < b` na gramática | Backtracking limitado: só é generic se o `>` for imediatamente seguido de `(` |
| Q6 | §22 não define exaustividade de `match` | `match` é expressão ⇒ exaustividade obrigatória; `_` permitido |
| Q7 | `print` na spec §34 é chamado sem argumentos genéricos explícitos | `print: fn<T>(value: T) Void` + inferência de 1ª ordem no call site |

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

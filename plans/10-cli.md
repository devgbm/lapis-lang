# 10 — CLI `lapis`

**Milestone:** M1 (mínimo) → M5 (subcomandos) · **Depende de:** 03–09 · **Projeto:** `Lapis.Cli`

Cobre a spec §33 e §56.

---

## Objetivo

Entregar o executável `lapis` e o **orquestrador do pipeline** — o único lugar do
código onde as fases são compostas.

---

## Escopo faseado

| Fase | Comandos | Milestone |
|---|---|---|
| A | `lapis <arquivo>.ls` (obrigatório pela spec §33) | M1 |
| B | `lapis run`, `lapis check`, `lapis ast`, `lapis desugar`, `lapis tokens` | M5 |
| C | `lapis pe` e `lapis pe --trace` | M6/M8 — detalhados no plano 15 |

---

## O que será construído

### 10.1 Orquestrador (`Lapis.Cli.Pipeline`)

```csharp
public sealed record CompilationRequest(SourceText Source, PipelineStage StopAfter);
public enum PipelineStage { Tokens, Parse, Desugar, TypeCheck, Evaluate }

public sealed record CompilationResult(
    ImmutableArray<Token>? Tokens,
    SourceFile? Surface,
    CoreProgram? Core,
    TypedProgram? Typed,
    EvaluationResult? Evaluation,
    ImmutableArray<Diagnostic> Diagnostics);

public static class Pipeline
{
    public static CompilationResult Compile(CompilationRequest request, RuntimeContext context);
}
```

Regra: para na primeira fase com `HasErrors`. Warnings não param nada.

Este tipo é o ponto de entrada usado **também** pelos testes de conformidade
(plano 11) — testar o CLI de verdade, e não uma reimplementação do pipeline, é o
que garante que a spec §60 seja de fato verificada.

### 10.2 Fase A — `lapis <arquivo>.ls`

Comportamento mínimo exigido pela spec §33:

```bash
lapis hello.ls      # executa; imprime 30
```

- Argumento único terminando em `.ls` ⇒ modo executar.
- Arquivo inexistente ou ilegível ⇒ mensagem + exit 64.
- Diagnósticos de erro ⇒ imprime todos em `stderr`, exit 65.
- Warnings ⇒ imprime em `stderr`, continua.
- Execução abortada (divisão por zero) ⇒ mensagem + exit 1.
- Sucesso ⇒ exit 0. **O valor final do programa não é impresso** — a saída vem
  exclusivamente de `print` (spec §34: a saída de `hello.ls` é só `30`).

### 10.3 Fase B — subcomandos (spec §56)

| Comando | Saída | Fase de parada |
|---|---|---|
| `lapis run <f>` | idêntico à fase A | Evaluate |
| `lapis check <f>` | apenas diagnósticos; exit 0 se limpo | TypeCheck |
| `lapis tokens <f>` | um token por linha com span | Tokens |
| `lapis ast <f>` | Surface AST em S-expression | Parse |
| `lapis desugar <f>` | Core AST em S-expression | Desugar |

Flags globais:

| Flag | Efeito |
|---|---|
| `--json` | diagnósticos em JSON (uma linha por diagnóstico) para consumo por ferramentas |
| `--no-color` | desliga ANSI; também respeita `NO_COLOR` do ambiente |
| `--source` | em `desugar`, imprime código `.ls` em vez de S-expression (usa `CoreSourcePrinter`) — não existe printer de fonte da Surface, então em qualquer outro comando é erro de uso (exit 64) |
| `--version` | versão + versão da spec implementada |

As flags são globais e aceitas em qualquer posição. O parsing é um laço explícito
sobre `args`, não `System.CommandLine`: são quatro flags ortogonais aos comandos,
e a dependência custaria mais do que resolve. **Compatibilidade:** invocar sem
subcomando (`lapis f.ls`) continua sendo `run` — a spec exige essa forma.

`--json` desliga a cor por construção: a saída é para máquina, e sequências ANSI
no meio de JSON não servem a ninguém.

### 10.4 Renderização de diagnósticos

Formato padrão, estilo compilador clássico:

```text
examples/hello.ls(4,9): error LAP0201: variável 'reslt' não existe
    4 |     print(reslt);
      |           ^^^^^
      = nota: você quis dizer 'result'?
```

- Caminho relativo ao diretório de trabalho.
- Linha/coluna 1-based, obtidas do `SourceText`.
- Cor só quando `stderr` é um terminal e `NO_COLOR` não está setado.
- Ordenação por offset; diagnósticos com o mesmo offset ordenados por código.
- Sugestão de nome próximo ("você quis dizer") por distância de Levenshtein ≤ 2 —
  barato e melhora muito a experiência de uma linguagem nova.

### 10.5 Códigos de saída

Os do plano 01 §1.4, agora efetivamente usados:

| Código | Situação |
|---|---|
| 0 | sucesso |
| 1 | execução abortada (erro de runtime da linguagem) |
| 64 | uso incorreto / arquivo não encontrado |
| 65 | erros de compilação |
| 70 | `InternalCompilerException` — imprime "erro interno do compilador" + stack trace + pedido de report |

---

## Testes necessários

Em `tests/Lapis.Cli.Tests`, invocando o `Pipeline` diretamente **e** o processo
real (um punhado de testes de processo, para pegar problemas de encoding e
códigos de saída).

### Fase A

| Teste | Asserção |
|---|---|
| `Run_HelloExample_Prints30` | stdout == `"30\n"`, exit 0 — **âncora do M1 (spec §60)** |
| `Run_MissingFile_Exit64` | mensagem em stderr, nada em stdout |
| `Run_NotLsExtension_Exit64` | `lapis foo.txt` |
| `Run_NoArgs_PrintsUsage_Exit64` | |
| `Run_SyntaxError_Exit65` | diagnóstico com linha/coluna |
| `Run_TypeError_Exit65` | |
| `Run_MultipleErrors_AllPrinted` | 3 erros ⇒ 3 linhas |
| `Run_Warning_DoesNotFail` | exit 0 com warning em stderr |
| `Run_DivideByZero_Exit1` | mensagem com span |
| `Run_ProgramValue_NotPrinted` | `def x = 42;` ⇒ stdout vazio |
| `Run_EmptyFile_Exit0` | stdout vazio |
| `Run_InternalError_Exit70` | injetar falha ⇒ mensagem de bug, exit 70 |
| `Run_StdoutIsUtf8` | `print("acentuação")` ⇒ bytes corretos |

### Fase B

| Teste | Asserção |
|---|---|
| `Check_ValidFile_Exit0_NoOutput` | |
| `Check_InvalidFile_Exit65` | não executa o programa (nenhum `print` acontece) |
| `Tokens_Output_Snapshot` | `examples/hello.ls` |
| `Ast_Output_Snapshot` | idem |
| `Desugar_Output_Snapshot` | idem |
| `Desugar_SourceFlag_IsReparseable` | `lapis desugar --source f.ls` ⇒ saída reparseável com a mesma Core |
| `Json_Diagnostics_Schema` | campos `file,line,column,code,severity,message` |
| `NoColor_Flag_StripsAnsi` | sem sequências ESC |
| `NoColor_EnvVar_Respected` | idem via `NO_COLOR=1` |
| `Version_PrintsSpecVersion` | contém `0.2` |
| `BareFile_EquivalentToRun` | `lapis f.ls` == `lapis run f.ls` |

### Renderização

| Teste | Asserção |
|---|---|
| `Render_IncludesLineAndColumn` | 1-based |
| `Render_CaretUnderlinesSpan` | largura do `^^^` == comprimento do span |
| `Render_MultiLineSpan_Truncates` | não explode em spans longos |
| `Render_TabsInSource_AlignCaret` | tab conta como 1 coluna, documentado |
| `Render_Suggestion_ForTypo` | `reslt` sugere `result` |
| `Render_NoSuggestion_WhenDistant` | `zzz` não sugere nada |
| `Render_Ordering_ByOffset` | |
| `Render_RelativePaths` | não vaza caminho absoluto do container |

### Pipeline

| Teste | Asserção |
|---|---|
| `Pipeline_StopsAtFirstFailingStage` | erro léxico ⇒ `Surface == null` |
| `Pipeline_StopAfter_Respected` | `StopAfter=Parse` ⇒ `Core == null` |
| `Pipeline_CollectsAllDiagnosticsOfStage` | não para no primeiro erro **dentro** de uma fase |
| `Pipeline_IsPure` | duas execuções ⇒ mesmos diagnósticos |

---

## Critérios de conclusão

- [x] `lapis examples/hello.ls` imprime `30` e sai 0 (spec §60).
- [x] Todos os códigos de saída implementados e testados.
- [x] Subcomandos da spec §56 (exceto `pe`, plano 15) funcionando.
- [x] Diagnósticos com linha, coluna, código, trecho e cursor.
- [x] Nenhum stack trace C# visível, exceto no exit 70.

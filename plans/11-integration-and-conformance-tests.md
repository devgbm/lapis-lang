# 11 — Testes de Integração e Conformidade

**Milestone:** M1 → M5 (e M9) · **Depende de:** 10 · **Projeto:** `Lapis.Conformance.Tests`

Corresponde à Etapa 9 da spec (§51).

---

## Objetivo

Construir a suíte que testa a linguagem **como um todo**, do texto fonte ao valor
— e que serve de especificação executável: cada afirmação testável da spec vira
um arquivo.

```text
source → lexer → parser → desugar → type checker → evaluator → Value
```

---

## O que será construído

### 11.1 Formato de caso de conformidade

Um caso = um arquivo `.ls` com um cabeçalho de expectativa em comentários. Tudo
num arquivo só (fácil de escrever, fácil de rodar com `lapis`).

```c
// expect: output
// 30
// ---
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

print(add(10, 20));
```

Diretivas suportadas:

| Diretiva | Significado |
|---|---|
| `// expect: output` | as linhas seguintes até `---` são o stdout exato esperado |
| `// expect: error LAP0201` | o código de diagnóstico esperado (um por linha; ordem irrelevante) |
| `// expect: error LAP0201 at 4:9` | com linha:coluna |
| `// expect: exit 65` | código de saída |
| `// expect: abort` | execução abortada |
| `// skip: motivo` | caso conhecido ainda não implementado (falha se **passar** — evita esquecer de reativar) |

### 11.2 Runner

```csharp
public sealed class ConformanceTests
{
    [Theory]
    [ConformanceData("tests/conformance/**/*.ls")]
    public void Run(ConformanceCase testCase) { ... }
}
```

- Descoberta por diretório, um teste xUnit por arquivo (nome do teste = caminho
  relativo) — falha aponta direto para o arquivo.
- Executa via `Pipeline` (plano 10) com `StringOutput`, comparando stdout,
  diagnósticos e exit code.
- Comparação de saída: normaliza `\r\n` → `\n`; **não** apara espaços internos.
- Cada caso roda com um `PreludeScope` compartilhado (rápido) e a suíte inteira
  tem um teste extra com prelude fresco (isolamento).

### 11.3 Organização dos casos

```
tests/conformance/
├── lexer/            erros léxicos, literais, comentários
├── syntax/           erros de parse e recuperação
├── types/            um arquivo por diagnóstico LAP02xx
├── eval/
│   ├── literals/
│   ├── operators/
│   ├── blocks/
│   ├── functions/
│   ├── closures/
│   ├── return/       ← ênfase da 0.2
│   ├── arrays/
│   ├── indexing/
│   ├── enums/
│   ├── match/
│   └── structs/
├── spec/             um arquivo por exemplo literal da spec (§3, §5, §11, §12,
│                     §14, §15, §16, §22, §32, §34, §42, §50)
└── pe/               (plano 12+) casos de partial evaluation
```

### 11.4 Cobertura obrigatória: exemplos da spec

Regra: **todo bloco de código executável da spec vira um caso em
`tests/conformance/spec/`**, nomeado pela seção. Se o exemplo da spec não roda, ou
a implementação está errada, ou a spec precisa mudar — e nos dois casos queremos
saber. Lista inicial:

| Arquivo | Spec | Verifica |
|---|---|---|
| `s03_toplevel_order.ls` | §3 | ordem de avaliação top-level |
| `s05_general_syntax.ls` | §5 | programa completo com array e índice |
| `s09_block_value.ls` | §9 | bloco vale a última expressão |
| `s09_block_no_tail.ls` | §9 | bloco sem cauda vale `Void` |
| `s11_fn_in_array.ls` | §11 | funções em literal de array |
| `s12_early_return.ls` | §12 | `abs` com retorno antecipado |
| `s12_void_return.ls` | §12 | `return;` em função `Void` |
| `s12_missing_return.ls` | §12/§26 | `LAP0272` |
| `s13_const_generics.ls` | §13 | `SomeType<"value",1,true,Int,fn() Int{return 1;}>` |
| `s14_user_type.ls` | §14 | `User`, `Box<T>` |
| `s15_enums.ls` | §15 | `Color`, `Result` |
| `s16_result.ls` | §16 | `Result` do prelude |
| `s18_arrays.ls` | §18 | array homogêneo |
| `s18_heterogeneous.ls` | §18 | `LAP0240` |
| `s21_index_returns_result.ls` | §21 | tipo é `Result<Int, IndexError>` |
| `s22_match.ls` | §22 | `match` como expressão |
| `s22_match_return.ls` | §22 | `unwrapOr` |
| `s32_closure.ls` | §32 | captura de ambiente |
| `s34_hello.ls` | §34 | saída `30` — **âncora do M1** |
| `s42_bounds.ls` | §42 | `values[1]` ⇒ `Ok(20)` |
| `s50_indexing.ls` | §50 | os três casos de indexação |

### 11.5 `examples/` como testes

Todo arquivo em `examples/` é executado pela suíte, e a saída esperada mora no
próprio arquivo, num cabeçalho `// Saída esperada:` — não num `.expected` ao
lado. Um arquivo só continua sendo mais fácil de ler e impossível de esquecer de
atualizar. Isso impede que a documentação apodreça.

| Exemplo | Conteúdo |
|---|---|
| `hello.ls` | spec §34, saída `30` |
| `functions.ls` | closures, ordem superior, retorno antecipado |
| `arrays.ls` | literais, indexação, `Ok`/`Err` |
| `result.ls` | `match` sobre `Result` |

### 11.6 Testes de propriedade da semântica

| Propriedade | Enunciado |
|---|---|
| `WellTyped_ProgramsDoNotThrow` | se o checker aceita, o evaluator não lança exceção C# |
| `Evaluation_IsDeterministic` | mesma fonte ⇒ mesmo stdout e mesmo valor |
| `Indexing_AlwaysProducesResult` | para qualquer array e índice, o valor é `Ok` ou `Err` (spec §30) |
| `Pipeline_IsPure` | rodar o pipeline 2x no mesmo processo dá o mesmo resultado |
| `Printer_Roundtrip` | `desugar(parse(print(core))) ≡ core` para todos os casos de conformidade que compilam |

Geração de programas aleatórios bem-tipados fica para o plano 14 (é lá que ela
paga o investimento, nos testes de equivalência do PE). Aqui as propriedades
rodam sobre o corpus de conformidade.

### 11.7 Regressões

`tests/conformance/regressions/` com um arquivo por bug encontrado, nomeado
`descricao.ls` (ou `issue-NNN-descricao.ls` quando houver issue). Regra do
projeto: **todo bug corrigido entra aqui antes da correção**.

### 11.8 Cobertura por diagnóstico, verificada

`DiagnosticCoverageTests` fecha o critério "pelo menos um caso por diagnóstico"
em teste, em vez de deixá-lo como intenção: lê o catálogo por reflexão, junta os
códigos que os casos **não pulados** afirmam esperar, e reprova o que sobrar.

Três guardas acompanham, porque uma lista de isenção sem manutenção vira ficção:

- toda isenção traz motivo e é reprovada se o código passar a ter caso;
- toda isenção precisa existir no catálogo;
- todo código citado numa diretiva `// expect:` precisa existir no catálogo — um
  erro de digitação faria o caso exigir algo impossível.

Isenções de hoje: `LAP0205` (reservado, ainda não emitido) e `LAP0302`
(inalcançável sem recursão — spec §8).

---

## Testes necessários (do próprio runner)

O runner é código e precisa de testes:

| Teste | Asserção |
|---|---|
| `Runner_ParsesOutputDirective` | |
| `Runner_ParsesErrorDirective_WithAndWithoutPosition` | |
| `Runner_ParsesExitDirective` | |
| `Runner_FailsOnMissingDirective` | arquivo sem `// expect:` ⇒ falha explícita |
| `Runner_SkipDirective_FailsIfCasePasses` | evita skip esquecido |
| `Runner_NormalizesLineEndings` | CRLF vs LF |
| `Runner_ReportsDiff_OnMismatch` | mensagem mostra esperado vs obtido |
| `Runner_DiscoversAllFiles` | contagem == arquivos no diretório |
| `Runner_UnexpectedDiagnostic_Fails` | diagnóstico a mais reprova o caso |
| `Runner_IsolatedPrelude_Works` | |

---

## Critérios de conclusão

- [x] Runner implementado e testado.
- [x] Todos os exemplos executáveis da spec com caso correspondente, verdes.
- [x] `examples/*.ls` executando com saída esperada.
- [x] Pelo menos um caso por diagnóstico do Apêndice B — verificado por
      `DiagnosticCoverageTests` (§11.8), com as isenções nomeadas.
- [x] As 5 propriedades de §11.6 verdes.
- [x] Diretório `regressions/` criado e documentado no `README.md`.

## O que a suíte encontrou

O ponto da conformidade não é o número de casos verdes; é o que ela derruba ao
ser escrita. No M5, seis bugs — todos com caso em `regressions/`:

| Bug | Correção |
|---|---|
| literal float acima do intervalo virava `Infinity` em silêncio | lexer reporta `LAP0006` |
| `ConstFloat` imprimia `1E+20`, forma que a linguagem não reparseia | `ToDisplayString` expande o expoente |
| `LAP0264` (aridade de variante) cascateava em `LAP0262` | o braço conta como coberto ao falhar |
| `def a: Int[] = [];` era rejeitado pelo próprio `LAP0241` que pedia a anotação | a anotação desce como tipo esperado |
| limite de profundidade produzia 201 diagnósticos | o desempilhamento não reporta |
| recuperação com fechamentos excedentes engolia o resto do arquivo | contador de aninhamento não fica negativo |

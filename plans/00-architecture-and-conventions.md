# 00 — Arquitetura e Convenções

**Milestone:** M0 · **Depende de:** — · **Projetos:** todos

---

## Objetivo

Fixar as decisões transversais que todos os outros planos assumem: layout de
projetos, direção de dependências, modelo de erros, imutabilidade da AST,
representação de posição de código e convenções de teste.

Este documento é normativo. Se um plano posterior contradiz este, este vence
(ou este é atualizado explicitamente).

---

## 1. Layout do repositório

Segue a spec §35, com adições marcadas `[+]`:

```
LapisLang/
├── src/
│   ├── Lapis.Ast/                 nós Surface + Core, tipos, SourceSpan
│   ├── Lapis.Lexer/               source → tokens
│   ├── Lapis.Parser/              tokens → Surface AST
│   ├── Lapis.Desugar/             Surface AST → Core AST
│   ├── Lapis.TypeChecker/         Core AST → Typed Core AST
│   ├── Lapis.Runtime/             Value, Environment, primitivas, prelude.ls
│   ├── Lapis.Evaluator/           Typed Core AST → Value
│   ├── Lapis.PartialEvaluator/    Core AST × StaticEnv → Residual Core AST
│   ├── Lapis.Diagnostics/     [+] Diagnostic, DiagnosticBag, códigos, renderer
│   └── Lapis.Cli/                 executável `lapis`
├── tests/
│   ├── Lapis.Lexer.Tests/
│   ├── Lapis.Parser.Tests/
│   ├── Lapis.Desugar.Tests/
│   ├── Lapis.TypeChecker.Tests/
│   ├── Lapis.Evaluator.Tests/
│   ├── Lapis.PartialEvaluator.Tests/
│   ├── Lapis.Cli.Tests/       [+] testes end-to-end do executável
│   └── Lapis.Conformance.Tests/ [+] runner de golden files .ls
├── tests/conformance/         [+] casos .ls + .expected
├── examples/                      hello.ls, functions.ls, arrays.ts, result.ls
├── plans/                     [+] estes documentos
├── spec/                      [+] lapislang-0.2.md
├── Directory.Build.props      [+] configuração comum de compilação
├── Directory.Packages.props   [+] versões centralizadas de pacotes
└── LapisLang.slnx
```

> A spec §35 escreve `LapisLang.sln`. O SDK 10 gera o formato XML `.slnx`, que é
> funcionalmente equivalente e mais legível em diff. Detalhe de tooling, sem
> efeito sobre a linguagem.

`Lapis.Diagnostics` é separado de `Lapis.Ast` porque lexer, parser, checker e o
PE produzem diagnósticos, e nenhum deles deve depender do outro só por causa
disso.

---

## 2. Grafo de dependências

Estritamente acíclico, e cada seta é verificada por teste de arquitetura:

```
Diagnostics ← Ast ← Lexer ← Parser ← Desugar ← TypeChecker ← Evaluator ← Cli
                                        ↑            ↑           ↑          ↑
                                        └──── Runtime ───────────┘          │
                                        └──── PartialEvaluator ─────────────┘
```

Regras duras:

- `Lapis.Ast` **não** referencia nada além de `Lapis.Diagnostics`. Não conhece
  evaluator, runtime nem valores (spec §36).
- `Lapis.Parser` **não** referencia `Lapis.Runtime`. Não executa código.
- `Lapis.Desugar` **não** referencia `Lapis.Runtime` nem `Lapis.Evaluator`.
- `Lapis.Runtime` conhece `Lapis.Ast` (uma `Closure` guarda um `CoreLambda`),
  mas **não** conhece `Lapis.Evaluator` — a chamada de closure é injetada.
- `Lapis.Runtime` **não** conhece `Lapis.TypeChecker`. Isso importa por causa do
  prelude: carregá-lo exige rodar o pipeline inteiro, então a **carga** do
  prelude mora no orquestrador (`Lapis.Cli`), e `Lapis.Runtime` define apenas o
  *dado* `PreludeScope`. Sem essa separação haveria o ciclo
  Runtime → Evaluator → TypeChecker → Runtime. Ver plano 09 §9.3.
- `Lapis.PartialEvaluator` pode usar `Lapis.Runtime` (valores estáticos) e
  `Lapis.Evaluator` (para reduzir subexpressões totalmente estáticas — spec §38),
  mas **nunca** define semântica própria.

Teste de arquitetura (`Lapis.Cli.Tests/ArchitectureTests.cs`): reflete sobre os
assemblies e falha se uma referência proibida existir.

---

## 3. Configuração comum

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

`Nullable` habilitado e warnings-as-errors valem desde o primeiro commit —
retrofit de nullability em um compilador é caro.

`InvariantGlobalization` é obrigatório: parsing de `Float` e formatação de saída
não podem depender de locale (`3.14` nunca vira `3,14`).

---

## 4. Convenções de código

- **AST e Core AST são imutáveis.** `sealed record` com propriedades `init`.
  Coleções expostas como `ImmutableArray<T>`.
- **Hierarquias fechadas.** `abstract record` com construtor privado protegido +
  todos os casos no mesmo arquivo/pasta, para o compilador ajudar em
  exaustividade de `switch`.
- **`switch` exaustivo** sobre nós de AST; braço `_ => throw new
  InternalCompilerException(...)` obrigatório, nunca `null`.
- **Nada de exceções para fluxo de controle da linguagem.** `return` é
  modelado com um registro de completion (plano 08), não com exceção C#.
- **Exceções só para bugs da implementação**: `InternalCompilerException`.
  Qualquer stack trace C# escapando para o usuário é um bug (spec §30).
- Nomes de tipos de nó: `Xyz` no Surface AST, `CoreXyz` na Core AST,
  `TypedXyz` (ou `CoreXyz` + tabela de tipos) na Typed Core AST — ver plano 02.

---

## 5. Modelo de erros

Duas categorias, jamais misturadas:

### 5.1 Diagnósticos (erros do usuário)

```csharp
public enum DiagnosticSeverity { Error, Warning, Info }

public sealed record Diagnostic(
    string Code,                 // "LAP0101"
    DiagnosticSeverity Severity,
    string Message,
    SourceSpan Span,
    ImmutableArray<DiagnosticNote> Notes);

public sealed record DiagnosticNote(string Message, SourceSpan? Span);

public sealed class DiagnosticBag : IEnumerable<Diagnostic>
{
    public bool HasErrors { get; }
    public void Report(Diagnostic diagnostic);
    public void ReportError(string code, SourceSpan span, string message, params DiagnosticNote[] notes);
}
```

Cada fase recebe um `DiagnosticBag`, **acumula** erros e tenta continuar
(recuperação de erro), devolvendo um resultado possivelmente parcial. O
orquestrador (plano 10) para o pipeline quando `HasErrors` é verdade ao fim de
uma fase.

Catálogo completo em [Apêndice B](appendix-b-diagnostics.md).

### 5.2 Erros internos

```csharp
public sealed class InternalCompilerException : Exception
{
    public InternalCompilerException(string message, SourceSpan? span = null);
}
```

Sinaliza estado impossível: nó de Core AST inválido, tipo ausente numa Typed
Core AST, variante de enum desconhecida na construção. Nunca deve ser lançada
por um programa `.ls` bem-formado — nem por um mal-formado que passou pelo
checker.

### 5.3 Erros semânticos do programa

Não são C#. São valores `Result` construídos no `prelude.ls`
(planos 07/09). `array[100]` retorna `Err(IndexError.OutOfBounds)`.

---

## 6. Posição de origem

`Lapis.Diagnostics`:

```csharp
public readonly record struct SourcePosition(int Offset, int Line, int Column);

public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => Start + Length;
    public static SourceSpan FromBounds(int start, int end);
    public static readonly SourceSpan Synthetic; // nós gerados pelo desugar/PE
}

public sealed class SourceText
{
    public string FileName { get; }
    public string Text { get; }
    public SourcePosition GetPosition(int offset);   // via índice de linhas binário
    public string GetLineText(int line);
}
```

`SourceSpan` guarda apenas offsets (barato); linha/coluna são calculadas sob
demanda pelo `SourceText`, para renderização de diagnóstico. Isso mantém os nós
de AST leves e ainda satisfaz a exigência de `line`/`column`/`offset` da spec §44.

Nós sintetizados (desugar, especialização do PE) usam `SourceSpan.Synthetic` mas
**devem** carregar o span do nó de origem sempre que existir — o rastro de origem
é o que torna `lapis pe --trace` legível.

---

## 7. Estratégia de testes (resumo)

Detalhamento em [Apêndice D](appendix-d-test-strategy.md).

| Camada | Ferramenta | Onde |
|---|---|---|
| Unitário | xUnit v3 | `tests/Lapis.*.Tests` |
| Snapshot de AST | printer S-expression próprio + `Verify.XUnit` | Parser/Desugar |
| Golden end-to-end | runner de `.ls` + `.expected` | `Lapis.Conformance.Tests` |
| Property-based | FsCheck | `Lapis.PartialEvaluator.Tests` |
| Arquitetura | reflexão sobre assemblies | `Lapis.Cli.Tests` |

Regras:

- Nenhum teste depende de locale, de ordem de `Dictionary` ou de path absoluto.
- Todo diagnóstico do catálogo tem pelo menos um teste que o dispara pelo código
  (`LAP0203`), não pela mensagem em texto.
- Testes de erro asseveram **código + span**, nunca a string da mensagem
  (mensagens podem mudar; códigos e spans, não).

---

## 8. Critérios de conclusão

- [ ] `Directory.Build.props` e `Directory.Packages.props` no repositório.
- [ ] Grafo de dependências documentado e coberto por teste de arquitetura.
- [ ] `SourceSpan`, `SourcePosition`, `SourceText`, `Diagnostic`,
      `DiagnosticBag`, `InternalCompilerException` implementados e testados.
- [ ] Renderer de diagnóstico produzindo saída no formato:
      `hello.ls(4,9): error LAP0101: variável 'x' não existe`.

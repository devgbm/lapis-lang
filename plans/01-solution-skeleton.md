# 01 — Esqueleto da Solução e CI

**Milestone:** M0 · **Depende de:** 00 · **Projetos:** `LapisLang.slnx`, todos

> ✅ **Executado.** Ver §"Resultado da execução" ao final para o que mudou em
> relação ao plano original.

---

## Objetivo

Ter uma solução .NET que compila, testa e empacota o executável `lapis`, com CI
verde, **antes** de qualquer linha de compilador ser escrita. Isso corresponde à
Etapa 1 da spec §43, com o escopo ampliado para incluir todos os projetos desde
já (criar projeto vazio é barato; reestruturar solução no meio de M2 não é).

---

## Escopo

**Entra:** solução, 10 projetos `src` + 8 `tests`, versionamento central de
pacotes, `dotnet run` funcional imprimindo uso, workflow de CI, `README.md`.

**Fica de fora:** qualquer lógica de linguagem. `lapis hello.ls` ainda falha com
"not implemented" ao fim deste plano — quem o faz funcionar é M1.

---

## O que será construído

### 1.1 Solução e projetos

```bash
dotnet new sln -n LapisLang

# bibliotecas
for p in Diagnostics Ast Lexer Parser Desugar TypeChecker Runtime Evaluator PartialEvaluator; do
  dotnet new classlib -o src/Lapis.$p -n Lapis.$p
  dotnet sln add src/Lapis.$p
done

# executável
dotnet new console -o src/Lapis.Cli -n Lapis.Cli
dotnet sln add src/Lapis.Cli

# testes
for p in Lexer Parser Desugar TypeChecker Evaluator PartialEvaluator Cli Conformance; do
  dotnet new xunit3 -o tests/Lapis.$p.Tests -n Lapis.$p.Tests
  dotnet sln add tests/Lapis.$p.Tests
done
```

Remover os `Class1.cs`/`UnitTest1.cs` gerados.

### 1.2 `Directory.Build.props`

Conforme plano 00 §3. Adicionar, apenas para projetos de teste
(`Condition="$(MSBuildProjectName.EndsWith('.Tests'))"`):

```xml
<IsPackable>false</IsPackable>
<CollectCoverage>true</CollectCoverage>
```

### 1.3 `Directory.Packages.props`

Versionamento central (`ManagePackageVersionsCentrally=true`):

| Pacote | Uso |
|---|---|
| `xunit.v3` | framework de teste |
| `xunit.runner.visualstudio` | runner |
| `Microsoft.NET.Test.Sdk` | host de teste |
| `Verify.XUnit` | snapshots de AST (plano 04, 05) |
| `Shouldly` | asserções legíveis |
| `FsCheck.Xunit` | property-based (plano 12–14) |
| `System.CommandLine` | parsing de argumentos do CLI (plano 10) |
| `coverlet.collector` | cobertura |

### 1.4 `Lapis.Cli` executável nomeado `lapis`

`src/Lapis.Cli/Lapis.Cli.csproj`:

```xml
<AssemblyName>lapis</AssemblyName>
<RootNamespace>Lapis.Cli</RootNamespace>
<PublishAot>false</PublishAot>
<InvariantGlobalization>true</InvariantGlobalization>
```

`Program.cs` nesta etapa: imprime texto de uso e retorna exit code 64
(`EX_USAGE`) quando não há argumentos; retorna 70 (`EX_SOFTWARE`) com
"not implemented" quando recebe um arquivo.

Códigos de saída fixados desde já (usados por testes de CLI em M1):

| Código | Significado |
|---|---|
| 0 | sucesso |
| 1 | o programa `.ls` terminou com erro de linguagem não tratado (reservado) |
| 64 | uso incorreto do CLI |
| 65 | erro de compilação (léxico, sintático, de tipos) |
| 70 | erro interno da implementação |

### 1.5 Referências entre projetos

Criar exatamente as referências do grafo do plano 00 §2 — nada além.

### 1.6 Diretórios de conteúdo

- `examples/` com os quatro arquivos da spec §35 (`hello.ls`, `functions.ls`,
  `arrays.ls`, `result.ls`), inicialmente contendo o código-alvo de cada
  milestone. Servem como documentação executável e alimentam o plano 11.
- `tests/conformance/` com subpastas por área (`lexer/`, `parser/`, `eval/`,
  `types/`, `pe/`), vazias por enquanto.

### 1.7 CI

`.github/workflows/ci.yml`:

```yaml
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet restore
      - run: dotnet build --no-restore -warnaserror
      - run: dotnet test --no-build --logger trx --collect:"XPlat Code Coverage"
      - run: dotnet format --verify-no-changes
```

### 1.8 `README.md` da raiz

Curto: o que é a linguagem, como compilar, como rodar `lapis examples/hello.ls`,
link para `spec/` e `plans/`. Deve ser atualizado ao fim de cada milestone.

---

## Decisões de design

**Por que criar todos os 10 projetos agora?** Porque o grafo de dependências é a
parte da arquitetura mais cara de corrigir depois. Projetos vazios com o teste de
arquitetura já ativo impedem que uma referência errada entre "temporariamente".

**Por que `xunit.v3`?** Suporte nativo a `net10.0`, execução paralela por classe
por padrão e melhor integração com testes teóricos parametrizados por arquivo
(usados pelo runner de conformidade).

**Por que `AssemblyName=lapis`?** A spec §33 exige o executável `lapis`. Com isso
`dotnet publish` produz diretamente o binário com o nome certo, sem wrapper.

---

## Testes necessários

### Testes de fumaça

| Teste | Asserção |
|---|---|
| `Solution_Builds` | implícito no CI: `dotnet build -warnaserror` sai 0 |
| `Cli_NoArgs_PrintsUsageAndReturns64` | stdout contém `usage: lapis`, exit code 64 |
| `Cli_UnknownFile_Returns64` | arquivo inexistente ⇒ 64 + mensagem, sem stack trace |
| `Cli_KnownFile_Returns70_NotImplemented` | placeholder desta etapa; removido em M1 |

### Testes de arquitetura (`Lapis.Cli.Tests/ArchitectureTests.cs`)

| Teste | Asserção |
|---|---|
| `Ast_DependsOnlyOnDiagnostics` | referências de `Lapis.Ast` ⊆ {`Lapis.Diagnostics`, BCL} |
| `Parser_DoesNotReferenceRuntime` | `Lapis.Runtime` ∉ referências de `Lapis.Parser` |
| `Desugar_DoesNotReferenceEvaluator` | idem para `Lapis.Evaluator` e `Lapis.Runtime` |
| `Runtime_DoesNotReferenceEvaluator` | idem |
| `NoProjectReferenceCycles` | ordenação topológica do grafo tem sucesso |

Implementação: ler `AssemblyName.GetReferencedAssemblies()` dos assemblies
carregados; filtrar prefixo `Lapis.`.

### Testes de infraestrutura

| Teste | Asserção |
|---|---|
| `AllProjects_TargetNet10` | varre `*.csproj`, todos herdam `net10.0` |
| `AllProjects_HaveNullableEnabled` | nenhum `.csproj` sobrescreve `Nullable` |
| `Examples_Directory_HasSpecFiles` | os 4 arquivos da spec §35 existem |

---

## Critérios de conclusão

- [x] `dotnet build -warnaserror` limpo.
- [x] `dotnet test` verde com os testes de fumaça e arquitetura acima.
- [x] `dotnet publish src/Lapis.Cli -c Release` produz binário `lapis`.
- [x] `./lapis` sem argumentos imprime uso e sai com 64.
- [x] `dotnet format --verify-no-changes` limpo.
- [ ] CI verde no branch (workflow criado; validação depende da execução no GitHub).

---

## Resultado da execução

`dotnet build -warnaserror`: 0 warnings, 0 erros.
`dotnet test`: **40 testes, 40 passando** (33 em `Lapis.Cli.Tests`, 1 de fiação
em cada um dos 7 demais projetos).
`dotnet format --verify-no-changes`: limpo.

### Desvios em relação ao plano original

| # | Plano dizia | Executado | Motivo |
|---|---|---|---|
| 1 | `LapisLang.sln` | `LapisLang.slnx` | o SDK 10 gera o formato XML por padrão; equivalente e melhor em diff |
| 2 | template `xunit3` | template `xunit` + `PackageReference` explícito para `xunit.v3` | o SDK 10 não expõe um template `xunit3`; os `.csproj` foram reescritos à mão para usar versionamento central |
| 3 | teste de arquitetura por reflexão sobre assemblies | leitura dos `ProjectReference` nos `.csproj` | o compilador C# omite referências a assemblies não usados; com os projetos vazios, a metadata compilada seria vazia e o teste, vácuo. O que se quer travar é a dependência **declarada** |
| 4 | `PreludeScope` em `Lapis.Runtime` | dado em `Runtime`, carga em `Lapis.Cli` | evitar o ciclo `Runtime → Evaluator → TypeChecker → Runtime`; plano 09 §9.3 atualizado |
| 5 | — | `WiringTests` nos 7 projetos de teste vazios | um projeto de teste sem testes faz o runner reclamar; o teste verifica que o assembly alvo está referenciado e será substituído pelos testes reais de cada plano |

### Versões de pacote fixadas

| Pacote | Versão |
|---|---|
| `xunit.v3` | 3.2.2 |
| `xunit.runner.visualstudio` | 3.1.5 |
| `Microsoft.NET.Test.Sdk` | 18.8.1 |
| `Shouldly` | 4.3.0 |
| `coverlet.collector` | 10.0.1 |
| `Verify.XunitV3` | 31.28.0 |
| `FsCheck.Xunit` | 3.3.4 |
| `System.CommandLine` | 2.0.10 |

### Nota de ambiente

O instalador oficial (`dot.net` / `builds.dotnet.microsoft.com`) está bloqueado
pela política de egress deste ambiente. O SDK foi instalado pelo repositório do
Ubuntu 24.04:

```bash
apt-get install -y dotnet-sdk-10.0     # 10.0.110
```

O download de pacotes NuGet funciona normalmente; apenas o endpoint de **busca**
(`azuresearch-*.nuget.org`) está bloqueado, o que afeta `dotnet package search`
mas não `restore` nem `dotnet add package`.

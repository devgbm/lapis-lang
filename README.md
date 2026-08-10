# LapisLang

Linguagem pequena, estaticamente tipada e orientada a expressões, criada como
plataforma experimental para estudar **avaliação de programas e partial
evaluation**.

- **Extensão:** `.ls` · **CLI:** `lapis` · **Implementação:** C# / .NET 10
- **Especificação:** [`spec/lapislang-0.2.md`](spec/lapislang-0.2.md)
- **Planos de implementação:** [`plans/`](plans/README.md)

```c
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

def main = fn() Void {
    def result = add(10, 20);

    print(result);
};

main();
```

```bash
lapis hello.ls
# 30
```

---

## Estado atual

| Milestone | Status |
|---|---|
| **M0** — esqueleto da solução e CI | ✅ concluído |
| **M1** — `lapis hello.ls` de ponta a ponta | ⏳ próximo |
| M2 — arrays, indexação e `Result` | ⬜ |
| M3 — enums, `match` e tipos | ⬜ |
| M4 — generics e const generics | ⬜ |
| M5 — suíte de conformidade | ⬜ |
| M6–M9 — partial evaluator | ⬜ |

Os projetos existem e compilam, mas ainda estão vazios: `lapis programa.ls`
retorna "pipeline ainda não implementado". O roteiro completo, com o que será
construído e quais testes são necessários em cada etapa, está em
[`plans/`](plans/README.md).

---

## Arquitetura

```
.ls → Lexer → Parser → Surface AST → Desugar → Core AST → TypeChecker → Typed Core AST → Evaluator → Value
                                                    ↓
                                          PartialEvaluator → Core AST residual
```

| Projeto | Responsabilidade |
|---|---|
| `Lapis.Diagnostics` | `SourceText`, `SourceSpan`, `Diagnostic`, `DiagnosticBag` |
| `Lapis.Ast` | nós Surface e Core, modelo de tipos, printers |
| `Lapis.Lexer` | fonte → tokens |
| `Lapis.Parser` | tokens → Surface AST |
| `Lapis.Desugar` | Surface AST → Core AST |
| `Lapis.TypeChecker` | Core AST → Typed Core AST |
| `Lapis.Runtime` | valores, ambiente, primitivas, `prelude.ls` |
| `Lapis.Evaluator` | Typed Core AST → `Value` (**referência semântica**) |
| `Lapis.PartialEvaluator` | Core AST × ambiente estático → Core AST residual |
| `Lapis.Cli` | executável `lapis` e orquestração do pipeline |

O grafo de dependências entre esses projetos é travado por testes de arquitetura
(`tests/Lapis.Cli.Tests/ArchitectureTests.cs`).

---

## Desenvolvimento

Requer o **.NET SDK 10**.

```bash
dotnet build -warnaserror     # compilar (zero warnings é obrigatório)
dotnet test                   # rodar a suíte
dotnet format                 # formatar
dotnet publish src/Lapis.Cli -c Release -o artifacts/lapis
./artifacts/lapis/lapis examples/hello.ls
```

### Layout

```
src/          projetos do compilador
tests/        projetos de teste
tests/conformance/   casos .ls executados de ponta a ponta (plano 11)
examples/     programas de exemplo, verificados pela suíte
plans/        planos de implementação
spec/         especificação da linguagem
```

### Convenções

- Warnings são erros; `Nullable` habilitado em toda a solução.
- Versões de pacote apenas em `Directory.Packages.props`.
- Testes de erro asseveram **código de diagnóstico + span**, nunca a mensagem.
- Todo bug corrigido entra antes em `tests/conformance/regressions/`.

Detalhes em [`plans/00-architecture-and-conventions.md`](plans/00-architecture-and-conventions.md)
e [`plans/appendix-d-test-strategy.md`](plans/appendix-d-test-strategy.md).

---

## Objetivo de pesquisa

> Quanto de um programa pode ser executado antecipadamente quando parte de seus
> valores é conhecida? — spec §61

O evaluator é a implementação de referência da semântica. O partial evaluator é
validado contra ele pela propriedade

```
evaluate(P, S) ≡ evaluate(partialEvaluate(P, S), S)
```

testada com programas gerados (planos 12–14).

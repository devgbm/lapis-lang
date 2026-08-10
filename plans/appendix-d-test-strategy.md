# Apêndice D — Estratégia de Testes

Como a spec §51–§55 coloca os testes no centro do projeto (o partial evaluator é
validado *por* testes de equivalência), esta é uma peça de arquitetura, não uma
formalidade.

---

## D.1 Camadas

```
                        quantidade    velocidade   o que pega
┌──────────────────────┐
│ property-based       │    ~15 props    lenta     bugs que ninguém imaginou
├──────────────────────┤
│ conformidade (.ls)   │    ~250 casos   média     regressões de semântica
├──────────────────────┤
│ snapshot (AST)       │    ~120         rápida    mudanças acidentais de forma
├──────────────────────┤
│ unitário             │    ~600         rápida    lógica de componente
└──────────────────────┘
```

A pirâmide é deliberadamente "gorda no meio": num compilador, o teste de
conformidade end-to-end é o que corresponde ao que o usuário observa, e é o mais
barato de escrever (um arquivo `.ls`).

---

## D.2 Ferramentas

| Ferramenta | Uso | Onde |
|---|---|---|
| xUnit v3 | framework | todos |
| Shouldly | asserções | todos |
| Verify.XUnit | snapshots `.verified.txt` | Parser, Desugar, CLI |
| FsCheck.Xunit | property-based | Lexer, Parser, PE |
| coverlet | cobertura | CI |

**Sem mocks.** Um compilador é uma cadeia de funções puras; substituir uma fase
por um mock testaria a fiação, não o comportamento. As duas únicas abstrações
injetadas são `IOutput` (para capturar `print`) e `Invoker` (para quebrar o ciclo
Runtime↔Evaluator).

---

## D.3 Convenções

### Nomenclatura

```
Metodo_Situacao_ResultadoEsperado
Eval_Index_OutOfBounds_ProducesErr
Parse_Def_MissingSemicolon_ReportsLAP0102
```

### Testes de erro

Sempre asseveram **código + span**:

```csharp
result.Diagnostics.ShouldHaveSingle()
      .ShouldHave(code: "LAP0201", span: Span(4, 5));
```

Nunca a mensagem — mensagens mudam, códigos e spans não.

### Snapshots

- Um `.verified.txt` por caso, versionado.
- `.received.txt` no `.gitignore`.
- Snapshot que muda numa PR exige justificativa na descrição: um snapshot
  atualizado sem explicação é uma regressão semântica aceita por descuido.

### Determinismo

Nenhum teste pode depender de:

- cultura (`InvariantGlobalization` está ligado; há testes explícitos sob `pt-BR`);
- ordem de iteração de `Dictionary` (usar `ImmutableSortedDictionary` onde a
  ordem sair em algum output);
- caminho absoluto (diagnósticos usam caminho relativo);
- relógio ou aleatoriedade não semeada (property tests usam semente fixa em CI).

---

## D.4 Testes de conformidade

Formato e runner detalhados no plano 11. Resumo:

```c
// expect: output
// 30
// ---
<código>
```

Um arquivo = um teste xUnit, nomeado pelo caminho. Adicionar um caso é criar um
arquivo — o que torna "escrever teste antes de corrigir o bug" um hábito barato.

---

## D.5 Property-based: catálogo

| # | Propriedade | Plano |
|---|---|---|
| P1 | lexer nunca lança e sempre termina | 03 |
| P2 | `source[token.Span] == token.Text` | 03 |
| P3 | parser nunca lança e sempre termina | 04 |
| P4 | desugar é total e determinístico | 05 |
| P5 | Core não contém `AndAlso`/`OrElse` | 05 |
| P6 | todo nó da Core tem tipo após o checker | 06 |
| P7 | programa bem-tipado não lança no evaluator | 08, 11 |
| P8 | indexação sempre produz `Ok` ou `Err` | 08, 11 |
| P9 | avaliação é determinística | 11 |
| P10 | `desugar(parse(print(core))) ≡ core` | 02, 11 |
| P11 | `eval(P,S) ≡ eval(PE(P,S),S)` — valor, stdout e status | 12, 13, 14 |
| P12 | `PE(PE(P)) ≡ PE(P)` | 12 |
| P13 | residual sempre re-tipável | 12 |
| P14 | efeitos não duplicados, eliminados nem reordenados | 12 |
| P15 | nenhum acesso não-checado (BCE) é inválido | 14 |

**P11 é a propriedade central do projeto** (spec §55).

---

## D.6 Gerador de programas

Construído no plano 14, usado por P11–P15.

- **Dirigido por tipo:** escolhe um tipo alvo e gera uma expressão daquele tipo,
  garantindo boa tipagem por construção. Programas gerados **devem** passar no
  checker — um gerado que falha é bug do gerador (teste
  `Generator_ProducesWellTypedPrograms`).
- **Cobertura por nó:** a suíte reporta quantas vezes cada nó da Core apareceu;
  cobertura zero em um nó reprova.
- **Shrinking obrigatório**, preservando boa tipagem.
- **Semente fixa em CI**, aleatória em execução local (para achar coisas novas);
  toda falha nova vira um arquivo em `tests/conformance/regressions/`.

---

## D.7 Regressões

`tests/conformance/regressions/issue-NNN-descricao.ls`.

Regra do projeto: **todo bug corrigido entra aqui antes da correção**, e o commit
da correção mostra o teste passando de vermelho para verde.

---

## D.8 CI

```yaml
- dotnet build -warnaserror
- dotnet test --collect:"XPlat Code Coverage"
- dotnet format --verify-no-changes
```

Portões (a partir do M1, não antes — portão em repositório vazio só atrapalha):

| Portão | Limite |
|---|---|
| build | zero warnings |
| testes | 100% passando; zero `Skip` sem justificativa |
| cobertura de linha | ≥ 85% em `src/Lapis.*` (exceto `Lapis.Cli`) |
| tempo da suíte | < 2 min sem property tests; < 10 min com |
| snapshots | zero `.received.txt` remanescentes |

Cobertura é indicador, não meta: 100% de cobertura com asserções fracas é pior
que 85% com testes de conformidade fortes. O portão existe para pegar código
morto e ramos nunca exercitados.

---

## D.9 Testes de performance

Não são portão em nenhum milestone. O evaluator é a implementação de referência
e "deve ser simples o suficiente para ser considerado a referência" (spec §58.3).

O que **é** medido, a partir do M6, e reportado em `docs/experiments/`:

| Métrica | Motivo |
|---|---|
| tempo de PE por programa | detectar explosão combinatória |
| razão de nós residual/original | responder à spec §61 |
| número de especializações | detectar polivariância descontrolada |
| iterações até ponto fixo | detectar não convergência |

Regressão de performance vira issue, não build vermelho.

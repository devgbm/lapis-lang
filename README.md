# LapisLang

Linguagem pequena, estaticamente tipada e orientada a expressões, criada como
plataforma experimental para estudar **avaliação de programas e partial
evaluation**.

- **Extensão:** `.ls` · **CLI:** `lapis` · **Implementação:** C# / .NET 10
- **Especificação:** [`spec/lapislang-0.2.md`](spec/lapislang-0.2.md)
- **Planos de implementação:** [`plans/`](plans/README.md)
- **Extensão planejada:** [macros, reflection e `goto`/`label`](spec/lapislang-macros-0.1.md)

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
| **M1** — `lapis hello.ls` de ponta a ponta | ✅ concluído |
| **M2** — arrays, indexação e `Result` | ✅ concluído |
| **M3** — `match`, tipos definidos pelo usuário | ✅ concluído |
| **M4** — generics escritos pelo programador | ✅ concluído |
| **M5** — suíte de conformidade e subcomandos do CLI | ✅ concluído |
| **M6** — `goto`/`label` e mutação com `var` | ✅ concluído |
| **M7** — PE núcleo (folding, propagação, dead code) | ✅ concluído |
| **M8** — macro engine (`match`/`expand`, higiene, `lapis expand`) | ✅ concluído |
| M9–M11 — `constraint`, reflection, macros do prelude | ⏳ próximo |
| M12–M14 — PE: especialização, análise, equivalência | ⬜ |

**1373 testes** cobrindo lexer, parser, macros, desugar, type checker, runtime,
evaluator, partial evaluator e CLI — entre eles uma **suíte de conformidade** de
157 programas `.ls` que é a especificação executável do projeto: cada afirmação testável da
spec é um arquivo, e o nome do teste que falha já é o arquivo a abrir.

A linguagem já roda programas de verdade: funções de primeira classe com
closures, `return` explícito com verificação de "retorna em todos os caminhos",
arrays com indexação segura, enums, `match` exaustivo, tipos definidos pelo
usuário, generics (inclusive const generics), `goto`/`label`, mutação com `var` e
um prelude escrito na própria linguagem.

```c
def numbers = [10, 20, 30];

def unwrapOr = fn<T>(r: Result<T, IndexError>, fallback: T) T {
    match r {
        Result.Ok(value) => return value,
        Result.Err(error) => return fallback
    }
};

print(unwrapOr<Int>(numbers[1], 0));    // 20
print(unwrapOr<Int>(numbers[9], 0));    // 0
```

Indexar **sempre** devolve `Result` (spec §21): a falha aparece no tipo, e o
acesso fora de limites nunca lança. `match` deve ser exaustivo, porque é uma
expressão e precisa produzir um valor em toda execução. E argumentos genéricos
são **sempre explícitos** — não há inferência na 0.2 (decisão Q7), o que mantém o
type checker previsível e deixa a porta aberta para inferência depois.

Const generics são o caso interessante para a pesquisa: `N` é conhecido no ponto
da instanciação mesmo quando o resto só existe em execução. Um argumento const
tem de ser resolvível em tempo de compilação — e um parâmetro const **é**
constante, então pode ser repassado adiante como constante simbólica.

```c
def scale = fn<N: Int>(x: Int) Int {
    return x * N;
};

def twice = fn<M: Int>(x: Int) Int {
    return scale<M>(x) + scale<M>(x);
};

print(scale<3>(5));    // 15
print(twice<3>(5));    // 30
```

`goto`/`label` (M6) dão controle de fluxo explícito — saída antecipada sem
aninhamento, e um grafo de fluxo que o partial evaluator vai analisar. O desugar
decompõe o bloco em blocos básicos: cada `label` abre um *join point*, e o
segmento anterior é fechado com um salto implícito.

```c
def buscar = fn(indice: Int) Int {
    goto invalido if indice < 0;
    goto invalido if indice > 2;

    match valores[indice] {
        Result.Ok(v) => return v,
        Result.Err(e) => return -1
    }

    label invalido;
    return -1;
};
```

Saltar para trás é permitido, e com isso a terminação deixa de ser garantida por
construção: o evaluator conta saltos e aborta com `LAP0303` em vez de travar.

Para um laço **avançar** falta uma peça, e é a decisão Q25: mutação com `var`.

```c
var i = 0;

label repete;
i = i + 1;
print(i);
goto repete if i < 3;   // 1, 2, 3
```

`def` continua definitivo; `var` pode ser reatribuído com `x = e;` — statement, não
expressão, o que elimina `if (x = 1)` e a confusão entre `=` e `==`. A restrição
que faz a mutação caber sem virar um buraco: **um `var` não atravessa fronteira de
função**. Nenhuma closure captura `var`, então não há aliasing, e a closure segue
sendo (código, ambiente imutável) para o partial evaluator — que é a premissa dos
planos 12 a 14.

O **partial evaluator** (M7) é a razão do projeto existir. `lapis pe` imprime o
programa residual — o mesmo programa com tudo o que já dava para decidir,
decidido:

```bash
$ lapis pe examples/partial-evaluation.ls --stats
print(30);
{
    print("ativo");
};
def f = fn(v: Int) Int {
    return 15 + v
};
print(f(6));
nós: 50 → 23; dobras: 9; ramos eliminados: 1; bindings eliminados: 0
```

A garantia é `evaluate(P) ≡ evaluate(PE(P))` (spec §40), e ela é verificada sobre
**todo o corpus de conformidade** a cada execução da suíte: mesma saída, mesmo
desfecho, residual reparseável e bem-tipado, `PE(PE(P)) == PE(P)`.

O que o PE não faz, de propósito: `print` nunca executa em tempo de
especialização, nem com argumento conhecido — executá-lo moveria a saída do
programa para o tempo de compilação. Nenhum efeito é duplicado, eliminado ou
reordenado. Sem `--dynamic` um programa fechado tende ao resultado já avaliado,
porque a 0.2 não tem entrada externa; `--dynamic=nome` declara um nome como
desconhecido e é o que torna o exercício interessante.

**Macros** (M8) são sintaxe → sintaxe, definidas pela própria linguagem e
expandidas entre o parser e o desugar:

```c
macro unless
    match Expression:condition Block:body
    expand {
        goto done if condition;
        body;
        label done;
    };

@unless pular {
    print("executou");
}
```

Uma macro é declarada por `macro`, não por `def`: ela não é first-class citizen
(Q19) — não existe em runtime, não é argumento, não é retorno, e some do programa
antes do desugar. A invocação começa com `@` e **não** exige parênteses: o que ela
consome é determinado pelo `match` da própria macro, e um literal sintático como o
`in` de `@bind x in 7 { }` pertence àquela macro sem virar palavra reservada.

A expansão é higiênica — o `temp` que a macro introduz não é o `temp` do programa
— e `lapis expand` imprime a Surface AST depois da expansão, que é a ferramenta
sem a qual macro vira adivinhação.

O que falta (M9 em diante): `constraint` em tempo de compilação, reflection, as
macros do prelude, e o resto do partial evaluator — especialização de chamadas e
eliminação de bounds check. O roteiro completo está em
[`plans/`](plans/README.md).

```bash
$ lapis examples/hello.ls
30

$ lapis desugar examples/hello.ls     # Core AST
$ lapis ast examples/hello.ls         # Surface AST
$ lapis check examples/hello.ls       # só diagnósticos
$ lapis tokens examples/hello.ls      # tokens com posição

$ lapis expand examples/macros.ls            # Surface AST depois das macros
$ lapis pe examples/hello.ls                 # programa residual
$ lapis desugar --source examples/hello.ls   # Core AST de volta como `.ls`
$ lapis check --json programa.ls             # diagnósticos para ferramentas
$ lapis check --no-color programa.ls         # sem ANSI (idem NO_COLOR=1)
```

A saída de `desugar --source` é código `.ls` que reparseia para a mesma Core AST.
Não é conveniência: é a propriedade que o partial evaluator vai precisar para
emitir um programa residual executável — e ela é verificada sobre o corpus
inteiro a cada execução da suíte.

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

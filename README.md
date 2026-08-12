# LapisLang

Linguagem pequena, estaticamente tipada e orientada a expressões, criada como
plataforma experimental para estudar **avaliação de programas e partial
evaluation**.

- **Extensão:** `.ls` · **CLI:** `lapis` · **Implementação:** C# / .NET 10
- **Especificação:** [`spec/lapislang-0.2.md`](spec/lapislang-0.2.md)
- **Planos de implementação:** [`plans/`](plans/README.md)
- **Extensão implementada:** [macros, reflection e `goto`/`label`](spec/lapislang-macros-0.1.md)
- **Extensão planejada:** [type members e extension methods](spec/lapislang-type-members-0.1.md)

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
| **M9** — `constraint`, `throw` e contexto de compilação | ✅ concluído |
| **M10** — reflection nas duas fases | ✅ concluído |
| **M11** — `@unless` e `@while` no prelude | ✅ concluído |
| **M12** — span com o tamanho no tipo | ✅ concluído |
| **M13** — membros de tipo e atribuição a campo | ✅ concluído |
| **M14** — métodos de instância e `self` | ✅ concluído |
| **M15** — membros sobre tipos genéricos (`Result<?, ?>`) | ✅ concluído |
| M16 — `is`: testar variante e desembrulhar carga | ⏳ próximo |
| M17–M19 — PE: especialização, análise, equivalência | ⬜ |

**1682 testes** cobrindo lexer, parser, macros, desugar, type checker, runtime,
evaluator, partial evaluator e CLI — entre eles uma **suíte de conformidade** de
202 programas `.ls` que é a especificação executável do projeto: cada afirmação testável da
spec é um arquivo, e o nome do teste que falha já é o arquivo a abrir.

A linguagem já roda programas de verdade: funções de primeira classe com
closures, `return` explícito com verificação de "retorna em todos os caminhos",
spans com indexação checada em compilação, enums, `match` exaustivo, tipos definidos pelo
usuário, membros de tipo, generics (inclusive const generics), `goto`/`label`,
mutação com `var`,
macros higiênicas com validação em tempo de compilação, reflection, e um prelude
escrito na própria linguagem — laços inclusive.

```c
def numbers = .[10, 20, 30];    // [Int;3] — o tamanho está no tipo

print(numbers[1]);              // 20 — total, os limites foram provados
print(numbers.length);          // 3  — constante de compilação
// print(numbers[3]);           // LAP0244: erro de compilação, não falha em execução

// Oito zeros não se escrevem à mão: `.[T; inicial; n]` diz elemento,
// valor inicial e quantidade.
def zeros = .[Int; 0; 8];       // [Int;8]

print(zeros.length);            // 8

var mutaveis = .[10, 20, 30];   // [Int;?] — um `var` pode receber outro tamanho

def unwrapOr = fn<T>(o: Option<T>, fallback: T) T {
    match o {
        Option.Some(value) => return value,
        Option.None => return fallback
    }
};

print(unwrapOr<Int>(mutaveis[1], 0));    // 20
print(unwrapOr<Int>(mutaveis[9], 0));    // 0
```

Um **span** é uma sequência com o tamanho no tipo, e é a diferença entre as duas
metades desse exemplo. Onde o tamanho é conhecido e o índice é constante, indexar
é **total**: o compilador verifica os limites e devolve o elemento, sem envelope
nenhum. Onde o tamanho se perde, a checagem sobra para a execução e o resultado é
um `Option<T>` — a falha aparece no tipo, e o acesso fora de limites nunca lança.
A quantidade de `.[T; inicial; n]` segue a mesma divisa: constante entra no tipo,
dinâmica dá `[T;?]`. `match` deve ser exaustivo, porque é uma expressão e precisa produzir um valor em
toda execução. E argumentos genéricos são **sempre explícitos** — não há
inferência na 0.2 (decisão Q7), o que mantém o type checker previsível e deixa a
porta aberta para inferência depois.

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

**Compile time** (M9): entre o `match` e o `expand` cabe um `constraint`, um bloco
que roda **durante a compilação** para validar a construção e acumular estado.

```c
macro post
    match Str:path Block:handler

    constraint {
        def key = "route.POST." + path;

        if contextHas(key) {
            throw "rota POST já registrada: " + path;
        }

        contextPut(key, path);
    }

    expand {
        print("POST " + path);
        handler;
    };

@post "/produtos" { print("criando produto"); }
@post "/produtos" { print("de novo"); }    // LAP0503, no span da invocação
```

Não é uma segunda linguagem: `constraint` é LapisLang, avaliada pelo **mesmo
evaluator** que roda o programa. O que muda entre as fases é o ambiente — em
compile time existem `throw` e as primitivas de contexto, em runtime não existe
nenhum dos dois (`throw` fora de um `constraint` é `LAP0507`, e `contextPut` num
programa é um nome livre como outro qualquer).

O contexto de compilação é a única coisa mutável do sistema, e só enquanto a
compilação dura: é estado do compilador exposto por primitivas, do mesmo jeito que
`print` expõe I/O. `contextGet` devolve `Result` porque chave ausente é falha
esperada, e falha esperada aparece no tipo (spec §30).

**Reflection** (M10) expõe os metadados do programa — nomes, campos, variantes —
como valores comuns, nas duas fases:

```c
def Color = enum { Red, Green, Blue };

print(reflect(Color).name);            // Color
print(reflect(Color).kind);            // TypeKind.Enum
print(reflect(Result).typeParameterNames);   // ["T", "E"]
```

O ponto é o que **não** existe aí: não há sistema de metadados paralelo. `TypeInfo`
é um `type` declarado no `prelude.ls`, e tudo o que vale para struct vale para ele
— igualdade estrutural, imutabilidade, indexação devolvendo `Result`. `reflect` é um
intrínseco e não um binding porque o argumento tem de ser um **tipo**, e "um tipo"
não é expressável na gramática de tipos; mesmo estatuto da indexação.

E porque é tudo valor comum, o partial evaluator dobra reflection de graça:
`reflect(User).name` residualiza como `"User"`, e o programa não paga nada por ter
usado.

E o **prelude** (M11) usa tudo isso para escrever, na própria linguagem, as
construções de controle que a LapisLang não tem:

```c
var i = 0;

@while i < 3 {          // vem do prelude — não precisa declarar
    i = i + 1;
    print(i);
}
```

`@while` são seis linhas de `prelude.ls` sobre `goto` e `label`. É a tese do
sistema de macros em uma frase: **um laço, que em qualquer outra linguagem é
trabalho de compilador, aqui é biblioteca.** E nada foi retirado do compilador
para isso — `if` e `match` continuam onde estavam, com desestruturação de carga,
que é justamente o que nenhuma macro faria com segurança (Q23). Um `@while`
quebrado não tem como regredir um programa que já funcionava.

O escopo se comporta como se esperaria: o corpo do laço é um escopo (o que ele
declara não escapa), e um `var` declarado entre dois laços é visível no segundo.
Só o que um `goto` **explícito** pode ter pulado continua invisível no destino —
que é a única forma de o contrário ser mentira. `examples/control.ls` mostra os
três casos.

O que falta: **fechar a linguagem** primeiro (M16) — o `is` —, porque o partial
evaluator precisa de um caso para cada construção, e escrevê-lo contra uma
superfície que ainda cresce significa reabri-lo a cada milestone. Com ele, o
último buraco conhecido da superfície fecha: `is` é a construção que a Q23 pedia
para ler a carga de uma variante com segurança. Os membros sobre tipos genéricos
já entraram (M15): `def Result<?, ?>.isOk` diz o alcance em vez de deduzi-lo, e o
`?` curinga dissolveu a Q27 em vez de respondê-la. O **span** já entrou (M12): o tamanho no tipo tirou do partial
evaluator o caso trivial de eliminação de bounds check e deixou com ele o
interessante — provar `i < n` para um `i` derivado de laço. Depois o resto do PE (M17–M19):
especialização de chamadas e eliminação de bounds check. O roteiro completo está em
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
.ls → Lexer → Parser → Surface AST → Macros → Surface AST → Desugar → Core AST → TypeChecker → Typed Core AST → Evaluator → Value
                                        │                                             ↓
                                   constraint ────────────────────────────→ PartialEvaluator → Core AST residual
```

A seta que sai de `constraint` é o único ponto em que uma fase inicial usa uma
posterior: rodar um `constraint` exige desugar, checker e evaluator. O ciclo não
existe porque `Lapis.Macros` só declara a interface — quem a implementa é o
orquestrador, a mesma divisão que já valia para o prelude.

| Projeto | Responsabilidade |
|---|---|
| `Lapis.Diagnostics` | `SourceText`, `SourceSpan`, `Diagnostic`, `DiagnosticBag` |
| `Lapis.Ast` | nós Surface e Core, modelo de tipos, printers |
| `Lapis.Lexer` | fonte → tokens |
| `Lapis.Parser` | tokens → Surface AST |
| `Lapis.Macros` | Surface AST → Surface AST: matching, higiene, expansão |
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

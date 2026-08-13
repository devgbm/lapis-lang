# Plano 27 — Roteiro 0.3: da pesquisa de PE ao protótipo de metaprogramação

**Milestone:** M17 (fase A), M18 (B), M19 (C), M20 (D), M21 (E)
**Decisões:** Q33 (novo norte), Q34 (recursão), Q35 (`Str`), Q36 (`s[i] = v`), Q37 (visibilidade), Q38 (macros)
**Depende de:** tudo até o M16

---

## Por que este plano existe

A Q33 mudou o objetivo do projeto: de *"estudar os limites do partial
evaluation"* para *"desenvolver o protótipo de uma linguagem com foco em
metaprogramação"*. O PE continua no roteiro, **no fim**.

O gatilho não foi preferência — foi um spike. A pergunta era "quanto custa
escrever uma biblioteca padrão?", e a resposta veio antes da estimativa:

| Tentativa | Resultado |
|---|---|
| `s.length` sobre `Str` | `LAP0250` — `Str` não tem campo nenhum |
| `s[0]` | `LAP0242` — `Str` não é indexável; não existe `Char` |
| `a[0] = 10` | não parseia |
| `.[1,2] + .[3]` | `LAP0280` — spans não concatenam |
| `def f = ... f(n) ...` | `LAP0201` — sem recursão (Q8) |
| `t.xs = .[4,5,6]` | ✅ funciona |
| retornar um `var` de uma função | ✅ funciona |
| `.[T; semente; n]` genérico | ✅ funciona, **se o chamador der a semente** |

> **O achado.** Não existe hoje nenhuma expressão que produza um span de
> elementos **computados e distintos**. As duas construções são a lista literal
> e a repetição de um mesmo valor. Sem escrita em elemento e sem concatenação, o
> span é selado no nascimento — e `map`, `filter`, `split`, `sort` e
> `Array.push` **não são escrevíveis em LapisLang**.

Isso reordena o pedido. "Escrever a stdlib" não é um milestone que se agenda: é
o **teste de aceitação** de três milestones de linguagem que vêm antes. Que é
exatamente a validação que se queria — ela só começa mais cedo do que parecia.

---

## As fases

```
A · Destravar a linguagem      pré-requisito de tudo
    A1  recursão                                        Q34
    A2  Str: length, indexação, Char                    Q35
    A3  escrita em elemento de span                     Q36

B · Módulos                    chave do multiarquivo
    B1  import, escopo por unidade, tudo público        Q37
    B2  .lp sobre a Core impressa + manifesto
    B3  (exportar macros — adiado até Q38)

C · Efeitos e mundo externo
    C1  Console — inclui entrada, que a 0.2 não tem
    C2  File
    C3  bindings C (quarentena + camada de tipos C)

D · Biblioteca padrão          o teste de aceitação de A–C
    Str, Array<T>, File, Console

E · Metaprogramação++          o novo norte                Q38
    reflection sobre AST; macros com splice, aninhamento e controle

F · Partial evaluator          M22–M24, adiado — não cancelado
```

**A ordem não é negociável nos dois primeiros degraus.** D depende de A, B e C;
C3 depende de B (é preciso um lugar para declarar bindings); e A é o único que
não depende de ninguém. E é onde o spike mostrou que a linguagem está quebrada
para o uso pretendido.

Duas consequências que valem ser ditas em voz alta:

- **C1 melhora a história do PE de graça.** Hoje "um programa fechado tende ao
  resultado já avaliado, porque a 0.2 não tem entrada externa". Com `stdin`,
  programas param de ser fechados por natureza — o exercício da fase F fica mais
  real, não menos.
- **A1 encarece a fase F.** Recursão torna a terminação do PE não-trivial. É o
  que a própria Q8 antecipou, e a troca foi aceita conscientemente (Q34).

---

## Fase A — destravar a linguagem

### A1 · Recursão (Q34)

```c
def foo = fn(value: Int) Void { ... foo(2) ... };
```

**Viabilidade: a linguagem já pagou quase tudo.** Três peças, e só uma é
trabalho de verdade.

**① Tipar `foo` antes de checar o corpo — já dá, sem inferência.**

O corpo de `foo` cita `foo`, então o checker precisa do tipo dele *antes* de
entrar. Em geral isso pede inferência — que a Q7 proibiu de propósito. Aqui não
pede: parâmetros são anotados **por obrigação** (spec §26) e o retorno omitido é
`Void`. A assinatura é derivável da sintaxe, sem olhar o corpo.

O que muda: em `CheckLet`, quando o valor é uma `CoreLambda`, declarar o nome no
escopo **antes** de checar o valor, com o tipo lido da assinatura. Hoje o nome
só entra no escopo do corpo (`inner`), que é o que faz `LAP0201` disparar.

> Restrição que cai junto: só vale quando o valor é sintaticamente uma lambda.
> `def x = x + 1;` continua `LAP0201`, e deve continuar — não há assinatura de
> onde tirar tipo, e a Q8 estava certa sobre esse caso.

**② Orçamento de profundidade — já existe e está isento.**

`MaxCallDepth = 10_000` e `LAP0302` estão implementados desde o M1, hoje
inalcançáveis. `DiagnosticCoverageTests.Exempt` registra literalmente
*"inalcançável sem recursão (spec §8)"*. **Essa isenção sai**, e o caso de
conformidade que a substitui é uma recursão infinita — o primeiro programa que
consegue alcançá-la.

**③ O ambiente cíclico da closure — o trabalho real.**

`ClosureValue(Lambda, Captured, Signature)` captura o ambiente **por valor**, e
`Let(x, v, body)` não expõe `x` em `v`. Para `foo` se encontrar, o ambiente
capturado precisa conter a própria closure. Duas saídas conhecidas:

| Saída | Como | Custo |
|---|---|---|
| **atar o nó** (*knot-tying*) | criar a closure, depois preencher a célula do ambiente que aponta para ela | uma célula mutável no `Environment`, que hoje é imutável |
| **resolver no ponto da chamada** | closure guarda o ambiente **definidor**; o nome é procurado nele quando a chamada acontece | nenhuma mutação; muda a ordem de resolução |

A segunda é mais alinhada ao projeto — `Environment` imutável é premissa do PE
("a closure segue sendo (código, ambiente imutável)") e mexer nisso mexeria na
fase F. Decidir na implementação, com testes que distingam as duas.

**O que precisa ser revisto além disso:**

- `FreeVariables` — `foo` aparece livre no próprio corpo. Sem tratar, o `Let`
  vira código morto ou, pior, o PE elimina a definição que a chamada usa.
- `ReturnAnalysis` — sem mudança esperada: a chamada recursiva é `CoreCall`, já
  coberta.
- **PE (fase F):** especializar chamada recursiva sem terminação é *hang*, não
  erro. Enquanto F não chega, o especializador precisa de uma guarda explícita
  que trate função recursiva como opaca. **Não pode ficar para depois** — o PE
  roda hoje sobre o corpus inteiro a cada suíte.

**Fora de escopo:** recursão **mútua**. Exige olhar declarações adiante, o que a
Q8 nunca precisou responder. Fica em aberto.

---

### A2 · `Str`, `Char` e indexação (Q35)

**A proposta:** `Char` como tipo novo e `Str` como caso especial de span
(`[Char;N]`), ganhando `length` e indexação pelas regras que os spans já têm.

**É elegante, e a análise está na Q35.** O resumo do que impede fechar agora:

1. **O que é um `Char`?** Ponto de código, unidade UTF-16 ou grafema? São três
   linguagens diferentes, e a escolha decide a representação em runtime, o custo
   de indexar e o marshalling do FFI na fase C3.
2. **Custo de representação.** `StrValue(string)` viraria um `Value` por
   caractere.
3. **Alcance.** `Str` está no prelude, na captura de macro, em `contextGet` e em
   reflection. `[Str;?]` viraria `[[Char;?];?]`.
4. **Concatenação.** Se `Str` é span, `+` precisa valer para spans — hoje é
   `LAP0280`.

**Recomendação: fatiar em duas etapas.**

- **A2a — destravar sem unificar.** Expor `length` e indexação de `Str` como
  **intrínsecos**, do mesmo jeito que `.length` de span já é
  (`SpanLengthResolution`), mantendo `StrValue` como representação. Introduz
  `Char` como tipo primitivo, sem prometer que `Str` *é* `[Char;N]`. Destrava a
  stdlib de strings com uma fração do custo.
- **A2b — unificar, se ainda valer a pena.** Depois de A2a e da stdlib, decidir
  se `Str = [Char;N]` compra o suficiente para pagar (1)–(4).

Fatiar assim tem uma vantagem específica: **A2a não fecha nenhuma porta**. Se a
unificação vier depois, `length` e indexação já têm a semântica certa e só mudam
de implementação.

---

### A3 · Escrita em elemento de span (Q36)

A semântica decidida está na Q36. O que este plano acrescenta é o **como** e o
que ela destrava.

**O mecanismo já existe.** `u.a.b = 1` funciona: `CoreAssign` carrega um
caminho, hoje só de nomes de campo. O trabalho é estender o caminho a segmentos
de **índice** — parser, desugar, checker e evaluator —, não inventar mecanismo
novo. E como span é valor, `s[i] = v` é atualização funcional do span inteiro
religada ao `var`: **nenhum aliasing novo**, premissa da Q25 e da fase F intacta.

**O que isso destrava, escrito na linguagem:**

```c
def map = fn(xs: [Int;?], f: fn(Int) Int) [Int;?] {
    var out = .[Int; 0; xs.length];
    var i = 0;

    loop {
        if i >= xs.length { break; }

        if xs[i] is Some(v) { out[i] = f(v); }

        i = i + 1;
    }

    return out;
};
```

Cada peça disso já foi verificada isolada: `var` retornável ✅, `.[T; init; n]`
com tamanho dinâmico ✅, `is` para desembrulhar ✅ (M16). Falta só `out[i] = ...`.

**Um limite que só aparece ao generalizar.** `.[T; semente; n]` exige uma
semente do tipo `T`, e um `Array<T>` genérico não tem uma. O spike confirmou que
funciona **se o chamador fornecer**:

```c
def novo = fn<T>(semente: T, n: Int) [T;?] { ... };
```

Ou seja: `Array.new<T>` vai pedir uma semente, ou os slots serão `Option<T>`. É
decisão de API da fase D, registrada nas questões em aberto.

**A assimetria que fica em aberto.** Ler fora dos limites devolve `Option` — a
falha aparece no tipo, como a §30 exige. Escrever fora dos limites não devolve
nada — a falha não aparece em lugar nenhum. Ver Q36 para as saídas consideradas.
A recomendação deste plano é **warning quando o índice é constante e o tamanho é
`?`**: pega o engano comum sem custar tipo novo, e não mexe na gramática.

---

## Fase B — módulos

### B1 · Escopo por unidade, tudo público (Q37)

`import`, um escopo por unidade, sem `pub`/`private`. O custo de adiar
visibilidade está na Q37; a mitigação é gravar o campo no `.lp` desde a v1,
sempre `public`.

### B2 · `.lp` sobre a Core impressa

**O projeto já pagou o formato de serialização sem perceber.** A propriedade
`desugar(parse(print(core))) ≡ core` é verificada **sobre o corpus inteiro, a
cada rodada da suíte** (`SemanticPropertyTests.Printer_Roundtrips`). Um `.lp`
v1 pode ser Core impressa + manifesto de exportações: formato que já existe, já
tem printer, já tem parser e já tem teste.

Não é atalho preguiçoso — é a única forma de serialização do projeto que vem com
prova de corretude embutida.

### B3 · Exportar macros — adiado

Macros são Surface, e `SurfaceSExprPrinter` é printer de **debug**, sem
round-trip. Exportá-las exigiria tornar a Surface round-trippable ou guardar o
fonte da macro no pacote. **Adiado por decisão do autor** (Q38): macro precisa
amadurecer antes de virar interface pública.

---

## Fases C–F — esboço

**C · Efeitos e mundo externo.** `Console` (com **entrada**, que a 0.2 não tem),
`File`, e bindings C. Sobre C3, dois pontos que precisam estar no plano próprio:

- `printf` é **variádica** — fora do escopo inicial por decisão do autor. Alvos
  v1 melhores: `puts`, `strlen`.
- **FFI quebra a invariante mais forte do projeto.**
  `SemanticPropertyTests.WellTyped_ProgramsDoNotThrow` afirma que exceção é bug
  da implementação, nunca erro do programa. Uma chamada C pode *segfaultar o
  processo* — não é exceção que se capture. FFI precisa de quarentena explícita:
  declarações marcadas e uma categoria de conformidade isenta daquela
  propriedade.

**D · Biblioteca padrão.** `Str`, `Array<T>`, `File`, `Console` — escritos em
LapisLang sempre que possível, pelo princípio §58.2.

**E · Metaprogramação++.** Reflection sobre AST (hoje só existe sobre tipos) e a
evolução de macros da Q38.

**F · Partial evaluator.** M17–M19, com o custo extra que a Q34 introduziu.

---

## Critérios de conclusão da fase A

- [ ] `def foo = fn(n: Int) Int { ... foo(n - 1) ... };` compila e roda.
- [ ] Recursão infinita aborta com `LAP0302`; a isenção em `DiagnosticCoverageTests` sai.
- [ ] O PE não trava em função recursiva — guarda explícita, com caso de teste.
- [ ] `Str` com `length` e indexação; `Char` como tipo.
- [ ] `s[i] = v` sobre `var` de span; `def` continua recusando.
- [ ] Fora dos limites com tamanho conhecido: erro de compilação. Com `[T;?]`: sem efeito.
- [ ] `map` acima escrito **em LapisLang**, no corpus de conformidade.
- [ ] Round-trip e equivalência do PE inalterados sobre o corpus.

---

## Perguntas que este plano deixa em aberto

1. **`Char` é ponto de código, unidade UTF-16 ou grafema?** Trava A2b e o
   marshalling de C3.
2. **A assimetria da Q36** — silêncio, warning ou forma-expressão?
3. **Recursão mútua** entra junto com A1 ou depois?
4. **`Array.new<T>`** pede semente ou usa slots `Option<T>`?

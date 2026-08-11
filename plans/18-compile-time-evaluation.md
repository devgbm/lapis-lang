# Plano 18 — `constraint`, `throw` e contexto de compilação

**Projeto:** `Lapis.Macros`, `Lapis.Runtime`, `Lapis.Cli`
**Milestone:** M9
**Spec:** [`lapislang-macros-0.1.md` §8](../spec/lapislang-macros-0.1.md)
**Depende de:** 17 (macro engine), 12 (PE núcleo — o "PE básico"), 08, 06

---

## Objetivo

Executar código LapisLang **durante a compilação**, entre o `match` e o `expand`,
para validar a construção e acumular estado.

## Escopo

**Entra:** `constraint`, `throw`, contexto de compilação e suas nativas, o
orquestrador que roda tudo isso.

**Fica de fora:** reflection dentro de `constraint` — plano 19, que depende deste.

---

## O que será construído

### 18.1 A decisão que organiza o plano — Q21 ✅

> **Uma linguagem, um evaluator, dois ambientes.**

Confirmado pelo autor.

`constraint` roda na própria LapisLang, avaliada pelo **evaluator do plano 08**.
Não há uma segunda linguagem de compile time nem uma segunda semântica.

Isto vem direto do princípio §58.3 — "o evaluator é a implementação de referência
da semântica" — e de uma consequência prática: uma segunda linguagem precisaria de
segundo lexer, parser, checker e evaluator, todos com o mesmo dever de estar
corretos, e a divergência entre as duas seria uma fonte de bugs sem fim.

O que muda entre as fases é só o **ambiente**:

| | Runtime | Compile time |
|---|---|---|
| Evaluator | o mesmo | o mesmo |
| Nativas | `print` | `print`, `throw`, contexto, `reflect` |
| Escopo | o programa | bindings do `constraint` + contexto |

### 18.2 Consequência no grafo de dependências

`Lapis.Macros` passa a precisar do checker e do evaluator para rodar um
`constraint`. Isso **fecharia um ciclo** se fosse feito ingenuamente:

```text
Lapis.Macros → Lapis.TypeChecker → ... → Lapis.Macros ?
```

A saída é a mesma do plano 09, que já resolveu isto para o prelude
(`PreludeScope` é dado no Runtime; `PreludeLoader` é carga no Cli):

```csharp
// Lapis.Macros — só a interface
public interface IConstraintRunner
{
    ConstraintOutcome Run(BlockExpression constraint, MatchResult bindings, CompileContext context);
}
```

`Lapis.Macros` declara a interface e recebe uma implementação. Quem a implementa é
`Lapis.Cli`, o orquestrador, que já conhece todas as fases. **`ArchitectureTests`
continua verde e o ciclo não existe.**

### 18.3 `CompileContext`

```csharp
// Lapis.Runtime — dado, não comportamento (mesma divisão do PreludeScope)
public sealed class CompileContext
{
    public bool Has(string key);
    public string? Get(string key);
    public void Put(string key, string value);
    public IReadOnlyList<string> Keys(string prefix);
    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
```

Nativas expostas só no ambiente de compile time:

```text
contextHas(key: Str) Bool
contextGet(key: Str) Result<Str, ContextError>
contextPut(key: Str, value: Str) Void
contextKeys(prefix: Str) Str[]
```

`contextGet` devolve `Result` — chave ausente é falha esperada, e a spec §30 é
categórica: falha esperada aparece no tipo, não em exceção. `ContextError` é um
enum do prelude, como `IndexError`.

**Chaves e valores são `Str`.** Não é preguiça: os casos reais (registrar,
deduplicar, contar) cabem em `Str`, e um contexto tipado exigiria serialização,
que é um subsistema inteiro. A restrição é revisitável sem quebrar nada.

**O contexto é a única coisa mutável do sistema**, e só durante a compilação. Não é
um recurso da linguagem — é estado do compilador exposto por nativas, do mesmo modo
que `print` expõe I/O. A regra "bindings são imutáveis" (§8) segue intacta.

### 18.4 `throw`

```csharp
sealed record ThrowExpression(Expression Value) : Expression;
sealed class CoreThrow(int nodeId, SourceSpan span, CoreExpr value) : CoreExpr;
```

| Aspecto | Decisão |
|---|---|
| Tipo | `Never` — cabe em qualquer posição, como `return` (Q13) |
| Valor | tem de ser `Str` (`LAP0508`) |
| Onde vale | só dentro de `constraint`; fora é `LAP0507` |
| Execução | `Completion.Abort`, que já existe |

`throw` **não** introduz exceções de runtime. A 0.2 não as tem, e Q9 tornou a
divisão total justamente para eliminar caminhos de aborto; reintroduzi-los pela
porta dos fundos seria um retrocesso. Exceções de runtime, se um dia existirem,
serão uma feature independente com sua própria discussão.

O evaluator já sabe abortar (`Completion.Abort` existe desde o M1, usado por
`LAP0302`). `throw` reaproveita esse caminho inteiro.

### 18.5 O orquestrador

Um `constraint` é código LapisLang, então precisa do pipeline completo — mas de um
pipeline **reduzido**, sem macros (um `constraint` não invoca macros):

```text
BlockExpression do constraint
    → Desugar
    → TypeChecker  (escopo: bindings do match + prelude + nativas de compile time)
    → Evaluator    (ambiente de compile time, com o CompileContext)
    → ConstraintOutcome
```

`ConstraintOutcome` é `Ok` ou `Rejected(mensagem, span)`.

**Os bindings do `match` entram no escopo como valores de reflection**, não como
árvores executáveis — um `constraint` não executa o código do usuário. `Str:path`
capturado vira o `Str` literal; `Expression:e` vira um valor de metadados
(plano 19). Enquanto o plano 19 não existir, só capturas de literal são visíveis, o
que já cobre `@post`.

### 18.6 Diagnósticos

```text
error LAP0503: rota POST já registrada: /products
  app.ls:24:1
  @post "/products" {
  ^^^^^^^^^^^^^^^^^
      = nota: registro anterior em app.ls:12:1
```

O span é o da **invocação**. A nota de "registro anterior" sai do próprio contexto,
se a macro tiver guardado o span lá — daí `contextPut` aceitar um valor arbitrário.

---

## Decisões de design

### Por que não uma linguagem de macro separada

Rust tem `macro_rules!` (declarativa) e macros procedurais (Rust de verdade). Ter
as duas é caro. Aqui a escolha é uma só: `constraint` é LapisLang. O custo é que a
linguagem precisa ser expressiva o bastante para validações — e é, desde o M4:
tem `Str`, arrays, `match`, `Result` e funções.

O benefício direto para a pesquisa: o **partial evaluator também roda em
`constraint`**, porque é o mesmo Core. Uma constraint cara pode ser especializada
com a mesma máquina que especializa o programa.

**E há uma dependência na direção contrária, que decide o cronograma.** Uma
`constraint` recebe capturas sintáticas e precisa reduzi-las a valores para decidir
— dobrar `"route." + path`, avaliar `arrayLength(faltando) > 0`. Isso é exatamente
constant folding e propagação, ou seja, o **núcleo do plano 12**.

Por isso o M7 (PE núcleo) vem antes do M8 (macro engine) mesmo com a decisão do
autor de tratar macros antes do partial evaluator: o que se antecipa é a *fatia
mínima* do plano 12 — folding e propagação —, não o PE inteiro. Especialização
(13) e análise (14) continuam depois das macros.

### Por que `Str` no contexto

Ver §18.3. É restrição consciente, documentada e reversível.

### Por que `throw` e não `Result`

`constraint` poderia devolver `Result<Void, Str>`. Seria mais idiomático — e pior
de escrever: toda validação viraria uma cadeia de `match`, e a mensagem de erro
perderia o span de onde a rejeição nasceu. `throw` como `Never` corta o fluxo no
ponto exato, e é isso que o diagnóstico precisa apontar.

---

## Testes necessários

### `constraint`

| Teste | Fonte | Esperado |
|---|---|---|
| `Constraint_Passing_Expands` | constraint que não rejeita | expande normalmente |
| `Constraint_Throwing_FailsCompilation` | `throw "não"` | `LAP0503`, span da invocação |
| `Constraint_DoesNotTryNextRule` | 1ª regra casa e rejeita | não tenta a 2ª |
| `Constraint_RunsAfterMatch` | ordem observável | bindings visíveis na constraint |
| `Constraint_RunsBeforeExpand` | rejeição impede expansão | zero nós expandidos |
| `Constraint_SeesLiteralCaptures` | `Str:path` | valor literal disponível |

### `throw`

| Teste | Fonte | Esperado |
|---|---|---|
| `Throw_IsNever` | `throw "x"` em posição de valor | tipa |
| `Throw_OutsideConstraint` | `throw` no programa | `LAP0507` |
| `Throw_NonString` | `throw 1` | `LAP0508` |
| `Throw_MessageReachesDiagnostic` | mensagem literal | aparece no diagnóstico |

### Contexto

| Teste | Asserção |
|---|---|
| `Context_PutThenHas` | `contextPut` então `contextHas` ⇒ true |
| `Context_GetMissing_IsErr` | `contextGet` ausente ⇒ `Result.Err` |
| `Context_Keys_FiltersByPrefix` | só as chaves com o prefixo |
| `Context_IsolatedPerCompilation` | duas compilações não se enxergam |
| `Context_DoesNotLeakToRuntime` | nativas de contexto ausentes do runtime |
| `Post_DuplicateRoute_IsRejected` | o exemplo canônico do `@post` |

### Isolamento entre fases

| Teste | Asserção |
|---|---|
| `CompileTime_CannotCallProgramFunctions` | função do programa não visível na constraint |
| `Runtime_CannotThrow` | `throw` ausente do ambiente de runtime |
| `Constraint_HasNoSideEffectOnProgram` | o programa expandido não vê o contexto |

O último é o teste que garante a separação de §21 da proposta.

---

## Critérios de conclusão

- [x] `@post` com deduplicação de rota funcionando ponta a ponta.
- [x] `throw` rejeitado fora de `constraint`; contexto ausente do runtime.
- [x] `ArchitectureTests` verde: sem ciclo `Macros → TypeChecker → Macros`.
- [x] Diagnóstico de constraint com span da invocação e nota de origem.
- [x] O evaluator do plano 08 usado **sem fork** — nenhuma segunda semântica.

---

## O que a implementação mudou no plano

### 1. `IConstraintRunner` não recebe o `CompileContext`

O plano previa `Run(constraint, bindings, CompileContext)`. O contexto saiu da
assinatura: quem tem uma compilação inteira em mãos é a **implementação**, e
passá-lo a cada chamada obrigaria `Lapis.Macros` a referenciar `Lapis.Runtime` só
para repassar um objeto que nunca lê. A implementação cria um contexto por
instância, e uma instância por compilação — o isolamento entre compilações sai de
graça disso.

### 2. `ConstraintOutcome` tem três resultados, não dois

`Ok` e `Rejected` não cobrem o caso em que a **própria constraint** não compila.
Sem um terceiro estado, um erro de tipo dentro da macro viraria "esta construção é
inválida" — a leitura errada, e com a mensagem errada. `Failed` diz "os
diagnósticos já foram reportados, fique calado", e é o que faz
`LAP0503` significar sempre uma rejeição deliberada.

### 3. O ambiente de compile time virou um tipo

A tabela do §18.1 ("uma linguagem, um evaluator, dois ambientes") virou
`CompileTimeScope`, passado ao checker e ao evaluator. Não-nulo é compile time:
as nativas de contexto entram no escopo e `throw` é permitido. Nulo é runtime, e
nenhuma das duas coisas existe. A alternativa — dois parâmetros soltos, um de
bindings e um booleano — deixaria construir o estado impossível de um ambiente que
permite `throw` mas não tem contexto.

### 4. `throw` é reservada de verdade, e o `LAP0507` é do checker

Reservar `throw` sempre, e não só dentro de `constraint`, é o que permite
responder "'throw' só é válido dentro de 'constraint'" em vez de deixar a linha
virar uma expressão malformada. Onde ela vale é pergunta de tipo, não de léxico.

### 5. O que uma `constraint` imprime **não** é saída do programa

Não estava no plano, e apareceu na primeira execução da suíte: um `print` dentro
de `constraint` ia parar na mesma saída do programa, e com isso a propriedade de
equivalência do partial evaluator quebrava — o programa residual não tem macros,
logo não reimprime nada.

A correção é a leitura certa do que aquilo é: **saída do compilador**. O CLI a
manda para `stderr`, junto dos diagnósticos, e a suíte de conformidade continua
afirmando só o que o programa imprime. `Pipeline.Compile` ganhou um
`compileTimeOutput` separado.

### 6. `ArchitectureTests` **não** foi quebrado

O plano 17 dizia que "o plano 18 quebra isso de propósito". Não quebrou: com a
interface declarada em `Lapis.Macros` e implementada no orquestrador,
`Lapis.Macros` continua sem enxergar checker, evaluator nem runtime. O teste ficou
como estava, e ganhou um irmão que trava o contrato de cada lado da fronteira.

---

## O que ficou de fora

**Capturas não-literais na `constraint`.** Um `Expression:e` capturou uma
**árvore**, e lê-la como valor é reflection — plano 19. Até lá o nome
simplesmente não está no escopo da constraint, e quem o usar recebe o `LAP0201`
normal. `@post`, que é o caso canônico, só precisa de `Str:path`.

**Nota de "registro anterior" automática.** O §18.6 mostra o diagnóstico com uma
nota apontando o registro anterior. A nota existe, mas aponta o ponto da
**constraint** onde a rejeição nasceu; apontar o *span do registro anterior*
depende de guardá-lo no contexto, e o contexto guarda `Str`. Uma macro que queira
isso pode compor a informação na mensagem do `throw` — que é exatamente o que o
§18.6 diz ao justificar `contextPut` aceitar valor arbitrário.

**Especializar a `constraint` com o partial evaluator.** O §"Por que não uma
linguagem de macro separada" observa que o PE roda em `constraint` porque é o mesmo
Core. Roda — `CoreThrow` está no especializador, e é tratado como fluxo de
controle, nunca dobrado. Mas o `ConstraintRunner` não chama o PE: uma constraint é
executada uma vez por invocação, e especializá-la antes custaria mais do que
executá-la.

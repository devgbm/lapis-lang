# 09 — Prelude: `Result`, `IndexError`, `print`

**Milestone:** M2 · **Depende de:** 06, 07, 08 · **Projeto:** `Lapis.Runtime` + `prelude.ls`

Cobre a spec §16, §17, §29, §49 e o princípio §58.2 ("runtime mínimo").

---

## Objetivo

Fornecer as definições que a linguagem precisa para ser útil, **escritas na
própria linguagem** sempre que possível. `Result` não é um conceito do runtime;
é um `enum` do prelude (spec §16: "o runtime não deve possuir uma implementação
semântica especial de `Result`").

---

## Escopo

**Entra:** o arquivo `prelude.ls`, o mecanismo de carga, o `PreludeScope` que
liga nomes do prelude ao checker e ao evaluator, e as poucas primitivas nativas.

**Fica de fora:** biblioteca padrão. A spec §1 exclui explicitamente "biblioteca
padrão extensa". O prelude tem o mínimo e cresce só com justificativa.

---

## O que será construído

### 9.1 `prelude.ls`

Arquivo embutido como recurso em `Lapis.Runtime` (`EmbeddedResource`), versionado
junto com o compilador:

```c
def Result = enum<T, E> {
    Ok(T),
    Err(E)
};

def IndexError = enum {
    OutOfBounds
};

def Option = enum<T> {
    Some(T),
    None
};
```

`Option` entra porque a spec §29 o cita ("a semântica de `Result`, `Option`, etc.
deve permanecer na linguagem") e porque não custa nada — é só um enum.

O M9 acrescentou `ContextError`, pelo mesmo motivo que `IndexError` já estava
aqui: `contextGet` devolve `Result<Str, ContextError>`, e o tipo do erro precisa
existir na linguagem antes de a primitiva poder tê-lo na assinatura (plano 18
§18.3).

```c
def ContextError = enum {
    Missing
};
```

O M10 acrescentou os tipos de reflection, pelo mesmo princípio §58.2 que pôs
`Result` aqui: não existe sistema de metadados paralelo, e um `TypeInfo` é um
struct comum — imutável por construção, sem precisar de regra própria.

```c
def TypeKind = enum { Struct, Enum };
def FieldInfo = type { name: Str; typeName: Str; };
def VariantInfo = type { name: Str; arity: Int; payloadTypeNames: Str[]; };
def TypeInfo = type { name: Str; kind: TypeKind; typeParameterNames: Str[];
                      fields: FieldInfo[]; variants: VariantInfo[]; };
```

Funções auxiliares (`unwrapOr`, `map`) **não** entram no prelude na 0.2: cada uma
precisaria de generics já estáveis e ampliaria a superfície de teste sem servir a
nenhum objetivo da spec. Ficam para v0.3.

### 9.1b Macros do prelude

O M11 acrescentou `@unless` e `@while` (plano 20). Elas **não** são
`PreludeBinding`: uma macro não tem tipo nem valor, e não é first-class citizen
(Q19). Ficam em `PreludeScope.Macros`, e a carga as separa do arquivo antes do
desugar — macro é sintaxe, não código, e o resto do pipeline não a conhece.

Redeclarar uma delas no arquivo do usuário **sombreia**, sem diagnóstico, do mesmo
jeito que `def Result = ...` sombreia o `Result` do prelude.

### 9.2 Nativos

Os únicos nomes que **não** dá para escrever em LapisLang, registrados
diretamente no escopo raiz:

| Nome | Tipo | Motivo de ser nativo |
|---|---|---|
| `print` | `fn(Any) Void` | efeito de I/O. Não é genérico: com Q7 exigindo argumentos explícitos, `fn<T>(T) Void` obrigaria `print<Int>(x)` em todo programa. `Any` é um tipo top interno que nenhuma sintaxe produz |
| `array_length` | `fn(Any) Int` | acesso à representação (spec §29); mesma razão de `print` para não ser genérico |

Regra de admissão de novos nativos, para manter §58.2: um nome só pode ser nativo
se for **impossível** defini-lo em `prelude.ls`. Cada nativo novo precisa dessa
justificativa escrita neste plano.

**As primitivas de contexto do M9 não estão nesta tabela** e não devem estar.
`contextHas`, `contextGet`, `contextPut` e `contextKeys` vivem em
`CompileTimeNatives`, e entram no escopo só quando o que se avalia é um
`constraint` (plano 18 §18.3). Esta tabela é o escopo raiz de **todo** programa;
uma nativa de compile time aqui tornaria o estado do compilador alcançável em
runtime, que é justamente a separação que o plano 18 existe para manter.

### 9.3 Carga do prelude

⚠️ **Correção de arquitetura (descoberta ao executar o plano 01).** A versão
original deste plano colocava `PreludeScope.Load()` em `Lapis.Runtime`. Isso
fecharia o ciclo `Runtime → Evaluator → TypeChecker → Runtime`, porque carregar o
prelude exige rodar o pipeline inteiro. Separação adotada:

| Onde | O quê |
|---|---|
| `Lapis.Runtime` | o **dado** `PreludeScope` + o recurso embutido `prelude.ls` |
| `Lapis.Cli` (orquestrador) | a **carga**: rodar lexer → parser → desugar → checker → evaluator sobre `prelude.ls` |

`PreludeScope` expõe os bindings como dados neutros (nome + `LapisType` +
`Value`); o `TypeChecker` monta seu próprio `Scope` a partir deles, e o
`Evaluator` monta seu `Environment`. Assim nem `Runtime` conhece `Scope` (tipo do
checker), nem o checker conhece o carregador.

```csharp
// Lapis.Runtime — apenas dado
public sealed class PreludeScope
{
    public ImmutableArray<PreludeBinding> Bindings { get; }

    // atalhos resolvidos, usados pelo evaluator ao construir Result
    public TypeDefinition Result { get; }
    public TypeDefinition IndexError { get; }
    public int OkVariantIndex { get; }
    public int ErrVariantIndex { get; }
    public int OutOfBoundsVariantIndex { get; }

    public Value MakeOk(Value payload, LapisType okType, LapisType errType);
    public Value MakeErr(Value payload, LapisType okType, LapisType errType);
    public Value MakeOutOfBounds();
}

public sealed record PreludeBinding(string Name, LapisType Type, Value Value);

// Lapis.Cli — carga
public static class PreludeLoader
{
    public static PreludeScope Load();          // com cache
    public static PreludeScope LoadFresh();     // para testes isolados
}
```

O prelude passa **pelo mesmo pipeline** do programa do usuário: lexer → parser →
desugar → checker → evaluator. Nada de construção manual de AST. Isso garante que
o prelude é um programa LapisLang legítimo, e qualquer regressão no pipeline
quebra o prelude imediatamente — um teste de fumaça permanente e barato.

Se `prelude.ls` falhar em qualquer fase, é `InternalCompilerException`: um
prelude inválido é bug do compilador, nunca do usuário.

**Cache:** `PreludeScope.Load()` é caro relativo a um programa pequeno; o
resultado é imutável e fica em cache estático (`Lazy<PreludeScope>`). Testes que
precisam de isolamento usam `PreludeScope.LoadFresh()`.

### 9.4 Escopo do usuário

O programa do usuário é checado e avaliado num escopo **filho** do prelude.
Consequências, todas testadas:

- o usuário pode sombrear `Result` (`def Result = enum { A }`), e seu `Result`
  passa a valer no código dele;
- indexação continua produzindo o `Result` **do prelude**, não o do usuário — o
  evaluator usa a `TypeDefinition` resolvida em `PreludeScope`, não uma busca por
  nome no escopo corrente. Isso evita que sombrear um nome mude a semântica de
  `[]`, o que seria uma armadilha.

### 9.5 Tipo de `Index` no checker

```text
typeof(Index(a: T[], i: Int)) = NamedType(prelude.Result, [TypeArg(T), TypeArg(NamedType(prelude.IndexError, []))])
```

Literalmente a regra da spec §21.

---

## Decisões de design

**Prelude em `.ls`, não em C#.** Custa uma passada extra do pipeline na
inicialização e ganha: (a) fidelidade ao §58.2; (b) um teste de integração
permanente do pipeline; (c) mudanças no prelude não exigem recompilar o
compilador conceitualmente — só editar um arquivo.

**Ligação por `TypeDefinition` resolvida, não por nome.** É o que torna o
sombreamento seguro e o que mantém o evaluator sem strings mágicas.

**`print` aceita um argumento, não vários.** A spec §34 nunca usa mais de um, e
variadicidade não está na spec.

**`print` não é genérico** (Q7). A alternativa — `fn<T>(T) Void` com argumentos
explícitos obrigatórios — forçaria `print<Int>(x)` em todo programa. O tipo `Any`
é o preço, e ele é contido: interno, sem sintaxe que o produza, e usado só por
primitivas.

---

## Testes necessários

### Carga e validade

| Teste | Asserção |
|---|---|
| `Prelude_Parses` | `prelude.ls` sem erros de parse |
| `Prelude_TypeChecks` | sem diagnósticos |
| `Prelude_Evaluates` | produz um `Environment` com `Result`, `IndexError`, `Option` |
| `Prelude_DefinesExpectedNames` | lista exata de nomes exportados (teste de regressão contra crescimento acidental) |
| `Prelude_Result_HasOkAndErr` | índices de variante 0 e 1 |
| `Prelude_IndexError_HasOutOfBounds` | índice 0 |
| `Prelude_Load_IsCached` | duas chamadas ⇒ mesma instância |
| `Prelude_LoadFresh_IsIndependent` | instâncias distintas |
| `Runtime_DoesNotReferenceTypeChecker` | teste de arquitetura: a separação de §9.3 se mantém |
| `Prelude_MalformedSource_ThrowsInternal` | injetar prelude inválido ⇒ `InternalCompilerException` |

### Integração com o checker

| Teste | Asserção |
|---|---|
| `Index_Type_UsesPreludeResult` | `[1][0] : Result<Int, IndexError>` |
| `User_CanAnnotateWithResult` | `def r: Result<Int, IndexError> = a[0];` tipa |
| `User_CanMatchOnResult` | `match a[0] { Result.Ok(v)=>v, Result.Err(e)=>0 }` tipa |
| `User_CanUseOption` | `Option.Some(1)` tipa |
| `Result_IsGeneric` | `Result<Str, Int>` tipa |

### Integração com o evaluator

| Teste | Asserção |
|---|---|
| `Index_ProducesPreludeOk` | `EnumValue.Definition` é a **mesma instância** de `prelude.Result` |
| `Index_OutOfBounds_ProducesPreludeErr` | idem, com carga `OutOfBounds` |
| `Result_FormatsAsOk` | `print([1][0])` ⇒ `Result.Ok(1)\n` |
| `Result_FormatsAsErr` | `print([1][5])` ⇒ `Result.Err(IndexError.OutOfBounds)\n` |

### Sombreamento

| Teste | Asserção |
|---|---|
| `User_CanShadowResult` | `def Result = enum { A };` ⇒ sem erro |
| `Shadowed_Result_DoesNotAffectIndexing` | após sombrear, `[1][0]` ainda produz o `Result` do prelude |
| `User_CanShadowPrint` | `def print = fn(x: Int) Void { };` ⇒ chamadas do usuário usam a dele |

### Nativos

| Teste | Asserção |
|---|---|
| `Print_Int`, `Print_Str`, `Print_Bool`, `Print_Void` | formatação da tabela do plano 07 §7.4 |
| `Print_Array`, `Print_Enum`, `Print_Struct`, `Print_Closure` | idem |
| `Print_ReturnsVoid` | `def x: Void = print(1);` tipa |
| `Print_AcceptsAnyType` | `print(1)`, `print("s")`, `print(f)` — sem argumento genérico (Q7) |
| `Print_IsNotGeneric` | a assinatura não tem parâmetros de tipo |
| `ArrayLength` | `array_length([1,2,3])` ⇒ 3 |
| `Natives_ListIsMinimal` | teste de regressão: exatamente 2 nativos |

---

## Critérios de conclusão

- [ ] `prelude.ls` embutido, passando pelo pipeline completo sem diagnósticos.
- [ ] `Result`, `IndexError`, `Option` disponíveis para o usuário.
- [ ] Indexação produzindo os valores do prelude (identidade de `TypeDefinition`
      verificada).
- [ ] Nenhuma string `"Result"` ou `"IndexError"` no código do evaluator.
- [ ] Exatamente 2 nativos, com justificativa escrita.

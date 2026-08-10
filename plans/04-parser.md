# 04 — Parser

**Milestone:** M1 → M3 · **Depende de:** 02, 03 · **Projeto:** `Lapis.Parser`

Corresponde à Etapa 3 da spec (§45). Gramática normativa completa no
[Apêndice A](appendix-a-grammar.md).

---

## Objetivo

`ImmutableArray<Token>` → `SourceFile` (Surface AST), com recuperação de erro e
sem jamais executar código (spec §36).

---

## Escopo faseado

| Fase | Construções | Milestone |
|---|---|---|
| A | literais, identificadores, `def`, blocos, binários/unários, `fn`, chamada, `return`, `if`/`else`, `()` | M1 |
| B | arrays, indexação, acesso a membro | M2 |
| C | `type`, `enum`, `match`, construção `.Nome { }` | M3 |
| D | generics: parâmetros e argumentos, incluindo const generics | M4 ✅ |

Cada fase entrega parser + testes de snapshot antes da próxima começar.

---

## O que será construído

### 4.1 API

```csharp
public sealed class Parser
{
    public static SourceFile Parse(SourceText source, DiagnosticBag diagnostics);
    public static SourceFile Parse(ImmutableArray<Token> tokens, DiagnosticBag diagnostics);
}
```

Parser recursivo descendente para statements e primárias, **precedence climbing**
para binários. Escrito à mão — sem gerador — porque as duas ambiguidades reais
(§4.4) exigem controle explícito de backtracking.

### 4.2 Precedência de operadores

Do menor para o maior:

| Nível | Operadores | Associatividade |
|---|---|---|
| 0 | `return` (prefixo, só em posição de expressão completa) | — |
| 1 | `\|\|` | esquerda |
| 2 | `&&` | esquerda |
| 3 | `==` `!=` | esquerda |
| 4 | `<` `>` `<=` `>=` | esquerda, **não encadeável** |
| 5 | `+` `-` | esquerda |
| 6 | `*` `/` | esquerda |
| 7 | unário `-` `!` | prefixo |
| 8 | pós-fixo: `(...)`, `[...]`, `.nome`, `<...>(...)` | esquerda |

`a < b < c` é rejeitado com `LAP0114` ("comparações não encadeiam") em vez de
parsear como `(a<b)<c` — a segunda comparação teria `Bool` à esquerda e o erro de
tipo resultante seria confuso.

### 4.3 Blocos e a cauda

```text
block := "{" statement* expression? "}"
```

Algoritmo: enquanto não for `}`, parseia um statement; se o que foi parseado é
uma expressão **e** o próximo token é `}`, ela vira `Tail`; se é uma expressão e
o próximo token é `;`, consome o `;` e vira `ExpressionStatement`.

Consequência (spec §9): `{ print("x"); }` tem `Tail == null` ⇒ valor `Void`;
`{ x + y }` tem `Tail != null` ⇒ valor da expressão.

### 4.4 As duas ambiguidades e como resolvê-las

#### (a) `f<Int>(x)` vs. `a < b` — Q5

Ao ver `<` em posição pós-fixa depois de uma expressão, o parser:

1. `mark = tokens.Mark()`;
2. tenta consumir uma lista de argumentos genéricos até `>`, com os diagnósticos
   suprimidos — a tentativa pode falhar de propósito;
3. **aceita** se o parse teve sucesso **e** o token imediatamente após `>` é `(`,
   é `.`, ou não pode iniciar uma expressão. Caso contrário, `tokens.Reset(mark)`
   e `<` volta a ser o operador binário.

O terceiro gatilho não é uma heurística solta: se o token seguinte não inicia
expressão, a leitura relacional ficaria sem operando à direita e não existe. É o
que faz `def t = SomeType<"v", 1>;` (spec §13) parsear.

Consequências documentadas na gramática:

- `identity<Int>(10)` ⇒ chamada genérica.
- `Result<Int, E>.Ok(1)` ⇒ variante de enum genérico.
- `a < b > (c)` ⇒ também vira chamada genérica. É o preço de não ter turbofish;
  o programa continua expressável como `(a < b) > (c)`. Registrado como
  limitação conhecida em [Apêndice C](appendix-c-decisions.md).
- `a < b > c` ⇒ continua relacional: as duas leituras existem e a relacional vence.
- Em **posição de tipo** (`x: Result<Int, IndexError>`) não há ambiguidade
  alguma: a gramática de tipos não tem operador `<`.

O backtracking é limitado — a tentativa consome no máximo os tokens da lista e
nunca reinicia — então o custo é linear no tamanho do arquivo, o que
`Backtracking_IsLinear` trava.

#### (b) ~~`if cond { ... }` vs. construção de struct~~ — resolvida por Q2

**Esta ambiguidade deixou de existir.** A construção de `type` leva um ponto
inicial — `.Point { x: 1 }` — e nenhuma outra expressão começa com `.`. O parser
decide entre bloco e construção olhando um único token, sem flag de contexto e
sem restrição na condição de `if`/`match`. Nada a implementar aqui além de
reconhecer `.` em posição de primária.

#### (c) `fn(Int) Int` (tipo) vs. `fn(a: Int) Int { ... }` (valor)

Ocorre apenas dentro de argumentos genéricos **em posição de expressão** (§13
permite uma função literal como argumento). Discriminador: um **tipo** de função
nunca tem corpo `{`. O parser tenta o tipo; se após o tipo de retorno não vier
`,` nem `>`, faz `Reset` e reparseia como literal de função.

Em posição de **tipo**, `fn` é sempre um tipo de função e não há tentativa
alguma — Q17 no [Apêndice C](appendix-c-decisions.md).

### 4.5 Recuperação de erro

Objetivo: um erro de sintaxe não deve suprimir os erros do resto do arquivo.

- **Statements**: em erro, descarta tokens até `;` (consumindo-o) ou até um token
  de início de statement (`def`, `}`, EOF). Produz um nó `ExpressionStatement`
  com uma expressão de erro para manter a árvore bem-formada.
- **Listas** (argumentos, elementos de array, campos, variantes): em erro dentro
  de um elemento, pula até `,` ou até o fechador da lista.
- **Chaves**: contador de profundidade impede que a recuperação salte para fora do
  bloco corrente.
- `Bad` token vindo do lexer é consumido silenciosamente (o erro já foi
  reportado) — nada de erro duplo.

Garantia testada: `Parse` sempre termina e sempre devolve um `SourceFile`
não-nulo, mesmo com entrada arbitrária.

### 4.6 Limite de profundidade

Expressões profundamente aninhadas (`((((...))))`) causariam stack overflow, que
não é capturável em .NET. O parser mantém um contador de profundidade com limite
de 200 e reporta `LAP0115` ao ultrapassá-lo, abortando aquele sub-parse.

---

## Testes necessários

Predominantemente **snapshots de AST** (spec §45), via `SurfaceSExprPrinter` +
`Verify`. Testes negativos asseveram **código de diagnóstico + span**.

### Fase A — núcleo (M1)

| Teste | Fonte | Esperado |
|---|---|---|
| `Parse_IntLiteral` | `1;` | snapshot |
| `Parse_NegativeLiteral` | `-10;` | `(neg 10)`, não literal negativo |
| `Parse_FloatBoolStrUnit` | `1.5; true; "s"; ();` | snapshot |
| `Parse_Def_Simple` | `def x = 10;` | snapshot |
| `Parse_Def_WithAnnotation` | `def x: Int = 10;` | anotação presente |
| `Parse_Def_MissingSemicolon` | `def x = 10` | `LAP0102` |
| `Parse_Def_MissingEquals` | `def x 10;` | `LAP0103` |
| `Parse_BinaryPrecedence_Mul` | `1 + 2 * 3;` | `(+ 1 (* 2 3))` |
| `Parse_BinaryPrecedence_Cmp` | `1 + 2 < 3;` | `(< (+ 1 2) 3)` |
| `Parse_BinaryAssoc_Left` | `1 - 2 - 3;` | `(- (- 1 2) 3)` |
| `Parse_Parens_OverridePrecedence` | `(1 + 2) * 3;` | `(* (+ 1 2) 3)` |
| `Parse_ChainedComparison_Rejected` | `a < b < c;` | `LAP0114` |
| `Parse_LogicalOps` | `a && b \|\| c;` | `(\|\| (&& a b) c)` |
| `Parse_Not` | `!a;` | `(not a)` |
| `Parse_Block_WithTail` | `{ def x=1; x }` | `Tail != null` |
| `Parse_Block_WithoutTail` | `{ print(1); }` | `Tail == null` |
| `Parse_Block_Empty` | `{}` | sem statements, sem tail |
| `Parse_Fn_NoParams` | `fn() Void { }` | snapshot |
| `Parse_Fn_Params` | spec §34 `add` | snapshot |
| `Parse_Fn_NoReturnType` | `fn(a: Int) { }` | `ReturnType == null` |
| `Parse_Fn_MissingParamType` | `fn(a) Int { }` | `LAP0104` |
| `Parse_Call_NoArgs` | `main();` | snapshot |
| `Parse_Call_Args` | `add(10, 20);` | snapshot |
| `Parse_Call_TrailingComma` | `add(10, 20,);` | aceito |
| `Parse_Call_Chained` | `f()();` | call de call |
| `Parse_Return_WithValue` | `return a + b;` | snapshot |
| `Parse_Return_Empty` | `return;` | `Value == null` |
| `Parse_Return_InsideMatchArm` | `Ok(v) => return v` | aceito |
| `Parse_If_NoElse` | `if x < 0 { return 1; }` | `Else == null` |
| `Parse_If_Else` | + `else { }` | snapshot |
| `Parse_If_ElseIf` | `else if` encadeado | aninhamento correto |
| `Parse_If_NoStructLiteralInCondition` | `if p { }` | `p` é identificador, bloco é o `then` |
| `Parse_HelloExample` | `examples/hello.ls` | snapshot — teste-âncora do M1 |

### Fase B — arrays e acesso (M2)

| Teste | Fonte |
|---|---|
| `Parse_Array_Literal` | `[1, 2, 3];` |
| `Parse_Array_Empty` | `[];` |
| `Parse_Array_Nested` | `[[1],[2]];` |
| `Parse_Array_TrailingComma` | `[1,2,];` |
| `Parse_Array_OfFunctions` | spec §11 |
| `Parse_Index` | `numbers[1];` |
| `Parse_Index_Chained` | `a[0][1];` |
| `Parse_Index_OnCall` | `f()[0];` |
| `Parse_Member` | `IndexError.OutOfBounds;` |
| `Parse_Member_Chained` | `a.b.c;` |
| `Parse_Postfix_Mixed` | `a.b[0](x).c;` — ordem de aninhamento correta |
| `Parse_Type_Array` | `def x: Int[] = [];` |
| `Parse_Type_ArrayOfArray` | `Int[][]` |
| `Parse_Index_Unclosed` | `a[1;` ⇒ `LAP0105` |

### Fase C — type, enum, match (M3)

| Teste | Fonte |
|---|---|
| `Parse_Type_Empty` | `def T = type { };` |
| `Parse_Type_Fields` | spec §14 `User` |
| `Parse_Type_FieldMissingSemicolon` | `LAP0106` |
| `Parse_Enum_Simple` | spec §15 `Color` |
| `Parse_Enum_WithPayload` | spec §15 `Result` |
| `Parse_Enum_MultiPayload` | `V(Int, Str)` |
| `Parse_Enum_TrailingComma` | aceito |
| `Parse_Match_Simple` | spec §22 |
| `Parse_Match_WithReturnArms` | spec §22 (segundo exemplo) |
| `Parse_Match_Wildcard` | `_ => 0` |
| `Parse_Match_LiteralPattern` | `1 => "one"` |
| `Parse_Match_NestedPattern` | `Result.Ok(Result.Ok(v)) => v` |
| `Parse_Match_QualifiedPattern` | `Result.Ok(v) => v` |
| `Parse_Match_BareIdent_IsBinding` | `outra => 0` ⇒ binding, não variante (Q3) |
| `Parse_Match_NoArms` | `match x { }` ⇒ `LAP0107` |
| `Parse_Match_ScrutineeIsUnrestricted` | `match p { ... }` sem regra contextual (Q2) |
| `Parse_Construct` | `.Point { x: 1, y: 2 };` |
| `Parse_Construct_Empty` | `.Unit { };` |
| `Parse_Construct_InIfCondition_NeedsNoParens` | `if .P { a: 1 }.b { }` aceito |
| `Parse_Block_NotConfusedWithConstruct` | `if p { }` — `p` é identificador, `{}` é o `then` |
| `Parse_Construct_DotIsRequired` | `Point { x: 1 };` ⇒ erro (é `Point` seguido de bloco) |

### Fase D — generics (M4) ✅

Em `tests/Lapis.Parser.Tests/GenericsParseTests.cs`.

| Teste | Fonte | Esperado |
|---|---|---|
| `GenericFn_Declaration` | `fn<T>(v: T) T { return v; }` | um `TypeParameterSyntax` sem `ConstType` |
| `GenericFn_ConstParameter` | `fn<T, N: Int>(...)` | um type + um const param |
| `GenericFn_ConstParameterOfFunctionType` | `fn<Make: fn() Int>(...)` | `ConstType` é `FunctionTypeSyntax` |
| `GenericType_Declaration` | `type<T> { value: T; }` | snapshot |
| `GenericType_ConstParameter` | `type<T, N: Int> { values: T[]; }` | snapshot |
| `GenericEnum_Declaration` | `enum<T, E> { Ok(T), Err(E) }` | snapshot |
| `TypeParameter_WithoutName_IsError` | `fn<1>(...)` | `LAP0112` |
| `GenericCall_Explicit` | `identity<Int>(10);` | `Call(Instantiate(...))` |
| `GenericCall_Multiple` | `f<Int, Str>(a, b);` | 2 args |
| `GenericCall_Omitted_ParsesAsPlainCall` | `identity(10);` | parseia; o erro (`LAP0290`) é do checker (Q7) |
| `GenericVariant_Access` | `Result<Int, IndexError>.Ok(1);` | `Call(Member(Instantiate(...)))` |
| `ConstGenericArguments_Mixed` | spec §13: `SomeType<"value", 1, true, Int, fn() Int { return 1; }>` | 5 args, tipos corretos de nó |
| `ConstGenericArgument_NegativeLiteral` | `Tagged<-1>` | o sinal entra no literal |
| `FunctionArgument_TypeVersusValue` | `F<fn(Int) Int, fn(a: Int) Int { return a; }>` | tipo e valor distinguidos pelo `{` |
| `GenericType_InAnnotation` | `x: Result<Int, IndexError>` | 2 args |
| `ConstGenericArgument_InAnnotation` | `x: FixedArray<Int, 3>` | o `3` é um `ValueArgumentSyntax` |
| `GenericType_Nested` | `Box<Box<Int>>` | sem `>>`, aninhado corretamente |
| `LessThan_IsNotGeneric` | `a < b;` | `BinaryExpression` |
| `LessThanChain_KeepsRelationalReading` | `a < b + 1;` | parse limpo |
| `LessThanGreaterThanParen_BecomesGenericCall` | `a < b > (c);` | vira chamada genérica **e** o teste documenta isso |
| `GreaterThanIdentifier_KeepsRelationalReading` | `(a < b) > c;` | parse limpo |
| `FailedSpeculation_ReportsNothing` | `a < b;` | zero diagnósticos vindos da tentativa |
| `Backtracking_IsLinear` | 50 `<` seguidos | termina em < 1s |
| `EmptyGenericArgumentList_InTypePosition_IsError` | `x: Box<>` | `LAP0116` |

### Recuperação de erro e robustez

| Teste | Asserção |
|---|---|
| `Recover_AfterBadStatement_ContinuesFile` | 2 defs, o 1º inválido ⇒ 2º parseado, 1 erro |
| `Recover_UnclosedBrace` | `LAP0108`, sem loop infinito |
| `Recover_UnexpectedEof` | `def x =` ⇒ `LAP0109` |
| `Recover_MultipleErrors_Reported` | arquivo com 5 erros ⇒ 5 diagnósticos |
| `Parser_NeverThrows` | property: tokens arbitrários ⇒ sem exceção |
| `Parser_AlwaysTerminates` | property com timeout |
| `Parse_DeepNesting_Reports_LAP0115` | 500 parênteses ⇒ diagnóstico, sem stack overflow |
| `Parse_EmptyFile` | `SourceFile` com 0 statements |
| `Parse_OnlyComments` | idem |

### Spans

| Teste | Asserção |
|---|---|
| `Span_OfBinary_CoversBothOperands` | span de `1+2` cobre de `1` a `2` |
| `Span_OfDef_CoversDefToSemicolon` | inclui `def` e `;` |
| `Span_OfCall_CoversCalleeAndParens` | idem |
| `EveryNode_HasNonEmptySpan` | property sobre `examples/*.ls` (exceto nós de erro) |

---

## Critérios de conclusão

- [x] Toda a gramática do Apêndice A implementada e coberta por snapshot.
- [x] `examples/hello.ls` produz o snapshot esperado (âncora do M1).
- [x] Os três casos de ambiguidade (§4.4) testados nos dois sentidos.
- [x] `Parser_NeverThrows` e `Parser_AlwaysTerminates` verdes.
- [x] `Lapis.Parser` não referencia `Lapis.Runtime` (teste de arquitetura).

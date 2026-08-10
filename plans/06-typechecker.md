# 06 — Type Checker

**Milestone:** M1 → M4 · **Depende de:** 02, 05 · **Projeto:** `Lapis.TypeChecker`

Corresponde à Etapa 5 da spec (§47) e aos requisitos de §26.

---

## Objetivo

`CoreProgram` → `TypedProgram` (ou lista de diagnósticos). Simples e previsível,
sem inferência sofisticada (spec §47).

O checker é responsável por três coisas que nenhuma outra fase faz:

1. **Resolução de nomes** — variáveis, campos, variantes de enum, parâmetros
   genéricos.
2. **Tipagem** — atribuir um `LapisType` a cada `NodeId`.
3. **Análise de fluxo de `return`** — garantir que toda função não-`Void` retorna
   em todos os caminhos (spec §12, §26).

---

## Escopo faseado

| Fase | Conteúdo | Milestone |
|---|---|---|
| A | escopos, primitivos, funções, chamadas, `if`, `return` + análise de caminhos | M1 |
| B | arrays, indexação ⇒ `Result<T, IndexError>`, prelude | M2 |
| C | `type`, `enum`, `match` + exaustividade, campos, variantes | M3 |
| D | generics de tipo + inferência de 1ª ordem + const generics | M4 |

---

## O que será construído

### 6.1 API

```csharp
public sealed class TypeChecker
{
    public static TypedProgram Check(CoreProgram program, PreludeScope prelude, DiagnosticBag diagnostics);
}
```

Uma travessia única, dirigida por sintaxe, com ambiente léxico. Sem unificação
global, sem geração de constraints — apenas checagem "de cima para baixo" com
tipos esperados propagados onde ajuda (literais de array, argumentos de chamada).

### 6.2 Escopo e resolução de nomes

```csharp
sealed record BindingId(int Value);

sealed class Scope
{
    Scope? Parent;
    Dictionary<string, BindingInfo> Bindings;
}

sealed record BindingInfo(BindingId Id, string Name, LapisType Type, SourceSpan Span,
                          BindingKind Kind);   // Value | TypeDef | EnumVariant | GenericParam
```

Regras:

- `Let` introduz o binding **apenas no seu corpo** (`Let(x, v, body)`: `x` não é
  visível em `v`). Consequência: **não há recursão** em v0.2 — `def fact = fn(n)
  { return fact(n-1); }` é `LAP0201` (variável inexistente).
  Isso é uma decisão explícita, registrada em [Apêndice C](appendix-c-decisions.md):
  a spec §8 não menciona recursão, e sem ela a terminação do partial evaluator é
  trivialmente garantida — o que é bom para M6–M8. Recursão entra em v0.3 junto
  com a estratégia de terminação do PE.
- **Shadowing é permitido** em escopo interno, e é `LAP0202` (erro) no mesmo
  escopo. Bindings são imutáveis (spec §8), então shadowing não é mutação.
- `def` de um `enum` também injeta os nomes das variantes no escopo corrente (Q3),
  permitindo `Ok(10)` além de `Result.Ok(10)`. Colisão com nome existente ⇒
  `LAP0203`.

### 6.3 Regras de tipagem

| Nó | Regra |
|---|---|
| `Literal` | pelo `ConstantValue` |
| `Variable` | tipo do binding; ausente ⇒ `LAP0201`, tipo `ErrorType` |
| `Let` | tipa `Value`; se há anotação, exige compatibilidade (`LAP0210`); tipo do `Let` é o tipo do `Body` |
| `Lambda` | tipa o corpo com parâmetros em escopo; tipo é `FunctionType(params, ret)`; roda a análise de `return` (§6.4) |
| `Call` | callee deve ser `FunctionType` (senão `LAP0220`); aridade exata (`LAP0221`); cada argumento compatível (`LAP0222`); tipo = retorno |
| `Return` | tipo `Never`; valida contra o retorno da função envolvente (§6.4) |
| `If` | condição deve ser `Bool` (`LAP0230`); ramos devem ter tipo comum (`LAP0231`); tipo = junção (§6.6) |
| `Binary` | tabela de operadores (§6.5) |
| `Unary` | `-` em `Int`/`Float`; `!` em `Bool` |
| `Array` | todos os elementos com o mesmo tipo (`LAP0240`); vazio sem anotação ⇒ `LAP0241` |
| `Index` | alvo deve ser `ArrayType` (`LAP0242`); índice deve ser `Int` (`LAP0243`); **tipo = `Result<T, IndexError>`** (spec §21) |
| `Field` | em `NamedType` struct ⇒ tipo do campo (`LAP0250` se ausente); em `MetaType(enum)` ⇒ construtor/valor da variante (`LAP0251`) |
| `Construct` | alvo deve ser `MetaType` de struct (`LAP0252`); campos exatos, sem faltas (`LAP0253`) nem extras (`LAP0254`); tipos compatíveis |
| `Match` | escrutinado de tipo `T`; todo padrão compatível com `T` (`LAP0260`); braços com tipo comum (`LAP0261`); exaustividade (`LAP0262`); braço inalcançável ⇒ warning `LAP0263` |
| `TypeDef` / `EnumDef` | registra `TypeDefinition`; tipo do nó é `MetaType(...)` |

### 6.4 Análise de `return` (spec §12, §26)

Duas verificações, ambas sobre o corpo de cada `Lambda`.

**(a) Tipo do valor retornado.** O checker mantém uma pilha de "função corrente".
`Return(e)` exige `typeof(e)` compatível com o retorno declarado (`LAP0270`).
`Return(null)` só é válido se o retorno for `Void` (`LAP0271` — item explícito da
spec §26).

**(b) Retorno definido em todos os caminhos.** Função `Definitely Returns`:

```text
DR(Return _)          = true
DR(Let(_, v, body))   = DR(v) || DR(body)
DR(If(c, t, e))       = DR(c) || (DR(t) && DR(e))
DR(Match(s, arms))    = DR(s) || (arms não vazio && ∀arm. DR(arm.body) && exaustivo)
DR(Binary(_, l, r))   = DR(l) || DR(r)
DR(Call(f, args))     = DR(f) || ∃a ∈ args. DR(a)
DR(_)                 = false
```

`DR(v) || DR(body)` no `Let` cobre `def x = return 1;` — bizarro mas
tipável, já que `Return` tem tipo `Never`.

Se o retorno da lambda **não** é `Void` e `DR(body)` é falso ⇒ `LAP0272`
("caminho de execução sem return"), apontando para a chave de fechamento do
corpo. Se o retorno é `Void`, nada é exigido (spec §12).

Código após um `Return` no mesmo `Let`-encadeamento é inalcançável ⇒ warning
`LAP0273`.

### 6.5 Tabela de operadores binários

| Op | Tipos aceitos | Resultado |
|---|---|---|
| `+` | `Int×Int`, `Float×Float`, `Str×Str` (concatenação) | mesmo tipo |
| `-` `*` `/` | `Int×Int`, `Float×Float` | mesmo tipo |
| `<` `>` `<=` `>=` | `Int×Int`, `Float×Float`, `Str×Str` (ordem lexicográfica) | `Bool` |
| `==` `!=` | ambos os lados do **mesmo** tipo, e o tipo deve ser comparável (§6.7) | `Bool` |

**Sem promoção implícita** `Int → Float` (spec §47: previsível). `1 + 1.0` é
`LAP0280`. Conversão explícita entra quando houver funções de conversão no
prelude.

Divisão por zero **não** é erro de tipo: é semântica de runtime (plano 08 define
o comportamento).

### 6.6 Junção de tipos (`if` e `match`)

Única relação de subtipagem da linguagem: `Never <: T` para todo `T`.

```text
join(Never, T)  = T
join(T, Never)  = T
join(T, T)      = T
join(Error, T)  = Error
join(T, U)      = erro
```

Isso é o que faz `if c { return 1; } else { 2 }` tipar, e o que faz o `match` da
spec §22 com `return` nos dois braços ter tipo `Never`.

### 6.7 Igualdade estrutural comparável

`==` é permitido em: primitivos, arrays de comparáveis, enums cujas cargas são
comparáveis, structs cujos campos são comparáveis. **Não** é permitido em
funções (`LAP0281`) — igualdade de closures não é decidível e destruiria a
equivalência PE/evaluator.

### 6.8 Generics (M4)

**Declaração** (Q1):

```c
def identity = fn<T>(value: T) T { return value; };
def FixedArray = type<T, N: Int> { values: T[]; };
```

Parâmetros de tipo entram no escopo como `TypeParameterType`; parâmetros const
entram como valores do tipo declarado, utilizáveis em posição de expressão e em
posição de argumento genérico.

**Uso**: `identity<Int>(10)`, `FixedArray<Int, 3>`.

**Verificações** (spec §26, último item):

| Situação | Diagnóstico |
|---|---|
| aridade genérica errada | `LAP0290` |
| valor const onde se espera tipo | `LAP0291` |
| tipo onde se espera valor const | `LAP0292` |
| const de tipo errado (`N: Int` recebendo `"a"`) | `LAP0293` |
| argumento const não é constante em tempo de compilação | `LAP0294` |
| tipo genérico usado sem argumentos | `LAP0295` |

**Instanciação:** por substituição. `identity<Int>` produz uma `FunctionType(Int)
Int`. `Box<Int>` produz `NamedType(Box, [TypeArg(Int)])`. O checker **não**
monomorfiza o corpo — quem especializa corpos é o partial evaluator (plano 13),
e essa divisão é exatamente o objeto de pesquisa do projeto.

**Inferência (Q7):** quando os argumentos genéricos são omitidos numa chamada, o
checker infere os **parâmetros de tipo** por casamento de 1ª ordem entre os tipos
dos parâmetros formais e os tipos dos argumentos reais, da esquerda para a
direita. Parâmetros **const** nunca são inferidos — devem ser explícitos
(`LAP0296`). Parâmetro de tipo que não aparece em nenhum parâmetro formal não é
inferível ⇒ `LAP0297`.

Isso é o mínimo necessário para `print(result)` (spec §34) funcionar com
`print: fn<T>(value: T) Void`.

### 6.9 Prelude

O checker recebe um `PreludeScope` já tipado (plano 09) contendo `Result`,
`IndexError` e `print`. O tipo de `Index` é montado como
`NamedType(Result, [TypeArg(elem), TypeArg(NamedType(IndexError,[]))])` — buscando
`Result` **no prelude**, não em uma tabela hardcoded. Se o prelude não define
`Result` ou `IndexError`, é `InternalCompilerException` (bug da implementação,
não do usuário).

---

## Decisões de design

**Sem inferência global (Hindley-Milner).** A spec §47 pede simples e previsível.
Anotações são obrigatórias em parâmetros de função; tudo o mais flui de baixo
para cima, com o tipo esperado descendo apenas em três lugares: anotação de
`Let`, elementos de literal de array e argumentos de chamada.

**`Never` em vez de regra especial para `return`.** Um único conceito resolve
`return` em posição de expressão, braços de `match` com `return` e `if` com
`return` num ramo só.

**Sem promoção numérica.** Previsibilidade e, principalmente, o partial evaluator:
constant folding com promoção implícita exige replicar exatamente as regras de
conversão, e qualquer divergência quebraria a equivalência PE/evaluator.

---

## Testes necessários

Todo teste negativo assevera **código + span**, nunca a mensagem.

### Escopo e resolução (Fase A)

| Teste | Fonte | Esperado |
|---|---|---|
| `Var_Unknown` | `x;` | `LAP0201` |
| `Var_UsedBeforeDef` | `x; def x = 1;` | `LAP0201` |
| `Var_NotVisibleInOwnInitializer` | `def f = fn(){ return f(); };` | `LAP0201` (sem recursão) |
| `Var_ShadowingInInnerScope_Allowed` | `def x=1; { def x="s"; }` | sem erro |
| `Var_RedefinitionInSameScope` | `def x=1; def x=2;` | `LAP0202` |
| `Closure_CapturesOuterBinding` | spec §32 | tipa |
| `EnumDef_InjectsVariantNames` | `def C = enum{Red};` + `Red;` | tipa |
| `EnumDef_VariantNameCollision` | variante colide com binding | `LAP0203` |

### Primitivos e operadores

| Teste | Asserção |
|---|---|
| `Literal_Types` | `1:Int`, `1.0:Float`, `true:Bool`, `"s":Str`, `():Void` |
| `Binary_IntArithmetic` | `1+2 : Int` |
| `Binary_FloatArithmetic` | `1.0*2.0 : Float` |
| `Binary_StrConcat` | `"a"+"b" : Str` |
| `Binary_MixedNumeric_Rejected` | `1+1.0` ⇒ `LAP0280` |
| `Binary_Comparison_ProducesBool` | `1<2 : Bool` |
| `Binary_StrComparison` | `"a"<"b" : Bool` |
| `Binary_EqualityRequiresSameType` | `1=="a"` ⇒ `LAP0280` |
| `Binary_EqualityOnFunctions_Rejected` | `f==g` ⇒ `LAP0281` |
| `Unary_NegateOnBool_Rejected` | `-true` ⇒ `LAP0280` |
| `Unary_NotOnInt_Rejected` | `!1` ⇒ `LAP0280` |

### Funções e chamadas

| Teste | Fonte | Esperado |
|---|---|---|
| `Fn_Type` | `fn(a:Int,b:Int) Int {...}` | `fn(Int,Int) Int` |
| `Fn_NoReturnType_IsVoid` | `fn(){}` | `fn() Void` |
| `Call_Ok` | `add(1,2)` | `Int` |
| `Call_TooFewArgs` | `add(1)` | `LAP0221` |
| `Call_TooManyArgs` | `add(1,2,3)` | `LAP0221` |
| `Call_WrongArgType` | `add(1,"s")` | `LAP0222`, span do argumento |
| `Call_NonFunction` | `1(2)` | `LAP0220` |
| `Call_ResultUsed` | `def x: Int = add(1,2);` | tipa |
| `Fn_AsArgument` | passar função como argumento | tipa |
| `Fn_ReturnedFromFn` | função de ordem superior | tipa |
| `Fn_InArrayLiteral` | spec §11 | tipa |

### `return` e análise de caminhos

| Teste | Fonte | Esperado |
|---|---|---|
| `Return_Ok` | `fn() Int { return 1; }` | tipa |
| `Return_WrongType` | `fn() Int { return "s"; }` | `LAP0270` |
| `Return_EmptyInNonVoid` | `fn() Int { return; }` | `LAP0271` |
| `Return_EmptyInVoid` | `fn() Void { return; }` | tipa |
| `Void_NoReturn_Ok` | `fn() Void { print(1); }` | tipa |
| `NonVoid_NoReturn` | `fn() Int { 1 }` | `LAP0272` |
| `NonVoid_ReturnOnlyInThen` | `fn() Int { if c { return 1; } }` | `LAP0272` |
| `NonVoid_ReturnInBothBranches` | `if c { return 1; } else { return 2; }` | tipa |
| `NonVoid_EarlyReturnThenFinal` | spec §12 `abs` | tipa |
| `NonVoid_ReturnInAllMatchArms` | spec §22 `unwrapOr` | tipa |
| `NonVoid_MatchNotExhaustive_NoReturn` | `LAP0262` + `LAP0272` |
| `Return_InsideNestedLambda_BindsToInner` | `fn() Int { def g = fn() Str { return "s"; }; return 1; }` | tipa |
| `Return_InsideNestedLambda_DoesNotSatisfyOuter` | lambda externa sem return próprio | `LAP0272` |
| `Unreachable_AfterReturn` | `return 1; print(2);` | warning `LAP0273` |
| `Return_AtTopLevel_Rejected` | `return 1;` fora de função | `LAP0274` |

### Arrays e indexação (Fase B)

| Teste | Fonte | Esperado |
|---|---|---|
| `Array_Homogeneous` | `[1,2,3]` | `Int[]` |
| `Array_Heterogeneous` | `[1,"a",true]` | `LAP0240` (spec §18) |
| `Array_Empty_WithAnnotation` | `def a: Int[] = [];` | tipa |
| `Array_Empty_WithoutAnnotation` | `def a = [];` | `LAP0241` |
| `Array_Nested` | `[[1],[2]]` | `Int[][]` |
| `Array_Annotation_Mismatch` | `def a: Str[] = [1];` | `LAP0210` |
| `Index_ReturnsResult` | `def r = [1,2,3][0];` | `Result<Int, IndexError>` — **teste central da spec §21** |
| `Index_NotInt` | `a["s"]` | `LAP0243` |
| `Index_OnNonArray` | `1[0]` | `LAP0242` |
| `Index_ResultNotUnwrappedImplicitly` | `def x: Int = a[0];` | `LAP0210` |

### Enums, structs, match (Fase C)

| Teste | Asserção |
|---|---|
| `Enum_Def_ProducesMetaType` | `def C = enum{Red};` ⇒ `C : MetaType` |
| `Enum_Variant_Nullary` | `C.Red : C` |
| `Enum_Variant_WithPayload` | `Result.Ok(1) : Result<Int, ?>` |
| `Enum_Variant_WrongPayloadArity` | `LAP0251` |
| `Enum_UnknownVariant` | `C.Purple` ⇒ `LAP0251` |
| `Struct_Def_ProducesMetaType` | spec §14 `User` |
| `Struct_Construct_Ok` | `User { id: 1, name: "g" }` |
| `Struct_Construct_MissingField` | `LAP0253` |
| `Struct_Construct_ExtraField` | `LAP0254` |
| `Struct_Construct_WrongFieldType` | `LAP0222` |
| `Struct_FieldAccess` | `u.id : Int` |
| `Struct_UnknownField` | `u.zzz` ⇒ `LAP0250` |
| `Struct_NominalNotStructural` | dois `type { id: Int }` distintos não são intercambiáveis |
| `Match_ArmsMustAgree` | braços com tipos diferentes ⇒ `LAP0261` |
| `Match_Exhaustive_AllVariants` | tipa |
| `Match_NonExhaustive` | `LAP0262` (Q6) |
| `Match_WildcardMakesExhaustive` | tipa |
| `Match_UnreachableArm_AfterWildcard` | warning `LAP0263` |
| `Match_DuplicateVariantArm` | warning `LAP0263` |
| `Match_PatternBindsPayload` | `Ok(v) => v` ⇒ `v` no escopo do braço com o tipo da carga |
| `Match_PatternTypeMismatch` | padrão de outro enum ⇒ `LAP0260` |
| `Match_BareVariantPattern` | `Ok(v)` sem qualificação resolve para a variante (Q3) |
| `Match_IdentifierPattern_IsBinding` | `x => x` com `x` não sendo variante ⇒ binding |
| `Match_LiteralPattern_OnInt` | `1 => "a"` tipa; exaustividade exige `_` |
| `Match_ArmScope_DoesNotLeak` | binding do padrão não visível fora do braço |

### Generics (Fase D)

| Teste | Fonte | Esperado |
|---|---|---|
| `Generic_Identity_Explicit` | `identity<Int>(10)` | `Int` |
| `Generic_Identity_Inferred` | `identity(10)` | `Int` (Q7) |
| `Generic_Identity_WrongArg` | `identity<Int>("s")` | `LAP0222` |
| `Generic_ArityMismatch` | `identity<Int,Str>(1)` | `LAP0290` |
| `Generic_Inference_FromMultipleParams` | `pair(1,"s")` com `fn<A,B>` | `A=Int, B=Str` |
| `Generic_Inference_Conflicting` | `same(1,"s")` com `fn<T>(a:T,b:T)` | `LAP0222` |
| `Generic_Uninferable` | `fn<T>() T` chamada sem args | `LAP0297` |
| `Generic_Type_Instantiation` | `Box<Int>` ⇒ `NamedType(Box,[Int])` |
| `Generic_Type_WithoutArgs` | `def b: Box = ...` | `LAP0295` |
| `Generic_Enum_Result` | `Result<Int, IndexError>` | tipa |
| `Const_Generic_Ok` | `FixedArray<Int, 3>` | tipa |
| `Const_Generic_WrongKind_TypeForConst` | `FixedArray<Int, Str>` | `LAP0292` |
| `Const_Generic_WrongKind_ConstForType` | `FixedArray<3, 3>` | `LAP0291` |
| `Const_Generic_WrongConstType` | `FixedArray<Int, "a">` | `LAP0293` |
| `Const_Generic_NonConstant` | `FixedArray<Int, x>` com `x` runtime | `LAP0294` |
| `Const_Generic_NotInferred` | `FixedArray<Int>` | `LAP0296` |
| `Const_Generic_MixedArgs` | spec §13 `SomeType<"value",1,true,Int,fn() Int {return 1;}>` | tipa — **teste central da spec §13** |
| `Const_Generic_FunctionValueArg` | `fn() Int { return 1; }` como argumento genérico | aceito, `ConstFunction` |
| `Generic_DistinctInstantiations_AreDistinctTypes` | `Box<Int>` ≠ `Box<Str>` |
| `Generic_ConstArgs_AffectTypeIdentity` | `FixedArray<Int,3>` ≠ `FixedArray<Int,4>` |

### Saída (`TypedProgram`)

| Teste | Asserção |
|---|---|
| `EveryNode_HasType` | property: `NodeTypes.Count == program.NodeCount` |
| `Call_HasCallResolution` | toda chamada genérica tem `CallResolution` com args resolvidos |
| `Field_HasFieldResolution` | índice de campo correto |
| `VariantAccess_HasVariantResolution` | índice de variante correto |
| `Errors_ProduceErrorType_NoCascade` | `x + 1` com `x` desconhecido ⇒ exatamente 1 diagnóstico |
| `Checker_NeverThrows` | property sobre Core ASTs geradas de fontes arbitrárias |

---

## Critérios de conclusão

- [ ] Todos os itens da lista da spec §26 têm diagnóstico e teste.
- [ ] `def r = [1,2,3][0];` tipa como `Result<Int, IndexError>`.
- [ ] Análise de `return` cobrindo `if`, `match`, blocos aninhados e lambdas
      aninhadas.
- [ ] Exemplo de const generics da spec §13 tipando.
- [ ] `TypedProgram` com tipo para 100% dos nós.
- [ ] Zero cascatas: um erro de origem gera exatamente um diagnóstico.

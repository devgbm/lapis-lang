# 07 — Runtime: Valores, Ambiente e Primitivas

**Milestone:** M1 → M2 · **Depende de:** 02 · **Projeto:** `Lapis.Runtime`

Cobre a spec §27 (valores), §29 (primitivas), §31 (ambiente) e §32 (closures).

---

## Objetivo

Fornecer a representação de valores de execução, o ambiente léxico e o conjunto
**mínimo** de primitivas — sem conhecer o evaluator (spec §36: o runtime é
consumido pelo evaluator, não o contrário).

---

## Escopo

**Entra:** `Value` e subtipos, `Environment`, `Closure`, primitivas nativas,
formatação de valores para `print`.

**Fica de fora:** a regra de avaliação de qualquer nó — isso é o plano 08. E
qualquer implementação especial de `Result`: `Result` é um enum **da linguagem**
(spec §16), definido em `prelude.ls` (plano 09).

---

## O que será construído

### 7.1 `Value`

```csharp
abstract record Value;

sealed record IntValue(long Value)      : Value;
sealed record FloatValue(double Value)  : Value;
sealed record BoolValue(bool Value)     : Value;
sealed record StrValue(string Value)    : Value;
sealed record VoidValue                 : Value;          // singleton
sealed record ArrayValue(ImmutableArray<Value> Elements, LapisType ElementType) : Value;
sealed record ClosureValue(CoreLambda Lambda, Environment Captured,
                           ImmutableArray<SemanticGenericArg> GenericArguments) : Value;
sealed record EnumValue(TypeDefinition Definition, int VariantIndex,
                        ImmutableArray<Value> Payload,
                        ImmutableArray<SemanticGenericArg> GenericArguments) : Value;
sealed record StructValue(TypeDefinition Definition, ImmutableArray<Value> Fields,
                          ImmutableArray<SemanticGenericArg> GenericArguments) : Value;
sealed record TypeValue(LapisType Type) : Value;
sealed record NativeFunctionValue(string Name, FunctionType Signature,
                                  Func<ImmutableArray<Value>, Value> Implementation) : Value;
```

Diferenças em relação à lista da spec §27:

| Adição | Motivo |
|---|---|
| `StructValue` | a spec lista `TypeValue` mas não o valor de uma instância de `type` |
| `NativeFunctionValue` | `print` e as poucas primitivas precisam ser valores chamáveis |

`ArrayValue` guarda `ElementType` porque um array vazio precisa saber seu tipo
para formatação e para o PE.

**Igualdade:** `record` dá igualdade estrutural, que é exatamente a semântica de
`==` da linguagem (plano 06 §6.7) — exceto para `ClosureValue` e
`NativeFunctionValue`, cuja comparação é proibida pelo checker. Para garantir,
`ClosureValue` sobrescreve `Equals` lançando `InternalCompilerException`: se
alguma vez for chamado, é bug do checker.

**Imutabilidade total.** Não há mutação em v0.2 (spec §8), então nenhum `Value`
precisa de cópia defensiva. Isso também torna trivial reusar valores entre o
evaluator e o partial evaluator.

### 7.2 `Environment` (spec §31)

```csharp
public sealed class Environment
{
    public Environment? Parent { get; }
    public bool TryLookup(string name, out Value value);
    public Environment Extend(string name, Value value);        // devolve novo ambiente
    public Environment ExtendAll(ReadOnlySpan<(string, Value)> bindings);
    public static readonly Environment Empty;
}
```

**Imutável e persistente**: `Extend` devolve um novo ambiente encadeado ao pai.
Motivo: closures capturam o ambiente por referência (spec §32) e, com bindings
imutáveis, um ambiente persistente é ao mesmo tempo correto e barato — e é o que
permite ao partial evaluator manter vários ambientes vivos ao especializar.

Otimização a implementar **só se** medições mostrarem necessidade: cadeia com
mapas pequenos (array linear até 8 entradas, `ImmutableDictionary` acima). Não
otimizar antes de medir.

### 7.3 Primitivas (spec §29)

O conjunto mínimo. Cada primitiva é um `NativeFunctionValue` registrado no
escopo raiz.

| Primitiva | Assinatura | Semântica |
|---|---|---|
| `print` | `fn<T>(value: T) Void` | escreve `Format(value)` + `\n` no `IOutput` |
| `array_length` | `fn<T>(array: T[]) Int` | comprimento |

Além disso, o runtime expõe operações usadas diretamente pelo evaluator (não são
valores da linguagem, são funções C#):

```csharp
public static class Primitives
{
    public static Value Add(Value l, Value r);       // Int+Int, Float+Float, Str+Str
    public static Value Subtract(Value l, Value r);
    public static Value Multiply(Value l, Value r);
    public static Value Divide(Value l, Value r);               // total (Q9)
    public static Value Negate(Value v);
    public static Value Not(Value v);
    public static BoolValue Compare(BinaryOperator op, Value l, Value r);
    public static BoolValue StructuralEquals(Value l, Value r);
    public static IndexOutcome ArrayGet(ArrayValue array, long index);  // bounds check
}
```

Todas assumem que o checker já validou os tipos: um tipo inesperado é
`InternalCompilerException`, não diagnóstico.

**Divisão por zero (Q9).** `Int` divisão por zero produz `long.MaxValue`;
`Float` segue IEEE 754. A divisão é **total**, então nenhuma operação binária
falha e o evaluator não tem caminho de aborto aritmético. Único caso de borda:
`MinValue / -1` estoura em Int64 e envolve para `MinValue`, coerente com o resto
da aritmética.

**Bounds check** (spec §41): `ArrayGet` sempre verifica `0 <= index < length` e
devolve `IndexOutcome.InBounds(value)` ou `IndexOutcome.OutOfBounds`. Quem
converte isso em `Ok(v)` / `Err(IndexError.OutOfBounds)` é o evaluator, usando os
construtores do prelude — o runtime **não** conhece `Result` (spec §16).

### 7.4 Formatação de valores

```csharp
public static class ValueFormatter
{
    public static string Format(Value value);          // para print
    public static string FormatDebug(Value value);     // para testes e REPL
}
```

Regras (fixadas agora porque os golden files dependem delas):

| Valor | `Format` |
|---|---|
| `IntValue(30)` | `30` |
| `FloatValue(3.14)` | `3.14` (invariante; `1.0` imprime `1.0`, não `1`) |
| `BoolValue(true)` | `true` |
| `StrValue("hi")` | `hi` (sem aspas em `print`; **com** aspas em `FormatDebug`) |
| `VoidValue` | `()` |
| `ArrayValue` | `[1, 2, 3]` |
| `EnumValue` | `Result.Ok(20)`, `IndexError.OutOfBounds` — qualificado, como a linguagem exige escrever (Q3) |
| `StructValue` | `User { id: 1, name: "g" }` |
| `ClosureValue` | `<fn(Int, Int) Int>` |
| `TypeValue` | `<type User>` |

`Format` é cultura-invariante. Testado explicitamente sob `pt-BR`.

### 7.5 Abstração de saída

```csharp
public interface IOutput { void Write(string text); }
public sealed class ConsoleOutput : IOutput;
public sealed class StringOutput  : IOutput { public string Text { get; } }
```

`print` escreve num `IOutput` injetado, nunca em `Console` diretamente — sem isso
os testes de conformidade (plano 11) não conseguiriam capturar a saída.

### 7.6 Chamada de closure sem depender do evaluator

O runtime precisa poder invocar uma closure (por exemplo, se uma primitiva futura
receber um callback), mas não pode referenciar `Lapis.Evaluator`. Solução:

```csharp
public delegate Value Invoker(ClosureValue closure, ImmutableArray<Value> arguments);
public sealed class RuntimeContext(IOutput output, Invoker invoke);
```

O evaluator fornece o `Invoker` na construção do contexto. Inversão de dependência
simples que mantém o grafo do plano 00 acíclico.

---

## Testes necessários

Ficam em `tests/Lapis.Evaluator.Tests/Runtime/` (o runtime não tem projeto de
teste próprio, para não multiplicar projetos).

### Valores

| Teste | Asserção |
|---|---|
| `Values_StructuralEquality` | `IntValue(1) == IntValue(1)` |
| `Values_DifferentKinds_NotEqual` | `IntValue(1) != FloatValue(1.0)` |
| `ArrayValue_EqualityIsElementwise` | `[1,2] == [1,2]`, `[1,2] != [2,1]` |
| `EnumValue_EqualityConsidersVariant` | `Ok(1) != Err(1)` |
| `EnumValue_EqualityConsidersDefinition` | mesma variante de enums distintos ⇒ diferente |
| `StructValue_EqualityIsFieldwise` | |
| `ClosureValue_Equals_Throws` | `InternalCompilerException` |
| `VoidValue_IsSingleton` | referência única |

### Ambiente

| Teste | Asserção |
|---|---|
| `Env_Lookup_Local` | encontra binding local |
| `Env_Lookup_Parent` | encontra no pai |
| `Env_Lookup_Missing` | `TryLookup` devolve false |
| `Env_Shadowing_InnerWins` | filho sobrepõe pai |
| `Env_Extend_DoesNotMutateParent` | pai inalterado após `Extend` |
| `Env_Extend_IsPersistent` | duas extensões do mesmo pai são independentes |
| `Env_DeepChain_Lookup` | 1000 níveis, encontra a raiz |
| `Env_ExtendAll_Order` | último binding do mesmo nome vence |

### Primitivas

| Teste | Asserção |
|---|---|
| `Add_Int`, `Add_Float`, `Add_Str` | resultados corretos |
| `Add_MismatchedTypes_Throws` | `InternalCompilerException` (checker deveria ter pego) |
| `Divide_Int_ByZero_IsMaxValue` | `long.MaxValue`, para qualquer sinal do dividendo |
| `Divide_MinValueByMinusOne_Wraps` | `long.MinValue` |
| `Divide_IsTotal_ForEveryIntPair` | nenhuma combinação lança |
| `Divide_Float_ByZero_IsInfinity` | IEEE 754 |
| `Divide_Int_Truncates` | `7/2 == 3` |
| `Divide_Int_NegativeTruncation` | `-7/2 == -3` (truncamento para zero, como C#) |
| `Negate_MinValue` | comportamento documentado (wrap, como C# `unchecked`) |
| `Compare_Str_Ordinal` | `"a" < "b"`; comparação **ordinal**, não linguística |
| `StructuralEquals_NestedArrays` | recursão correta |
| `ArrayGet_InBounds_First` | `[10,20,30][0] ⇒ InBounds(10)` |
| `ArrayGet_InBounds_Last` | `[10,20,30][2] ⇒ InBounds(30)` |
| `ArrayGet_Negative` | `[..][-1] ⇒ OutOfBounds` |
| `ArrayGet_TooLarge` | `[..][3] ⇒ OutOfBounds` |
| `ArrayGet_EmptyArray` | `[][0] ⇒ OutOfBounds` |
| `ArrayLength` | `array_length([1,2,3]) == 3` |

### Formatação

| Teste | Asserção |
|---|---|
| `Format_Int`, `Format_Bool`, `Format_Void` | conforme tabela §7.4 |
| `Format_Float_KeepsDecimalPoint` | `1.0` ⇒ `"1.0"` |
| `Format_Float_Invariant_UnderPtBr` | `3.14` ⇒ `"3.14"` |
| `Format_Str_NoQuotes_InPrint` | `hi` |
| `Format_Str_Quotes_InDebug` | `"hi"` |
| `Format_Array_Nested` | `[[1], [2]]` |
| `Format_Enum_Nullary` | `IndexError.OutOfBounds` |
| `Format_Enum_WithPayload` | `Result.Ok(20)` |
| `Format_Struct` | `User { id: 1, name: "g" }` |
| `Format_Closure` | `<fn(Int, Int) Int>` |

### Saída

| Teste | Asserção |
|---|---|
| `StringOutput_CapturesWrites` | acumula texto |
| `Print_AppendsNewline` | `print(1)` ⇒ `"1\n"` |
| `Print_DoesNotTouchConsole` | nenhum escrita em `Console.Out` durante testes |

---

## Critérios de conclusão

- [ ] Todos os `Value` implementados com igualdade testada.
- [ ] `Environment` persistente com testes de shadowing e profundidade.
- [ ] `ArrayGet` com bounds check e os 5 casos de fronteira verdes.
- [ ] `ValueFormatter` cultura-invariante.
- [ ] `Lapis.Runtime` não referencia `Lapis.Evaluator` (teste de arquitetura).
- [ ] Nenhuma menção a `Result` ou `IndexError` no código do runtime.

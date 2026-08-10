# 02 — AST: Surface, Core, Tipos e Printers

**Milestone:** M1 (cresce em M2/M3) · **Depende de:** 00, 01 · **Projeto:** `Lapis.Ast`

---

## Objetivo

Definir as três representações do programa e as ferramentas para imprimi-las:

1. **Surface AST** — espelha a sintaxe escrita. Saída do parser.
2. **Core AST** — pequena e uniforme. Saída do desugar; entrada do type checker,
   do evaluator e do partial evaluator.
3. **Modelo semântico de tipos** — `LapisType`, usado pelo checker, pelo runtime
   e pelo PE.

Conforme spec §36, este projeto **não conhece** evaluator, runtime ou valores de
execução. Depende apenas de `Lapis.Diagnostics`.

---

## Escopo

**Entra:** definições de nós, modelo de tipos, `ConstantValue`, visitors,
printers (S-expression e source-like).

**Fica de fora:** parsing, checagem, avaliação. Zero lógica além de construção,
travessia e impressão.

---

## O que será construído

### 2.1 Surface AST — `Lapis.Ast.Surface`

Nós são `sealed record` (igualdade estrutural facilita testes de parser). Todos
herdam de `SurfaceNode` com `SourceSpan Span`.

```csharp
sealed record SourceFile(ImmutableArray<Statement> Statements);

abstract record Statement;
  sealed record DefStatement(string Name, TypeSyntax? Annotation, Expression Value);
  sealed record ExpressionStatement(Expression Expression);

abstract record Expression;
  // literais
  sealed record IntLiteral(long Value, string RawText);
  sealed record FloatLiteral(double Value, string RawText);
  sealed record BoolLiteral(bool Value);
  sealed record StrLiteral(string Value);
  sealed record UnitLiteral;                                   // ()
  // nomes e operadores
  sealed record IdentifierExpression(string Name);
  sealed record UnaryExpression(UnaryOperator Op, Expression Operand);
  sealed record BinaryExpression(BinaryOperator Op, Expression Left, Expression Right);
  // estruturais
  sealed record BlockExpression(ImmutableArray<Statement> Statements, Expression? Tail);
  sealed record IfExpression(Expression Condition, BlockExpression Then, Expression? Else);
  sealed record MatchExpression(Expression Scrutinee, ImmutableArray<MatchArm> Arms);
  sealed record ReturnExpression(Expression? Value);
  // definições que são expressões
  sealed record FunctionExpression(
      ImmutableArray<GenericParameterSyntax> GenericParameters,
      ImmutableArray<ParameterSyntax> Parameters,
      TypeSyntax? ReturnType,                                  // ausente ⇒ Void
      BlockExpression Body);
  sealed record TypeExpression(
      ImmutableArray<GenericParameterSyntax> GenericParameters,
      ImmutableArray<FieldSyntax> Fields);
  sealed record EnumExpression(
      ImmutableArray<GenericParameterSyntax> GenericParameters,
      ImmutableArray<VariantSyntax> Variants);
  // aplicação e acesso
  sealed record CallExpression(
      Expression Callee,
      ImmutableArray<GenericArgumentSyntax> GenericArguments,  // vazio ⇒ inferir
      ImmutableArray<Expression> Arguments);
  sealed record IndexExpression(Expression Target, Expression Index);
  sealed record MemberExpression(Expression Target, string Name);
  sealed record ArrayExpression(ImmutableArray<Expression> Elements);
  sealed record ConstructExpression(                            // Q2: .Nome { ... }
      string TypeName,
      ImmutableArray<GenericArgumentSyntax> GenericArguments,
      ImmutableArray<FieldInitSyntax> Fields);

sealed record ParameterSyntax(string Name, TypeSyntax Type);
sealed record FieldSyntax(string Name, TypeSyntax Type);
sealed record FieldInitSyntax(string Name, Expression Value);
sealed record VariantSyntax(string Name, ImmutableArray<TypeSyntax> Payload);
sealed record MatchArm(Pattern Pattern, Expression Body);
```

Operadores:

```csharp
enum UnaryOperator { Negate, Not }
enum BinaryOperator {
    Add, Subtract, Multiply, Divide,
    Equal, NotEqual, Less, Greater, LessOrEqual, GreaterOrEqual,
    AndAlso, OrElse        // [extensão Q4] — desaparecem no desugar
}
```

Padrões:

```csharp
abstract record Pattern;
  sealed record WildcardPattern;                        // _
  sealed record BindingPattern(string Name);            // sempre liga um nome novo
  sealed record VariantPattern(string EnumName, string VariantName,
                               ImmutableArray<Pattern> Arguments);
  sealed record LiteralPattern(Expression Literal);
```

Com Q3 (variantes sempre qualificadas), não há ambiguidade: `x` é sempre um
binding novo e `Color.Red` é sempre uma variante. O parser decide sozinho, sem
consultar o escopo.

Sintaxe de tipos:

```csharp
abstract record TypeSyntax;
  sealed record NamedTypeSyntax(string Name, ImmutableArray<GenericArgumentSyntax> Arguments);
  sealed record ArrayTypeSyntax(TypeSyntax Element);
  sealed record FunctionTypeSyntax(ImmutableArray<TypeSyntax> Parameters, TypeSyntax Return);

abstract record GenericArgumentSyntax;
  sealed record TypeArgumentSyntax(TypeSyntax Type);
  sealed record ConstArgumentSyntax(Expression Value);
  sealed record AmbiguousArgumentSyntax(string Name);   // `Int` ou `N` — resolvido no checker

abstract record GenericParameterSyntax;
  sealed record TypeParameterSyntax(string Name);              // <T>
  sealed record ConstParameterSyntax(string Name, TypeSyntax Type); // <N: Int>  [Q1]
```

### 2.2 Core AST — `Lapis.Ast.Core`

Nós são `sealed class` imutáveis com identidade de referência e um `int NodeId`
único (atribuído por um contador por-programa). O checker e o PE anexam
informação por `NodeId` sem inflar os nós.

```csharp
abstract class CoreExpr { public int NodeId { get; } public SourceSpan Span { get; } }

sealed class CoreLiteral   : CoreExpr { ConstantValue Value; }
sealed class CoreVariable  : CoreExpr { string Name; }
sealed class CoreLet       : CoreExpr { string Name; TypeSyntaxRef? Annotation; CoreExpr Value; CoreExpr Body; bool IsSynthetic; }
sealed class CoreLambda    : CoreExpr { ImmutableArray<CoreGenericParam> GenericParameters;
                                        ImmutableArray<CoreParam> Parameters;
                                        LapisTypeSyntax ReturnType; CoreExpr Body; }
sealed class CoreCall      : CoreExpr { CoreExpr Callee; ImmutableArray<CoreGenericArg> GenericArguments;
                                        ImmutableArray<CoreExpr> Arguments; }
sealed class CoreReturn    : CoreExpr { CoreExpr? Value; }
sealed class CoreIf        : CoreExpr { CoreExpr Condition; CoreExpr Then; CoreExpr Else; }
sealed class CoreBinary    : CoreExpr { BinaryOperator Op; CoreExpr Left; CoreExpr Right; }
sealed class CoreUnary     : CoreExpr { UnaryOperator Op; CoreExpr Operand; }
sealed class CoreArray     : CoreExpr { ImmutableArray<CoreExpr> Elements; }
sealed class CoreIndex     : CoreExpr { CoreExpr Target; CoreExpr Index; }
sealed class CoreField     : CoreExpr { CoreExpr Target; string Name; }
sealed class CoreConstruct : CoreExpr { CoreExpr TypeRef; ImmutableArray<CoreFieldInit> Fields; }
sealed class CoreMatch     : CoreExpr { CoreExpr Scrutinee; ImmutableArray<CoreArm> Arms; }
sealed class CoreTypeDef   : CoreExpr { ImmutableArray<CoreGenericParam> GenericParameters;
                                        ImmutableArray<CoreFieldDecl> Fields; }
sealed class CoreEnumDef   : CoreExpr { ImmutableArray<CoreGenericParam> GenericParameters;
                                        ImmutableArray<CoreVariantDecl> Variants; }

sealed class CoreProgram { CoreExpr Body; int NodeCount; }
```

**16 nós.** Em relação à lista sugerida na spec §24:

| Mudança | Motivo |
|---|---|
| `Block` **removido** | `Let(name, value, body)` já sequencia; ver §2.3 abaixo |
| `Field` **adicionado** | necessário para `user.id` e para `IndexError.OutOfBounds` |
| `TypeDef`/`EnumDef` **adicionados** | `type`/`enum` são expressões (spec §14, §15) e precisam existir na Core |
| `AndAlso`/`OrElse` **ausentes** | desugaram para `If` (curto-circuito explícito) |

### 2.3 Por que não há `Block` na Core

A spec §48 já sugere a forma `Let(x, Literal(10), ...)` — com continuação. Um
bloco

```c
{ def x = 10; print(x); x }
```

vira

```text
Let(x, 10,
  Let(_0, Call(print, [x]),      // IsSynthetic = true
    Var(x)))
```

Ganhos: um único nó de escopo; substituição e inlining no PE ficam textuais;
menos casos em todo `switch`. O printer re-achata cadeias de `Let` em blocos ao
imprimir, então a saída de `lapis desugar` continua legível.

### 2.4 Valores constantes — `Lapis.Ast.ConstantValue`

Compartilhado entre literais da Core, argumentos const-genéricos e o
`StaticEnvironment` do PE. Puro dado, sem dependência de runtime.

```csharp
abstract record ConstantValue;
  sealed record ConstInt(long Value);
  sealed record ConstFloat(double Value);
  sealed record ConstBool(bool Value);
  sealed record ConstStr(string Value);
  sealed record ConstUnit;
  sealed record ConstType(LapisType Type);          // `Int` como argumento genérico
  sealed record ConstFunction(CoreLambda Lambda);   // fn() Int { return 1; } como arg genérico
```

### 2.5 Modelo semântico de tipos — `Lapis.Ast.Types`

```csharp
abstract record LapisType;
  sealed record PrimitiveType(PrimitiveKind Kind);   // Int, Float, Bool, Str, Void
  sealed record NeverType;                            // bottom: tipo de `return` e de match vazio
  sealed record ArrayType(LapisType Element);
  sealed record FunctionType(ImmutableArray<LapisType> Parameters, LapisType Return);
  sealed record NamedType(TypeDefinition Definition, ImmutableArray<SemanticGenericArg> Arguments);
  sealed record TypeParameterType(string Name, int Ordinal);
  sealed record MetaType(LapisType Represented);      // o tipo de uma expressão que É um tipo
  sealed record ErrorType;                            // propagação de erro sem cascata

abstract record SemanticGenericArg;
  sealed record TypeArg(LapisType Type);
  sealed record ConstArg(ConstantValue Value);

sealed record TypeDefinition(
    string Name,
    TypeDefinitionKind Kind,                          // Struct | Enum
    ImmutableArray<GenericParameterInfo> GenericParameters,
    ImmutableArray<FieldInfo> Fields,                 // Struct
    ImmutableArray<VariantInfo> Variants,             // Enum
    SourceSpan Span);
```

Notas:

- `NeverType` é interno (não escrevível em código). É o tipo de `return e`, o que
  permite `match r { Ok(v) => return v, Err(e) => return f }` sem regra especial:
  `Never` é subtipo de tudo na única relação de subtipagem da linguagem.
- `MetaType` existe porque tipos são valores de primeira classe: em
  `def Box = type<T> { ... };`, `Box` tem tipo `MetaType(...)`.
- `ErrorType` absorve operações: qualquer operação com `ErrorType` produz
  `ErrorType` sem emitir novo diagnóstico. Evita cascatas.
- `LapisType` é `record` ⇒ igualdade estrutural ⇒ comparação de tipos é `==`.
  `TypeDefinition` é comparado por identidade nominal (`Name` + `Span`), não
  estruturalmente: dois `type { id: Int }` distintos são tipos distintos.

### 2.6 Typed Core AST — `Lapis.Ast.Typed`

Não há um segundo conjunto de nós. O checker devolve:

```csharp
sealed record TypedProgram(
    CoreProgram Program,
    ImmutableDictionary<int, LapisType> NodeTypes,          // NodeId → tipo
    ImmutableDictionary<int, Resolution> Resolutions);      // NodeId → decisão do checker

abstract record Resolution;
  sealed record VariableResolution(BindingId Binding);
  sealed record CallResolution(ImmutableArray<SemanticGenericArg> InferredArguments, FunctionType Instantiated);
  sealed record FieldResolution(int FieldIndex, LapisType Owner);
  sealed record VariantResolution(TypeDefinition Enum, int VariantIndex);
  sealed record PatternResolution(ImmutableArray<BindingId> Bindings);
```

Motivo: um segundo conjunto de nós duplicaria 16 classes e todo visitor, sem
ganho. O evaluator consulta `Resolutions` onde precisa (variantes, campos), e a
maior parte do tempo nem consulta.

### 2.7 Visitors

```csharp
abstract class CoreVisitor<TResult> { ... }                    // travessia com retorno
abstract class CoreRewriter { public virtual CoreExpr Visit(CoreExpr e); }  // usado pelo PE
```

`CoreRewriter` preserva identidade quando nenhum filho muda (evita realocar a
árvore inteira a cada passe do PE) — importante para os testes de "PE é idempotente".

### 2.8 Printers

| Printer | Saída | Uso |
|---|---|---|
| `SurfaceSExprPrinter` | `(def add (fn ((a Int) (b Int)) Int (return (+ a b))))` | `lapis ast`, snapshots de parser |
| `CoreSExprPrinter` | idem para a Core, com `NodeId` opcional | `lapis desugar`, snapshots de desugar |
| `CoreSourcePrinter` | código `.ls` válido e re-parseável | `lapis pe`, testes de round-trip |

`CoreSourcePrinter` é requisito, não conveniência: ele habilita a propriedade
`parse(desugar⁻¹(print(core)))` usada nos testes do PE (plano 12) e produz a
saída de pesquisa da spec §56.

---

## Decisões de design

**Surface = `record`, Core = `class`.** Surface AST é comparado estruturalmente
em testes de parser — `record` dá isso de graça. Core AST precisa de identidade
estável (`NodeId`) para as tabelas do checker e para o tracing do PE — o que
brigaria com a igualdade estrutural de `record`. Comparação estrutural de Core,
quando necessária, passa pelo `CoreSExprPrinter`.

**Tipos moram em `Lapis.Ast`, não em `Lapis.TypeChecker`.** Runtime e PE precisam
falar de tipos (`Unknown(Type)` na spec §39) sem depender do checker.

**Sem nó de parênteses.** A precedência já está na forma da árvore; o printer
reintroduz parênteses conforme necessário.

---

## Testes necessários

Ficam em `tests/Lapis.Parser.Tests` (Surface) e `tests/Lapis.Desugar.Tests`
(Core), já que `Lapis.Ast` não tem projeto de teste próprio.

### Construção e invariantes

| Teste | Asserção |
|---|---|
| `CoreNodes_HaveUniqueNodeIds` | construir programa com 100 nós ⇒ 100 ids distintos |
| `CoreNodes_AreImmutable` | reflexão: nenhuma propriedade pública com setter |
| `SurfaceNodes_HaveStructuralEquality` | dois `IntLiteral(1)` são iguais |
| `AllCoreNodes_CarrySpan` | reflexão sobre subclasses de `CoreExpr` |

### Modelo de tipos

| Teste | Asserção |
|---|---|
| `PrimitiveTypes_AreSingletons` | `PrimitiveType.Int == PrimitiveType.Int` |
| `ArrayType_EqualityIsStructural` | `Int[] == Int[]`, `Int[] != Str[]` |
| `FunctionType_EqualityConsidersArity` | `fn(Int) Int != fn(Int,Int) Int` |
| `NamedType_EqualityIsNominal` | dois `type { id: Int }` distintos ⇒ tipos distintos |
| `NamedType_SameDefinition_DifferentArgs_AreDistinct` | `Box<Int> != Box<Str>` |
| `ConstArg_Equality` | `FixedArray<Int,3> != FixedArray<Int,4>` |
| `ErrorType_AbsorbsInOperations` | helper de combinação devolve `ErrorType` |

### Printers

| Teste | Asserção |
|---|---|
| `SurfacePrinter_Snapshot_*` | um snapshot por família de nó (Verify) |
| `CorePrinter_FlattensLetChains` | 3 `Let` aninhados ⇒ um bloco de 3 linhas |
| `CorePrinter_SyntheticLet_PrintsAsStatement` | `Let(_0, e, body)` imprime `e;` |
| `CoreSourcePrinter_Roundtrip` | para cada `examples/*.ls`: `desugar(parse(print(desugar(parse(src)))))` produz a mesma S-expression |
| `CoreSourcePrinter_Reparenthesizes` | `(a+b)*c` imprime com parênteses; `a+b*c` não |
| `Printer_IsCultureInvariant` | com `pt-BR` ativo, `Float 3.14` imprime `3.14` |

### Visitors

| Teste | Asserção |
|---|---|
| `CoreRewriter_Identity_PreservesReference` | rewriter que não muda nada devolve a **mesma** instância raiz |
| `CoreRewriter_ChangedChild_RebuildsSpine` | trocar uma folha reconstrói só o caminho até a raiz |
| `CoreVisitor_VisitsEveryNode` | contador de visitas == `NodeCount` |

---

## Critérios de conclusão

- [ ] Todos os nós Surface e Core definidos, imutáveis, com span.
- [ ] `LapisType` completo com igualdade testada.
- [ ] Três printers funcionando, com round-trip verde para `examples/hello.ls`.
- [ ] `CoreVisitor` e `CoreRewriter` com testes de travessia.
- [ ] `Lapis.Ast` referencia apenas `Lapis.Diagnostics` (teste de arquitetura).

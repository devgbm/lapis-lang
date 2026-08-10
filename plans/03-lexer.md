# 03 — Lexer

**Milestone:** M1 · **Depende de:** 00, 01, 02 · **Projeto:** `Lapis.Lexer`

Corresponde à Etapa 2 da spec (§44).

---

## Objetivo

Transformar `SourceText` em uma sequência de tokens com posição de origem,
reportando erros léxicos como diagnósticos em vez de exceções.

---

## Escopo

**Entra:** todos os tokens da spec §44 + extensões Q4, comentários, trivia,
tratamento de fim de arquivo, recuperação de erro.

**Fica de fora:** qualquer decisão sintática. O lexer não sabe o que é `def`,
apenas que `def` é uma palavra-chave. Em particular, **não** trata `<`/`>` de
generics de forma especial — isso é problema do parser (plano 04).

---

## O que será construído

### 3.1 `TokenKind`

Da spec §44, mais o que é necessário na prática:

```csharp
enum TokenKind
{
    // literais
    Identifier, IntegerLiteral, FloatLiteral, StringLiteral,

    // palavras-chave
    DefKeyword, FnKeyword, TypeKeyword, EnumKeyword, ReturnKeyword,
    TrueKeyword, FalseKeyword, IfKeyword, ElseKeyword, MatchKeyword,

    // delimitadores
    OpenParen, CloseParen, OpenBrace, CloseBrace, OpenBracket, CloseBracket,

    // pontuação
    Colon, Comma, Semicolon, Equals, Dot, Underscore, FatArrow,   // . _ =>

    // operadores
    Plus, Minus, Star, Slash,
    EqualsEquals, BangEquals, Less, Greater, LessEquals, GreaterEquals,
    Bang, AmpersandAmpersand, PipePipe,      // [extensão Q4]

    // controle
    EndOfFile, Bad
}
```

Adições em relação à lista literal da spec §44, todas justificadas:

| Token | Motivo |
|---|---|
| `Dot` (`.`) | `IndexError.OutOfBounds`, `user.id` (spec §17, §21) |
| `FatArrow` (`=>`) | braços de `match` (spec §22) |
| `Underscore` (`_`) | padrão coringa; lexado como token próprio apenas quando isolado — `_x` continua sendo `Identifier` |
| `Bang`, `AmpersandAmpersand`, `PipePipe` | Q4: sem eles, `Bool` só serve para `if` |
| `Bad` | recuperação de erro |

**Não** existem `>>`, `<<`, `->`, `::`. A ausência de `>>` é intencional: elimina
o problema de "fechar dois generics aninhados" (`Box<Box<Int>>` lexa como dois
`Greater`).

### 3.2 `Token`

```csharp
readonly record struct Token(
    TokenKind Kind,
    SourceSpan Span,
    string Text,          // lexema bruto
    object? Value);       // long para Int, double para Float, string decodificada para Str
```

`Text` bruto é preservado para o printer e para mensagens de erro.

### 3.3 `Lexer`

```csharp
public sealed class Lexer
{
    public static ImmutableArray<Token> Tokenize(SourceText source, DiagnosticBag diagnostics);
}
```

Scanner de um caractere de lookahead, sem regex (previsibilidade e spans exatos).

Regras:

- **Espaço em branco** (` `, `\t`, `\r`, `\n`) é descartado; a contagem de linhas
  vem do `SourceText`, não do lexer.
- **Comentários**: `// linha` e `/* bloco */`. Blocos **não** aninham (regra
  simples e previsível); `/*` sem fechamento ⇒ `LAP0005`.
- **Identificadores/palavras-chave**: `[A-Za-z_][A-Za-z0-9_]*` (spec §7); uma
  tabela decide se é palavra-chave. `_` sozinho ⇒ `Underscore`.
- **Inteiros**: `[0-9]+`. Overflow de `long` ⇒ `LAP0002`, token vale 0 e o
  parsing continua. Sem `0x`/`0b`/separador `_` na 0.2.
- **Floats**: `[0-9]+ '.' [0-9]+`. O ponto **exige** dígito à direita, senão
  `1.` colidiria com acesso a membro. `1.foo` lexa como `1` `.` `foo`.
  Sem notação exponencial na 0.2. Parsing com `InvariantCulture`.
- **Strings**: aspas duplas, escapes `\" \\ \n \t \r \0`. Escape desconhecido ⇒
  `LAP0003`. String não terminada até fim de linha ⇒ `LAP0004`, token fecha no
  fim da linha. Sem interpolação, sem strings multilinha, sem raw strings.
- **Literal negativo**: `-10` é `Minus` + `IntegerLiteral(10)`. A spec §6 lista
  `-10` como literal, mas tratar isso no lexer quebraria `a -10`. O parser
  produz `Unary(Negate, 10)`, e o desugar dobra para `Literal(-10)` — o resultado
  observável é idêntico.
- **Caractere desconhecido** ⇒ `LAP0001`, emite `Bad` e **avança um caractere**
  (garantia de progresso; sem loops infinitos).
- Sempre emite `EndOfFile` no fim, com span de comprimento 0.

### 3.4 `TokenStream` (consumido pelo parser)

```csharp
public sealed class TokenStream
{
    public Token Current { get; }
    public Token Peek(int offset);
    public Token Advance();
    public int Mark();              // para backtracking do parser (Q5)
    public void Reset(int mark);
}
```

`Mark`/`Reset` existem por causa da desambiguação de generics do plano 04. O
custo é zero: os tokens já estão num `ImmutableArray`, `Mark` é um índice.

---

## Decisões de design

**Tokenização completa e ansiosa (eager)**, não sob demanda. O arquivo é o
programa (spec §3) e é pequeno; ter o array completo torna `Mark`/`Reset`
trivial e os testes determinísticos.

**Sem trivia anexada aos tokens.** Não há formatador nem IDE na 0.2; guardar
comentários só complicaria. Se um formatador for necessário depois, os
comentários voltam como um array lateral indexado por offset.

**Lexer nunca lança.** Todo defeito vira diagnóstico + token `Bad` + progresso
garantido. Um `.ls` de bytes aleatórios deve tokenizar sem exceção.

---

## Testes necessários

### Tokens individuais (`TokenKindTests`)

| Categoria | Casos |
|---|---|
| Palavras-chave | uma asserção por keyword: `def`, `fn`, `type`, `enum`, `return`, `true`, `false`, `if`, `else`, `match` |
| Quase-palavras-chave | `define`, `fnx`, `returns`, `type_`, `_def` ⇒ `Identifier` |
| Delimitadores | `( ) { } [ ]` |
| Pontuação | `: , ; = . _ =>` |
| Operadores 1 char | `+ - * / < > !` |
| Operadores 2 chars | `== != <= >= && \|\|` |
| Maximal munch | `==` é um token, não dois `=`; `<=` não é `<` + `=`; `=>` não é `=` + `>` |
| `>>` | `Box<Box<Int>>` ⇒ ... `Greater`, `Greater` (dois tokens) |

### Literais

| Teste | Entrada | Esperado |
|---|---|---|
| `Int_Zero` | `0` | `IntegerLiteral(0L)` |
| `Int_Large` | `9223372036854775807` | valor exato |
| `Int_Overflow` | `9223372036854775808` | `LAP0002`, valor 0, sem exceção |
| `Float_Simple` | `3.14` | `FloatLiteral(3.14)` |
| `Float_Negative_IsTwoTokens` | `-0.5` | `Minus`, `FloatLiteral(0.5)` |
| `Float_RequiresDigitAfterDot` | `1.foo` | `IntegerLiteral(1)`, `Dot`, `Identifier` |
| `Float_CultureInvariant` | `3.14` sob `pt-BR` | `3.14d` |
| `Str_Simple` | `"hello world"` | valor sem aspas |
| `Str_Escapes` | `"a\nb\t\"c\\"` | decodificado corretamente |
| `Str_UnknownEscape` | `"a\q"` | `LAP0003` |
| `Str_Unterminated` | `"abc⏎` | `LAP0004`, span termina no fim da linha |
| `Str_Empty` | `""` | valor `""` |

### Comentários e espaço

| Teste | Asserção |
|---|---|
| `LineComment_IsSkipped` | `def x = 1; // nota` ⇒ nenhum token de comentário |
| `LineComment_AtEof_NoNewline` | não gera `Bad` |
| `BlockComment_IsSkipped` | `/* a */ def` ⇒ começa em `def` |
| `BlockComment_Unterminated` | `LAP0005` |
| `BlockComment_DoesNotNest` | `/* /* */ x` ⇒ `x` é identificador (fecha no primeiro `*/`) |
| `Newlines_DoNotProduceTokens` | sem ASI, sem token de fim de linha |

### Posições de origem (spec §44)

| Teste | Asserção |
|---|---|
| `Span_OffsetAndLength_Exact` | para `def x = 10;`, o span de `10` é `(8,2)` |
| `Position_LineColumn_AreOneBased` | primeira linha/coluna == 1 |
| `Position_AfterCrLf` | `\r\n` conta como uma quebra de linha |
| `Position_AfterLf` | idem para `\n` |
| `EofToken_HasZeroLengthSpanAtEnd` | span == `(len, 0)` |
| `EveryToken_SpanTextMatchesSource` | property: `source[t.Span] == t.Text` para todo token |

### Erros e robustez

| Teste | Asserção |
|---|---|
| `UnknownChar_ProducesBadAndProgresses` | `def x = @ ;` ⇒ um `LAP0001`, tokenização completa |
| `MultipleErrors_AllReported` | 3 caracteres inválidos ⇒ 3 diagnósticos |
| `Lexer_NeverThrows` | property (FsCheck): string arbitrária ⇒ sem exceção, sempre termina em `EndOfFile` |
| `Lexer_AlwaysProgresses` | property: `tokens.Count <= source.Length + 1` |

### Programas completos (snapshot)

| Teste | Fonte |
|---|---|
| `Tokenize_HelloExample` | `examples/hello.ls` da spec §34 — snapshot da lista de tokens |
| `Tokenize_GenericsExample` | `SomeType<"value", 1, true, Int, fn() Int { return 1; }>` |
| `Tokenize_MatchExample` | o `match` da spec §22 |
| `Tokenize_EmptyFile` | apenas `EndOfFile` |

---

## Critérios de conclusão

- [ ] Todos os `TokenKind` produzidos e testados individualmente.
- [ ] Snapshot de `examples/hello.ls` estável.
- [ ] Nenhum caminho lança exceção (property test verde com 10k casos).
- [ ] Spans exatos verificados pela property `source[span] == text`.
- [ ] `LAP0001`–`LAP0005` implementados conforme Apêndice B.

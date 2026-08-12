// Prelude da LapisLang.
//
// Escrito na própria linguagem (spec §58.2): o runtime não tem implementação
// especial de `Result` — ele só sabe construir e manipular valores de enum
// (spec §16). Este arquivo passa pelo mesmo pipeline que o programa do usuário,
// o que o torna um teste de fumaça permanente do compilador.

def Result = enum<T, E> {
    Ok(T),
    Err(E)
};

// Falha de `contextGet` (spec de macros §8.3). Chave ausente é falha esperada, e
// a spec §30 é categórica: falha esperada aparece no tipo, não em aborto.
def ContextError = enum {
    Missing
};

// O retorno de uma indexação que pode falhar (Q31). Era
// `Result<T, IndexError>` até o M12: `IndexError.OutOfBounds` nunca carregou
// informação — um enum de uma variante cujo significado é "falhou" —, e `Result`
// existe para o erro que **diz** alguma coisa.
def Option = enum<T> {
    Some(T),
    None
};

// Reflection (spec de macros §11, plano 19). Escritos aqui, e não em C#, pelo
// princípio §58.2: não existe sistema de metadados paralelo. Um `TypeInfo` é um
// struct comum — e portanto imutável por construção, sem precisar de regra
// própria, porque a linguagem não tem mutação (§8).

def TypeKind = enum {
    Struct,
    Enum
};

def FieldInfo = type {
    name: Str;
    typeName: Str;
};

def VariantInfo = type {
    name: Str;
    arity: Int;
    payloadTypeNames: [Str;?];
};

// `typeName` é `Str`, e não um `TypeInfo` aninhado: tipos podem ser recursivos, e
// aninhar não teria fim. Resolver o nome é de quem consome.
//
// Um `TypeInfo` só serve struct e enum: `kind` diz qual é, `fields` fica vazio em
// enums e `variants` em structs. Dois tipos separados obrigariam `reflect` a ter
// resultado dependente do argumento, complicando o checker sem ganho.
def TypeInfo = type {
    name: Str;
    kind: TypeKind;
    typeParameterNames: [Str;?];
    fields: [FieldInfo;?];
    variants: [VariantInfo;?];
};

// ---------------------------------------------------------------- controle
//
// As construções de controle que a LapisLang **não tem**, escritas na própria
// linguagem sobre `if` e `loop` (spec de macros §12, plano 20; base trocada de
// `goto`/`label` para `loop`/`break` no plano 26 — Q32).
//
// É a demonstração mais forte da tese do sistema de macros: um laço, que em
// qualquer outra linguagem é trabalho de compilador, aqui é uma declaração de
// biblioteca. Nada foi **retirado** do compilador para isso — `if` e `match`
// continuam onde estavam —, então não há como um `@while` quebrado regredir um
// programa que já funcionava.
//
// O rótulo que `@while` introduziria não é mais necessário — `loop` sem rótulo
// já é o laço mais próximo — mas a higiene continua valendo para tudo que a
// macro liga (`def`, `var`): dois `@while` no mesmo bloco não colidem.
//
// O corpo vai entre chaves — `{ body; }`, e não `body;` — de propósito. Uma
// captura de bloco escrita nua é **colada** no lugar, e o que ela declara
// escaparia para fora; entre chaves ela é um escopo, como o corpo de um `if`.
// Quem escreve uma macro escolhe entre as duas formas.

macro unless
    match Expression:condition Block:body
    expand { if !condition { body; } };

// A razão de `loop` poder ter progresso (antes, de `goto` poder saltar para
// trás — plano 16), e o primeiro programa LapisLang capaz de não terminar —
// daí o orçamento de iterações e o `LAP0303`.
//
// Só passou a iterar de verdade com `var` (Q25): antes da mutação nada mudava
// entre as voltas, então `condition` valia o mesmo sempre e o laço ou não rodava
// ou não parava.
//
// O corpo de um `loop` é um escopo léxico só, reavaliado a cada volta. Um `var`
// declarado **dentro** dele seria recriado a cada passagem — por isso as
// declarações que o laço lê ficam antes da invocação, e não dentro do corpo:
//
//     var i = 0;
//     @while i < 3 { i = i + 1; }
//
// A posição de `condition` — ramo verdadeiro de um `if` — não é acidente: é a
// posição 1 da regra de escopo do `is` (plano 25 §25.3). `@while e is Some(v)
// { usa(v); }` liga `v` dentro do corpo de graça, sem nada especial aqui nem
// em `is`.
macro while
    match Expression:condition Block:body
    expand { loop { if condition { body; } else { break; } } };

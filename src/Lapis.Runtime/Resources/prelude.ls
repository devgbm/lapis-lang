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

def IndexError = enum {
    OutOfBounds
};

// Falha de `contextGet` (spec de macros §8.3). Chave ausente é falha esperada, e
// a spec §30 é categórica: falha esperada aparece no tipo, não em aborto.
def ContextError = enum {
    Missing
};

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
    payloadTypeNames: Str[];
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
    typeParameterNames: Str[];
    fields: FieldInfo[];
    variants: VariantInfo[];
};

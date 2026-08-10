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

def Option = enum<T> {
    Some(T),
    None
};

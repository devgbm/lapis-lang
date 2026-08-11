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

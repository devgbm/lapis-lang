// `@unless` construída sobre `if` — a macro que o M6 existia para viabilizar
// (spec de macros §12.1; reescrita sobre `if` no plano 26 — Q32).
//
// A macro não acrescenta nada ao compilador: é sintaxe que a própria linguagem
// define, e some antes do desugar.
// expect: output
// executou
// fim
// ---
macro unless
    match Expression:condition Block:body
    expand {
        if !condition {
            body;
        }
    };

def pular = false;

@unless pular {
    print("executou");
}

@unless true {
    print("nao executa");
}

print("fim");

// `@unless` construída sobre `goto`/`label` — a macro que o M6 existia para
// viabilizar (spec de macros §12.1).
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
        goto done if condition;
        body;
        label done;
    };

def pular = false;

@unless pular {
    print("executou");
}

@unless true {
    print("nao executa");
}

print("fim");

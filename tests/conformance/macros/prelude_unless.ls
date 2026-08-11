// `@unless` também vem do prelude, e duas invocações no mesmo bloco não colidem:
// o rótulo que a macro introduz é higienizado.
// expect: output
// executou
// também
// fim
// ---
def pular = false;

@unless pular {
    print("executou");
}

@unless pular {
    print("também");
}

@unless true {
    print("nao executa");
}

print("fim");

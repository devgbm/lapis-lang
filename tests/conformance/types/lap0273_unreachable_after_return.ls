// LAP0273 é aviso: o código depois do `return` nunca executa.
// expect: warning LAP0273
// expect: output
// 1
// ---
def f = fn() Int {
    return 1;
    print("nunca");
};

print(f());

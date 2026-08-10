// Sombrear num bloco interno é permitido; redefinir no mesmo bloco não é.
// expect: output
// interno
// externo
// ---
def x = "externo";

def f = fn() Void {
    def x = "interno";
    print(x);
};

f();
print(x);

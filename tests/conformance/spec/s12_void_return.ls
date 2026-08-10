// spec §12 — função `Void` pode usar `return;` ou cair no fim do corpo.
// expect: output
// antes
// ---
def cedo = fn(sair: Bool) Void {
    if sair {
        return;
    }

    print("depois");
};

def implicito = fn() Void {
    print("antes");
};

implicito();
cedo(true);

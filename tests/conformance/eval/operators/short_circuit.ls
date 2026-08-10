// `&&` e `||` têm curto-circuito: o lado direito não é avaliado quando o
// resultado já está decidido. `print` devolve Void, então o efeito é observável.
// expect: output
// esquerda
// false
// ---
def falso = fn() Bool {
    print("esquerda");
    return false;
};

def nunca = fn() Bool {
    print("direita");
    return true;
};

print(falso() && nunca());

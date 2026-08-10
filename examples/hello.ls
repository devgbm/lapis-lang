// Exemplo da spec §34 — alvo do primeiro milestone (spec §60).
// Saída esperada: 30

def add = fn(a: Int, b: Int) Int {
    return a + b;
};

def main = fn() Void {
    def result = add(10, 20);

    print(result);
};

main();

// spec §32 — a closure captura o ambiente do ponto de definição.
// expect: output
// 15
// ---
def makeAdder = fn(n: Int) fn(Int) Int {
    return fn(x: Int) Int {
        return x + n;
    };
};

def add5 = makeAdder(5);

print(add5(10));

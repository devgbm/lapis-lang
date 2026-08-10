// spec §3 — expressões top-level são avaliadas na ordem em que aparecem.
// expect: output
// 30
// ---
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

def result = add(10, 20);

print(result);

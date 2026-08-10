// spec §8 — não há recursão na 0.2 (Q8): o nome não é visível na expressão que
// o define.
// expect: error LAP0201
// ---
def fact = fn(n: Int) Int {
    if n == 0 {
        return 1;
    }

    return n * fact(n - 1);
};

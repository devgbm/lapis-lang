// spec §8, revisada pela Q34: uma função enxerga a si mesma.
//
// Substitui `s08_no_recursion.ls`, que afirmava o contrário — a Q8 proibia
// recursão para manter trivial a terminação do partial evaluator, e a Q33
// adiou o PE.
// expect: output
// 120
// 55
// ---
def fatorial = fn(n: Int) Int {
    if n <= 1 { return 1; }

    return n * fatorial(n - 1);
};

def fib = fn(n: Int) Int {
    if n < 2 { return n; }

    return fib(n - 1) + fib(n - 2);
};

print(fatorial(5));
print(fib(10));

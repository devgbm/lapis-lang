// `return` tem tipo Never (Q13) e cabe em qualquer posição — inclusive num ramo
// de `if` cujo outro ramo produz Int.
// expect: output
// 2
// ---
def f = fn(c: Bool) Int {
    def x = if c { return 0; } else { 2 };
    return x;
};

print(f(false));

// LAP0294 — um valor que só existe em execução não serve de argumento const (Q18).
// expect: error LAP0294
// ---
def Fixed = type<T, N: Int> { values: T[]; };

def f = fn(n: Int) Void {
    def g = fn(a: Fixed<Int, n>) Void { };
};

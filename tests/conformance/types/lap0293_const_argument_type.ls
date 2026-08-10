// expect: error LAP0293
// ---
def Fixed = type<T, N: Int> { values: T[]; };
def f = fn(a: Fixed<Int, "texto">) Void { };

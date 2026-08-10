// expect: error LAP0292
// ---
def Fixed = type<T, N: Int> { values: T[]; };
def f = fn(a: Fixed<Int, Str>) Void { };

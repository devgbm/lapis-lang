// expect: error LAP0291
// ---
def Fixed = type<T, N: Int> { values: [T;?]; };
def f = fn(a: Fixed<3, 3>) Void { };

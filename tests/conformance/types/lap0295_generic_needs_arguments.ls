// expect: error LAP0295
// ---
def Box = type<T> { value: T; };
def f = fn(b: Box) Void { };

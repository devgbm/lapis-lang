// expect: error LAP0116
// ---
def Box = type<T> { value: T; };
def b: Box<> = x;

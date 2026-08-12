// `e is Some` sem ligação é um `Bool` como outro qualquer.
// expect: output
// true
// false
// ---
def a = Option<Int>.Some(1);
def b: Option<Int> = Option<Int>.None;

print(a is Some);
print(b is Some);

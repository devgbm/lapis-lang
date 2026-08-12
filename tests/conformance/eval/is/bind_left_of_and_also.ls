// A composição do §25.3: `e is Some(v) && v == 1` liga `v` no operando
// direito e adiante.
// expect: output
// true
// false
// ---
def a = Option<Int>.Some(1);
def b = Option<Int>.Some(2);

print(a is Some(v) && v == 1);
print(b is Some(v) && v == 1);

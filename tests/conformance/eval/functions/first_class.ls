// expect: output
// 6
// 50
// 5
// ---
def apply = fn(f: fn(Int) Int, value: Int) Int { return f(value); };
def increment = fn(x: Int) Int { return x + 1; };
def double = fn(x: Int) Int { return x * 2; };

print(apply(increment, 5));
print(apply(double, 25));
print(apply(fn(x: Int) Int { return x; }, 5));

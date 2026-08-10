// spec §11 — funções são valores de primeira classe.
// expect: output
// 2
// 0
// ---
def increment = fn(x: Int) Int { return x + 1; };
def decrement = fn(x: Int) Int { return x - 1; };

def operations = [increment, decrement];

def apply = fn(f: fn(Int) Int, v: Int) Int { return f(v); };

print(apply(increment, 1));
print(apply(decrement, 1));

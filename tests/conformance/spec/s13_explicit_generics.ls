// spec §13 — argumentos genéricos são sempre explícitos (Q7).
// expect: output
// 10
// hello
// ---
def identity = fn<T>(value: T) T {
    return value;
};

print(identity<Int>(10));
print(identity<Str>("hello"));

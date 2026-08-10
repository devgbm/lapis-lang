// Bindings são imutáveis, então a captura não tem como mudar depois.
// expect: output
// 1
// ---
def n = 1;
def f = fn() Int { return n; };
def n2 = 2;

print(f());

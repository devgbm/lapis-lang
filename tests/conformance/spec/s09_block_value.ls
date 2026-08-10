// spec §9 — um bloco vale a última expressão, quando não termina em `;`.
// expect: output
// 11
// ---
def x = { def y = 10; y + 1 };

print(x);

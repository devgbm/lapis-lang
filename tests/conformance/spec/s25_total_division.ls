// spec §25 — a divisão inteira é total (Q9): dividir por zero produz o maior
// `Int`, e não há caminho de aborto.
// expect: output
// 9223372036854775807
// 5
// ---
print(1 / 0);
print(10 / 2);

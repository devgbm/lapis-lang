// A divisão inteira é total (Q9): por zero produz o maior Int qualquer que seja
// o numerador, e não há caminho de aborto. Foi essa decisão que tirou o último
// `Abort` do evaluator e deu ao partial evaluator aritmética sempre pura.
// expect: output
// 9223372036854775807
// 9223372036854775807
// 9223372036854775807
// 3
// ---
print(7 / 0);
print(-7 / 0);
print(0 / 0);
print(7 / 2);

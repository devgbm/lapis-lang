// Encontrado pela propriedade `Printer_Roundtrips` do plano 11 §11.6.
//
// `ConstFloat` imprimia via `ToString("R")`, que cai em notação científica para
// magnitudes extremas: `100000000000000000000.0` saía como `1E+20`. A gramática
// de float da 0.2 é `dígitos "." dígitos` (spec §6) e não tem expoente, então a
// saída deixava de ser reparseável — e o round-trip é o que permite ao
// `lapis pe` emitir um residual executável.
// expect: output
// 100000000000000000000.0
// 0.00001234
// -1500.0
// ---
print(100000000000000000000.0);
print(0.00001234);
print(-1500.0);

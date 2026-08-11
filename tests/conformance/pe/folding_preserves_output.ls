// Constant folding não muda o que o programa imprime — é a propriedade que o PE
// existe para preservar, vista do lado do usuário.
// expect: output
// 30
// 21
// true
// ab
// ---
print(10 + 20);
print((1 + 2) * (3 + 4));
print(1 < 2);
print("a" + "b");

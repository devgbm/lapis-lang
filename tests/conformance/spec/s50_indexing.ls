// Indexação com tamanho e índice conhecidos é **total** (plano 24): o tipo é o do
// elemento, sem envelope. É o que o tamanho no tipo compra.
//
// O caso fora de limites saiu daqui: com `[Int;3]` ele é erro de compilação, e
// vive em `lap0244_index_out_of_bounds.ls`.
// expect: output
// 1
// 3
// ---
print(.[1, 2, 3][0]);
print(.[1, 2, 3][2]);

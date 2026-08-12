// A repetição segue a regra de sempre: um `var` sem anotação alarga o tamanho
// para `?`, porque a reatribuição pode trazer outro (plano 24 §24.7). Anotado, o
// tamanho fica — e com ele a indexação total num binding mutável.
//
// Não é regra da repetição, é regra da **ligação**: a construção produz `[Int;3]`
// nos dois casos, e quem decide o que fazer com o tamanho é o `var`.
// expect: output
// Option.Some(0)
// 0
// 9
// ---
var alargado = .[Int; 0; 3];
var fixo: [Int;3] = .[Int; 0; 3];

// Sem o tamanho no tipo, indexar embrulha.
print(alargado[0]);

// Com ele, sai o `Int`.
print(fixo[0]);

// E a reatribuição do anotado é checada contra o tamanho.
fixo = .[9, 9, 9];

print(fixo[2]);

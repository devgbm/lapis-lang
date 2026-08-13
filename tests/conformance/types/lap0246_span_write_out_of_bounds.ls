// LAP0246 — escrita fora dos limites com tamanho conhecido.
//
// É **warning**, e não erro, e a assimetria com `LAP0244` (leitura) é
// deliberada (Q36): a escrita não tem efeito, então o programa continua bem
// definido e roda. Uma leitura fora dos limites com tamanho conhecido não tem
// valor a produzir, e por isso é erro.
//
// O aviso sai só onde o compilador **consegue ver** — tamanho no tipo e índice
// constante. Com `[T;?]` não há o que avisar, e é a mesma fronteira em que a
// leitura devolve `Option` em vez de garantir.
// expect: warning LAP0246
// expect: output
// [0, 1, 2]
// ---
var s: [Int;3] = .[0, 1, 2];

s[5] = 9;

print(s);

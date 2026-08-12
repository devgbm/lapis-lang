// Um `if` sem chaves não pode ter outro `if` como corpo direto — é o que evita
// o dangling-else sem regra de precedência (plano 26 §26.9).
// expect: error LAP0527 at 4:9
if true if true print(1); else print(2);

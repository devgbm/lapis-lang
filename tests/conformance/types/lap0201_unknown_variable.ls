// Nome que não existe. Até o M17 este código era exemplificado por acidente,
// pelo caso de recursão que a Q8 recusava (`spec/s08_no_recursion.ls`); com a
// Q34 aquele programa passou a ser válido, e `LAP0201` ganhou caso próprio —
// que é o que ele sempre deveria ter tido, já que "variável não existe" não
// tem nada a ver com recursão.
// expect: error LAP0201 at 7:7
print(inexistente);

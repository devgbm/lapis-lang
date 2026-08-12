// Uma macro do arquivo com o nome de uma do prelude **vence**, sem diagnóstico —
// a mesma regra que já vale para `def Result = ...`. Quem escreve `macro while`
// no seu arquivo quis o seu.
// expect: output
// 42
// ---
macro while
    match Expression:e
    expand { print(e); };

@while 42;

// Com o tamanho no tipo, um índice literal fora dos limites é erro **de
// compilação** — checado pela mesma maquinaria que rejeita `def a: Str = 1;`
// (plano 24).
//
// Provar `i < n` para um `i` derivado de laço continua sendo trabalho do partial
// evaluator, não do checker.
// expect: error LAP0244 at 10:9
def a = .[1, 2, 3];

print(a[3]);

// O que o partial evaluator ganha com o tamanho no tipo.
//
// Uma indexação total é uma projeção de um valor conhecido: o especializador lê o
// elemento e a expressão inteira some. `length` de um span conhecido vira a
// constante pelo mesmo motivo. Nenhum dos dois precisou de análise de limites — o
// checker já fez a prova, e deixou a decisão registrada.
//
// Rode `lapis pe` neste arquivo: o residual é três `print` de literais.
//
// Onde o tamanho se perde, nada disso vale, e é de propósito: a última linha
// atravessa a especialização intacta porque o envelope `Option` só pode ser
// construído em execução.
// expect: output
// 20
// 3
// Option.Some(20)
// ---
def valores = .[10, 20, 30];

print(valores[1]);
print(valores.length);

var mutavel = .[10, 20, 30];

print(mutavel[1]);

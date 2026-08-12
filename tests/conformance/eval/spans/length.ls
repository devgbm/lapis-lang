// `length` responde nos dois regimes, e é a mesma leitura: o valor sempre carrega
// a quantidade junto do dado (plano 24 §24.6).
//
// O que muda é quem responde. Com o tamanho no tipo, o checker devolve a
// constante e o partial evaluator dobra a linha inteira; sem ele, o span é lido
// em execução. As duas respostas coincidem — é o que esta caso afirma.
// expect: output
// 3
// 3
// 0
// ---
def conhecido = .[1, 2, 3];
var desconhecido = .[1, 2, 3];
def vazio: [Int;0] = .[];

print(conhecido.length);
print(desconhecido.length);
print(vazio.length);

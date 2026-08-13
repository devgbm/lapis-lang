// O caminho de atribuição mistura campo e índice, em qualquer ordem, porque os
// dois são passos do mesmo mecanismo (Q36).
//
// Fora dos limites em qualquer profundidade não escreve **nada** — não é "não
// escreve naquele elemento". A regra é uma só, e a reconstrução aborta inteira.
// expect: output
// Caixa { xs: [1, 20, 3] }
// Caixa { xs: [1, 20, 3] }
// ---
def Caixa = type {
    xs: [Int;?];
};

var c = .Caixa { xs: .[1, 2, 3] };

c.xs[1] = 20;
print(c);

c.xs[99] = 30;
print(c);

// Q29: `[T;N] <: [T;?]` — um span de tamanho conhecido cabe onde se espera
// tamanho desconhecido. `?` não é outro tamanho, é a ausência da informação, e
// esquecer o que se sabia é sempre seguro.
//
// É a regra que faz uma função sobre spans ser escrevível uma vez só. Sem ela,
// `imprime` precisaria de uma versão por tamanho.
//
// Numa direção só: o contrário afirmaria um tamanho que ninguém verificou.
// expect: output
// 3
// 1
// 2
// ---
def tamanho = fn(xs: [Int;?]) Int {
    return xs.length;
};

def tres = .[1, 2, 3];
def um = .[9];
var mutavel = .[1, 2];

print(tamanho(tres));
print(tamanho(um));
print(tamanho(mutavel));

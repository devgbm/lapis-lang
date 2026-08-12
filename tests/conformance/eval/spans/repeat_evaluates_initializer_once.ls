// O inicializador é avaliado **uma vez**, e o mesmo valor ocupa as `n` posições.
//
// O compartilhamento não é observável — não existe escrita em span (Q28) —, mas o
// número de avaliações é: um inicializador que imprime imprime uma vez só. É a
// leitura que faz de `.[Int; 0; 8]` uma construção, e não um laço escondido.
// expect: output
// avaliado
// [7, 7, 7]
// ---
def registra = fn(x: Int) Int {
    print("avaliado");
    return x;
};

def repetido = .[Int; registra(7); 3];

print(repetido);

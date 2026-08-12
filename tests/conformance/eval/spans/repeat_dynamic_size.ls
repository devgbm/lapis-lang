// A quantidade pode só existir em execução — e é metade do motivo de a forma
// existir: `.[Int; 0; n]` com `n` calculado não tem como ser escrito por lista.
//
// Aí o tipo é `[T;?]`, como qualquer span cujo tamanho ninguém sabe: indexar
// devolve `Option`, e `length` é lido do valor. O span carrega a quantidade junto
// do dado (Q29), então ela responde de qualquer jeito.
//
// Quantidade negativa produz span **vazio**, não aborto: é a mesma escolha da
// divisão inteira por zero (Q9) — a operação é total, e o programa segue.
// expect: output
// ["x", "x", "x", "x"]
// 4
// Option.Some("x")
// Option.None
// []
// 0
// ---
var n = 4;

def repetido = .[Str; "x"; n];

print(repetido);
print(repetido.length);
print(repetido[0]);
print(repetido[9]);

def negativo = .[Int; 0; 0 - 1];

print(negativo);
print(negativo.length);

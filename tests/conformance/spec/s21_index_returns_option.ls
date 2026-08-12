// Quando o tamanho **não** está no tipo, indexar devolve `Option<T>`: a falha
// aparece no tipo, e o acesso fora de limites nunca lança (spec §21, revisada
// pelo plano 24).
//
// Deixou de ser `Result<T, IndexError>` (Q31): `IndexError.OutOfBounds` era um
// enum de uma variante cujo significado é "falhou", e `Result` existe para o erro
// que **diz** alguma coisa.
// expect: output
// Option.Some(20)
// Option.None
// ---
var numbers = .[10, 20, 30];

def dentro: Option<Int> = numbers[1];
def fora: Option<Int> = numbers[9];

print(dentro);
print(fora);

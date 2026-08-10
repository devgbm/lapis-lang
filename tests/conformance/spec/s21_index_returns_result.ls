// spec §21 — indexar SEMPRE devolve `Result<T, IndexError>`: a falha aparece no
// tipo, e o acesso fora de limites nunca lança.
// expect: output
// Result.Ok(20)
// ---
def numbers = [10, 20, 30];

def r: Result<Int, IndexError> = numbers[1];

print(r);

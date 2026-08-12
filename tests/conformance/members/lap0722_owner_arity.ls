// O padrão do dono tem de ter um argumento por parâmetro do tipo. Escrever menos
// não é "o resto é curinga": omitir posições faria a declaração significar coisas
// diferentes conforme o tipo mudasse de aridade.
//
// Omitir `<>` **inteiro**, esse sim, é o padrão todo curinga — é o que faz
// `def Par.m` valer para qualquer `Par`, e é o que mantém `def Result.ok`
// funcionando como sempre funcionou.
// expect: error LAP0722 at 11:5
def Par = type<A, B> { a: A; b: B; };

def Par<Int>.primeiro = fn(self) Int { return 0; };

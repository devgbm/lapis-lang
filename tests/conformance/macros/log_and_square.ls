// Os dois exemplos da spec de macros §3 e §6: uma macro em posição de statement,
// outra em posição de expressão.
// expect: output
// 3
// 9
// 16
// ---
macro log
    match Expression:e
    expand { print(e); };

macro square
    match Expression:e
    expand { (e * e) };

@log 1 + 2;

def r = @square 3;
print(r);

// A captura vem inteira: `@square 2 + 2` captura `2 + 2`, não `2`.
print(@square 2 + 2);

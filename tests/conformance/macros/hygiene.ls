// Higiene (spec de macros §9): um identificador introduzido pela macro não
// captura o do programa, e um identificador capturado mantém o contexto léxico
// de quem invocou.
//
// A macro declara `temp`; o programa também. São nomes diferentes — o da macro
// virou `temp@1`, que não é escrevível em fonte.
// expect: output
// meu
// 10
// ---
macro example
    match Block:body
    expand {
        def temp = 10;
        body;
    };

def temp = "meu";

@example { print(temp); }

print(10);

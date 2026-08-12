// Somar um span com laço — o programa que a 0.2 não sabia escrever.
//
// A atribuição é statement, não expressão, então dentro de um braço de `match`
// ela vai num bloco, com `;`.
// expect: output
// 60
// ---
def valores = .[10, 20, 30];
var soma = 0;
var i = 0;

loop {
    match valores[i] {
        Option.Some(v) => { soma = soma + v; },
        Option.None => { print("fora"); }
    }
    i = i + 1;
    if i < 3 { continue; } else { break; }
}

print(soma);

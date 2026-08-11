// Encontrado ao escrever um caso de `var` dentro de braço de `match`.
//
// Um braço cujo corpo não termina onde o parser espera fazia a recuperação
// rebobinar o fluxo para antes do início do statement. `SpanFrom` então montava
// um span com fim antes do início e o parser **lançava** — quebrando a promessa
// de que todo defeito vira diagnóstico (plano 04).
//
// O bug é anterior ao `var`: `match 1 { _ => a b }` derruba igual.
//
// O que este caso trava é "não lança"; os quatro códigos abaixo são a cascata
// que a recuperação produz hoje. Se ela melhorar, o caso falha e é atualizado —
// que é o comportamento certo.
// expect: error LAP0102 at 18:12
// expect: error LAP0108 at 18:12
// expect: error LAP0102 at 19:1
// expect: error LAP0110 at 19:1
match 1 {
    _ => a b
}

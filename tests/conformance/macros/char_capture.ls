// `Char:c` captura um literal de caractere, como `Int:n` captura um inteiro
// (Q35). A categoria entrou junto com o literal: sem ela, uma macro que recebe
// um caractere teria de pedir `Expression:c` e perder a checagem de forma.
//
// A aspa simples não conflita com a pseudo-palavra-chave da Q38 porque as duas
// nunca disputam o mesmo lugar: pseudo-palavra-chave só existe **dentro** do
// `match`, e o que aparece na invocação é sempre valor.
// expect: output
// true
// false
// ---
macro comecaCom
    match Expression:texto Char:esperado
    expand {
        if texto[0] is Some(primeiro) { primeiro == esperado } else { false }
    };

print(@comecaCom "abc" 'a');
print(@comecaCom "abc" 'z');

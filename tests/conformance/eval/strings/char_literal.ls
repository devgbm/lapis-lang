// Literal de `Char` (Q35, revisando o que a A2a tinha deixado de fora).
//
// As aspas simples cabem em `Char` **e** na pseudo-palavra-chave de macro (Q38)
// porque as duas nunca disputam o mesmo lugar: a pseudo-palavra-chave só existe
// dentro do `match` de uma macro. Fora dali, aspa simples é caractere.
//
// Escapa-se a aspa que delimita, e só ela: `'\''` precisa de barra, `'"'` não —
// e o simétrico vale para a string.
// expect: output
// a
// 𝕏
// '
// "
// true
// false
// ---
print('a');
print('𝕏');
print('\'');
print('"');
print('a' == 'a');
print('a' == 'b');

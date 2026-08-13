// LAP0117 — literal de caractere com mais de um ponto de código.
//
// Quem reporta é o **parser**, não a lexer, e isso é de propósito (Q35/Q38): as
// mesmas aspas simples delimitam uma pseudo-palavra-chave dentro do `match` de
// uma macro, onde o conteúdo é um identificador inteiro. A lexer não sabe onde o
// token está; o parser sabe, e cobra a regra só onde ela vale.
// expect: error LAP0117
// expect: error LAP0117
def dois = 'ab';
def vazio = '';

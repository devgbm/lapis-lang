// O que a Q25 destravou: um laço que **avança**.
//
// O corpo de um join roda sempre no ambiente do grupo — o mesmo em toda volta.
// Com bindings imutáveis nada mudava entre iterações e o laço só terminava pelo
// orçamento de saltos. Um `var` declarado antes do rótulo é um slot que
// atravessa as voltas, e é isso que faz a condição de saída chegar.
// expect: output
// 1
// 2
// 3
// fim
// ---
var i = 0;

label repete;
i = i + 1;
print(i);
goto repete if i < 3;

print("fim");

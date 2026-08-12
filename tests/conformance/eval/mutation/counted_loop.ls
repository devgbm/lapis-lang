// O que a Q25 destravou: um laço que **avança**.
//
// O corpo de um `loop` roda sempre no mesmo ambiente léxico, a cada volta. Com
// bindings imutáveis nada mudava entre iterações e o laço só terminava pelo
// orçamento de iterações. Um `var` declarado antes do `loop` é um slot que
// atravessa as voltas, e é isso que faz a condição de saída chegar.
// expect: output
// 1
// 2
// 3
// fim
// ---
var i = 0;

loop {
    i = i + 1;
    print(i);
    if i < 3 { continue; } else { break; }
}

print("fim");

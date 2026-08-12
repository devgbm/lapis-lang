// Um `break` rotulado alcança o laço que o declara por cima de quantos
// aninhamentos for preciso, pulando o que ficaria entre eles.
//
// O laço interno só sai por um `break` que mira `:fora` — nunca cai fora dele
// por conta própria —, então o `print("segundo")` que vem depois dele é
// código genuinamente inalcançável (a mesma leitura de DR sobre um `loop` sem
// `break` que o alcance, aqui por rótulo).
// expect: warning LAP0273 at 16:5
// expect: output
// terceiro
// ---
loop :fora {
    loop {
        break :fora;
    }
    print("segundo");
}
print("terceiro");

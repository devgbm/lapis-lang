// Escrever fora dos limites **não tem efeito** — não aborta, não avisa em
// execução, não deixa o span num estado intermediário (Q36).
//
// É a metade que faltava da Q31. Ler fora dos limites devolve `Option` porque a
// leitura precisa produzir valor e pode não haver um; escrever não produz nada,
// então não há o que envelopar — e atribuição é statement de propósito (Q25),
// sem lugar onde um `Bool` de retorno caberia.
//
// Aqui o tamanho **não** está no tipo (`var` alarga), então nem warning existe:
// o compilador genuinamente não sabe, que é a mesma fronteira em que a leitura
// deixa de garantir.
// expect: output
// [0, 1, 2]
// [0, 1, 2]
// ---
var s = .[0, 1, 2];

s[99] = 7;
print(s);

s[0 - 1] = 7;
print(s);

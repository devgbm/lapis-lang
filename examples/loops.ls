// `var` e laço com progresso (Q25).
//
// Um `def` é definitivo; um `var` pode ser reatribuído. A restrição que faz isso
// caber na linguagem sem virar um buraco: **um `var` não atravessa fronteira de
// função**. Nenhuma closure captura `var`, então não há aliasing — e a pergunta
// "captura por valor ou por referência?" nem chega a ser feita.
//
// É a peça que faltava para `loop` servir de laço de verdade (plano 26 — Q32,
// que substituiu `goto`/`label` por `loop`/`break`/`continue`). O corpo de um
// `loop` roda sempre no mesmo escopo léxico, o mesmo em toda volta; com
// bindings imutáveis nada mudava entre iterações e o laço só parava pelo
// orçamento de iterações (LAP0303). Um `var` declarado antes do `loop` é o slot
// que atravessa as voltas.
//
// Este arquivo escreve os laços **à mão**, com `loop`/`break`/`continue`, para
// mostrar a forma. O `@while` do prelude gera exatamente isto; `examples/
// control.ls` tem a versão que se escreve no dia a dia.
//
// Saída esperada:
//   1
//   2
//   3
//   55
//   60

def valores = .[10, 20, 30];

var i = 0;
var soma = 0;
var n = 1;
var total = 0;
var k = 0;

loop {
    i = i + 1;
    print(i);
    if i < 3 { continue; } else { break; }
}

// Somatório de 1 a 10 — exatamente a forma que `@while` gera por macro.
loop {
    soma = soma + n;
    n = n + 1;
    if n <= 10 { continue; } else { break; }
}

print(soma);

// E percorrendo um span. O índice é um `var`, então não é constante para o
// checker: a indexação devolve `Option`, e o braço de `None` existe mesmo quando
// o laço garante que ele não acontece. Com índice literal, `valores[1]` daria o
// `Int` direto — ver `examples/spans.ls`.
//
// `valores.length` é `3` em compilação, porque o tamanho está no tipo.
loop {
    match valores[k] {
        Option.Some(v) => { total = total + v; },
        Option.None => { print("fora dos limites"); }
    }
    k = k + 1;
    if k < valores.length { continue; } else { break; }
}

print(total);

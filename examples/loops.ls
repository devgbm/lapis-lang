// `var` e laço com progresso (Q25).
//
// Um `def` é definitivo; um `var` pode ser reatribuído. A restrição que faz isso
// caber na linguagem sem virar um buraco: **um `var` não atravessa fronteira de
// função**. Nenhuma closure captura `var`, então não há aliasing — e a pergunta
// "captura por valor ou por referência?" nem chega a ser feita.
//
// É a peça que faltava para `goto` para trás servir de laço. O corpo de um join
// roda sempre no ambiente do grupo, o mesmo em toda volta; com bindings
// imutáveis nada mudava entre iterações e o laço só parava pelo orçamento de
// saltos. Um `var` declarado antes do rótulo é o slot que atravessa as voltas.
//
// Aqui as declarações vêm todas antes do primeiro `label` por clareza, não por
// obrigação: rótulos que não saltam um para o outro formam grupos aninhados, e
// um `var` escrito entre dois laços é visível no segundo. O que continua
// invisível é o que um `goto` **explícito** pode ter pulado — ver
// `tests/conformance/eval/mutation/scope_across_joins_with_jump.ls`.
//
// Este arquivo escreve os laços **à mão**, com `goto` e `label`, para mostrar a
// forma. O `@while` do prelude gera exatamente isto; `examples/control.ls` tem a
// versão que se escreve no dia a dia.
//
// Saída esperada:
//   1
//   2
//   3
//   55
//   60

def valores = [10, 20, 30];

var i = 0;
var soma = 0;
var n = 1;
var total = 0;
var k = 0;

label conta;
i = i + 1;
print(i);
goto conta if i < 3;

// Somatório de 1 a 10 — exatamente a forma que `@while` gera por macro.
label somando;
soma = soma + n;
n = n + 1;
goto somando if n <= 10;

print(soma);

// E percorrendo um array. A indexação continua devolvendo `Result`, então o
// braço de erro existe mesmo quando o laço garante que ele não acontece.
label percorre;
match valores[k] {
    Result.Ok(v) => { total = total + v; },
    Result.Err(e) => { print("fora dos limites"); }
}
k = k + 1;
goto percorre if k < 3;

print(total);

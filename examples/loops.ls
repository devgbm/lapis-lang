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
// Note que TODAS as declarações vêm antes do primeiro `label`. Não é estilo: o
// que é declarado depois de um rótulo vive dentro daquele join, e um join não
// enxerga os bindings de outro — o salto pode ter pulado a declaração. É a mesma
// regra que vale entre o `goto` e o `label`.
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

// Somatório de 1 a 10 — a forma que o `@while` do plano 20 vai gerar por macro.
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

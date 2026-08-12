// Quantidade grande demais termina com `LAP0304` em vez de travar a máquina.
// Mesmo espírito do orçamento de saltos (`LAP0303`): a garantia não é "nunca
// falta memória", é "o programa termina e diz o que houve".
//
// O orçamento é de **execução**, e não de compilação, de propósito: o checker
// rejeitar o caso constante faria o partial evaluator poder transformar um
// programa que compila num que não compila, ao dobrar a quantidade. Nenhuma
// transformação dele pode mudar se um programa é bem tipado.
// expect: error LAP0304
// expect: abort
var enorme = 2000000;

def span = .[Int; 0; enorme];

print(span.length);

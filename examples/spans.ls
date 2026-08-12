// Spans e indexação (spec §18, §20, §21, §50, revisadas pelo plano 24).
//
//     lapis run examples/spans.ls
//     lapis pe  examples/spans.ls    # veja a metade de cima sumir
//
// Um span é uma sequência de tamanho fixo, e o tamanho **está no tipo**:
// `.[10, 20, 30]` é `[Int;3]`. O ponto antes do colchete é o mesmo da construção
// de `type` — `[` sozinho abre um tipo, `.[` constrói um valor.
//
// A consequência é a que interessa: quando o tamanho é conhecido e o índice é
// constante, o compilador verifica os limites e a indexação devolve **o
// elemento**. Onde o tamanho se perde, a checagem sobra para a execução e o
// resultado vem embrulhado num `Option`. As duas metades deste arquivo são
// exatamente esses dois casos.
//
// Saída esperada:
//   10
//   30
//   3
//   Option.Some(10)
//   Option.None
//   3

// ----------------------------------------------------- tamanho no tipo

def numbers = .[10, 20, 30];

// Total: os limites foram provados em compilação, então sai o `Int`.
print(numbers[0]);
print(numbers[2]);

// `numbers[3]` aqui seria erro de compilação (LAP0244), não `None` em execução.

// E o tamanho, sendo do tipo, é uma constante — o `lapis pe` dobra esta linha
// para `print(3);`.
print(numbers.length);

// -------------------------------------------------- tamanho desconhecido

// Um `var` pode ser reatribuído a um span de outro tamanho, então o tamanho sai
// do tipo já na declaração: `mutaveis` é `[Int;?]`.
var mutaveis = .[10, 20, 30];

// Parcial: o compilador não sabe o tamanho, então a indexação devolve `Option`.
print(mutaveis[0]);
print(mutaveis[3]);

// O valor, esse, sempre carrega a quantidade junto do dado — `length` continua
// respondendo, só que agora em execução.
print(mutaveis.length);

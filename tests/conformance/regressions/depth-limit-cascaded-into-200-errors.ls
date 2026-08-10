// Encontrado ao escrever o caso de LAP0115.
//
// Estourado o limite de profundidade, o parser recuperava até o fim do
// statement e então **cada uma** das ~200 chamadas recursivas ainda no ar
// reportava o seu `)` faltante: 201 erros para um problema só, com o único
// diagnóstico útil enterrado no topo.
//
// A recuperação também não parava no `;`: começando já dentro de 200
// parênteses, os fechamentos excedentes levavam o contador a negativo e o
// resto do arquivo era engolido — daí o `def` mal formado abaixo, que precisa
// continuar sendo analisado.
// expect: error LAP0115 at 14:209
// expect: error LAP0103 at 15:7
def x = (((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((1)))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))));
def a 1;

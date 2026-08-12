// E o outro lado da regra: quando um `goto` **explícito** atravessa a fronteira,
// o destino não enxerga o que foi declarado no caminho.
//
// Não é limitação, é a verdade — `goto fim` pula o `var contador = 0;`, então em
// `fim` não há contador nenhum. Os dois rótulos continuam irmãos, e o diagnóstico
// aponta exatamente o que o programa não pode fazer.
// expect: error LAP0201 at 16:1
// expect: error LAP0201 at 16:12
var entrar = true;

label a;
goto fim if entrar;
var contador = 0;

label fim;
contador = contador + 1;

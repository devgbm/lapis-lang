// Um `var` declarado depois de um rótulo **é** visível no rótulo seguinte,
// quando nenhum salto explícito atravessa a fronteira entre os dois.
//
// A única forma de chegar em `b` é caindo do fim do segmento de `a` — e cair
// executa o `var contador = 0;`. Os rótulos, então, não precisam ser irmãos: `b`
// abre um grupo **aninhado** no corpo de `a`, e a declaração o envolve como um
// `Let` comum.
//
// Onde a declaração pode ser pulada, a regra antiga continua valendo: ver
// `scope_across_joins_with_jump.ls`.
// expect: output
// 1
// ---
label a;
var contador = 0;

label b;
contador = contador + 1;

print(contador);

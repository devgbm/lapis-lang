// Um `var` declarado dentro de um join não é visível no join seguinte — a mesma
// regra que vale entre o `goto` e o `label` (o salto pode ter pulado a
// declaração), aplicada entre joins do mesmo grupo.
//
// Na prática: toda declaração que o laço usa precisa vir antes do primeiro
// rótulo. É a ergonomia que join com parâmetros resolveria.
// expect: error LAP0201 at 13:1
// expect: error LAP0201 at 13:12
label a;
var contador = 0;

label b;
contador = contador + 1;

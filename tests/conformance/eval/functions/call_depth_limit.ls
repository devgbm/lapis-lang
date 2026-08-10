// LAP0302 — profundidade de chamada excedida.
//
// Sem recursão (spec §8) só se chega ao limite de 10.000 quadros empilhando
// 10.000 chamadas aninhadas no texto, o que esbarra antes no limite de
// profundidade do parser (LAP0115, 200). O código existe para quando houver
// recursão; até lá é inalcançável a partir de fonte.
// skip: LAP0302 é inalcançável sem recursão (spec §8)
// expect: abort
// ---
print(1);

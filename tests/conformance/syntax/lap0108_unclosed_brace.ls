// LAP0108 — corpo de enum que o arquivo termina sem fechar.
//
// O `;` da declaração some junto, então LAP0102 vem no mesmo arquivo: é o
// desfecho honesto de uma entrada truncada, não cascata.
// expect: error LAP0102 at 7:1
// expect: error LAP0108 at 8:1
def Cor = enum { Vermelho, Azul

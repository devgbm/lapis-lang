// spec §42 — `values[1]` com tamanho e índice conhecidos: a checagem de limites
// acontece no **checker**, e o que sobra em execução é a leitura.
// expect: output
// 20
// ---
def values = .[10, 20, 30];

print(values[1]);

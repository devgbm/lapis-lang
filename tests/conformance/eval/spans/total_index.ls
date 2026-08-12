// Com o tamanho no tipo e o índice constante, a indexação é **total**: o
// compilador já provou os limites e o elemento sai sem envelope (plano 24 §24.5).
//
// É o outro lado de `never_aborts.ls`, que fala do caso em que o tamanho se
// perdeu. Aqui não existe braço de erro porque não existe erro possível — a
// leitura fora dos limites nem chega a compilar (`types/lap0244_...`).
//
// O índice constante não precisa ser um literal escrito na posição: um `def`
// ligado a literal é constante pela mesma regra da Q18.
// expect: output
// 1
// 3
// 2
// ---
def a = .[1, 2, 3];

print(a[0]);
print(a[2]);

def meio = 1;

print(a[meio]);

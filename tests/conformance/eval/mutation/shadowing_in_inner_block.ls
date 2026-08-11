// Sombrear num bloco interno continua valendo, e o `var` de fora não muda.
// expect: output
// 2
// 1
// ---
var x = 1;

def bloco = { var x = 2; x };

print(bloco);
print(x);

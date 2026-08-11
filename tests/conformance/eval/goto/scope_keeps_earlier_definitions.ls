// O que foi declarado antes do primeiro salto envolve o grupo de joins, e
// continua em escopo no destino (plano 16 §16.4, regra 2).
// expect: output
// 10
// ---
def x = 10;
goto fim;
print(0);
label fim;
print(x);

// O que foi declarado antes de um `loop` continua em escopo depois dele — é
// escopo léxico comum, sem nada de especial a dizer (diferente do antigo
// goto/label, plano 16 §16.4 regra 2, retirado no plano 26).
// expect: output
// 10
// ---
def x = 10;

loop {
    break;
}

print(x);

// `@unless` escrito à mão: é a forma que o prelude gera por macro (plano 20,
// reescrita sobre `if` no plano 26 — Q32).
// expect: output
// executou
// ---
def condicao = false;

if !condicao {
    print("executou");
}

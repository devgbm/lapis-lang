// `@unless` escrito à mão: é a forma que o plano 20 vai gerar por macro.
// expect: output
// executou
// ---
def condicao = false;

goto fim if condicao;
print("executou");
label fim;

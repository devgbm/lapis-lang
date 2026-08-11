// Um laço atravessa o PE intacto: especializar fluxo cíclico exige widening ou
// combustível, e isso é o plano 14. O que o PE faz aqui é dobrar o que está
// dentro de cada segmento — sem tocar nos saltos.
// expect: output
// 1
// 2
// 3
// ---
var i = 0;

label repete;
i = i + 1;
print(i);
goto repete if i < 1 + 2;

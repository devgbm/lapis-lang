// Um laço atravessa o PE intacto: especializar fluxo cíclico exige widening ou
// combustível, e isso é o plano 14. O que o PE faz aqui é dobrar o que está
// dentro do corpo do `loop` — sem tocar em `break`/`continue`.
// expect: output
// 1
// 2
// 3
// ---
var i = 0;

loop {
    i = i + 1;
    print(i);
    if i < 1 + 2 { continue; } else { break; }
}

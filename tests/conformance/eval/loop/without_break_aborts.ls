// Um `loop` sem `break` que nunca para por conta própria: só o orçamento de
// iterações interrompe. É a evidência de que a terminação deixou de ser
// garantida por construção — e de que ela vira diagnóstico, não travamento
// (plano 26, Q32 — substitui o salto para trás do plano 16).
// expect: error LAP0303
// expect: abort
loop { }

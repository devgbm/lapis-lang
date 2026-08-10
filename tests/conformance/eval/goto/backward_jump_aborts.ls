// Salto para trás: com bindings imutáveis nada muda entre as voltas, então o
// laço só termina pelo orçamento de saltos. É a evidência de que a terminação
// deixou de ser garantida por construção — e de que ela vira diagnóstico, não
// travamento.
// expect: error LAP0303
// expect: abort
label repete;
goto repete;

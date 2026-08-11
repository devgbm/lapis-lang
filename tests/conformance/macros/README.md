# Casos de macro

Um por afirmação da spec de macros que dá para observar do lado de fora: o que o
programa imprime, ou o diagnóstico que ele produz.

A expansão acontece entre o parser e o desugar, então estes casos passam pelo
mesmo `Pipeline` que todos os outros — e, como qualquer caso do corpus, também
exercitam o partial evaluator e as propriedades da semântica de graça.

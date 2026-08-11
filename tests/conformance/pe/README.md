# Casos de partial evaluation

Estes arquivos são casos de conformidade normais: rodam pelo `Pipeline` como
qualquer outro, e o cabeçalho `// expect:` afirma o que o **programa** faz.

O partial evaluator é exercitado sobre eles de graça, por
`PartialEvaluationPropertyTests`, que roda as invariantes do plano 12 sobre o
corpus inteiro:

- `evaluate(P) ≡ evaluate(PE(P))` — valor, saída e desfecho (spec §40);
- o residual reparseia e passa no type checker;
- `PE(PE(P)) == PE(P)`;
- o PE nunca lança.

Ou seja: um caso escrito aqui para exercitar o PE também trava a semântica, e um
caso escrito em qualquer outro diretório também exercita o PE. É de propósito —
o PE precisa funcionar sobre a linguagem toda, não sobre os exemplos que alguém
lembrou de escrever para ele.

// `@while` vem do `prelude.ls` — este programa não a declara.
//
// É a demonstração mais forte da tese do sistema de macros: um laço, que em
// qualquer outra linguagem é trabalho de compilador, aqui são seis linhas de
// biblioteca escritas sobre `goto` e `label` (spec de macros §12.2).
//
// Só passou a iterar de verdade com `var` (Q25): antes da mutação nada mudava
// entre as voltas, e o laço ou não rodava ou não parava.
//
// As declarações ficam **antes** da invocação: o que é declarado depois de um
// rótulo vive dentro daquele join, e a expansão gera o primeiro rótulo antes do
// corpo.
// expect: output
// 1
// 2
// 3
// 55
// ---
var i = 0;
var soma = 0;
var n = 1;

@while i < 3 {
    i = i + 1;
    print(i);
}

@while n <= 10 {
    soma = soma + n;
    n = n + 1;
}

print(soma);
